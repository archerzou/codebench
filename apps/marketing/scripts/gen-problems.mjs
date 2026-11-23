// Generates src/content/problems/<slug>.md from ../../problems/*/problem.yaml — SUMMARIES ONLY (FE-13, QY-04).
//
// Allow-list, not deny-list: each summary is built from named public fields, so a field added to problem.yaml
// later (tests, reference, schema, seedSql, model, planRules, ...) can never reach the site by accident.
// Published: slug, title, track, difficulty, free, sortOrder, tags, limits, the first paragraph of the statement,
// and test *counts* (the API's GET /problems/{slug} exposes the same counts). Never test names, args, expected
// values, starter or reference code. scripts/verify-dist.mjs re-checks the built site.
//
// Runs as predev/prebuild/precheck. The output folder is git-ignored and rebuilt from scratch every time.
import { mkdir, readdir, readFile, rm, writeFile } from 'node:fs/promises';
import { existsSync } from 'node:fs';
import path from 'node:path';
import { fileURLToPath } from 'node:url';
import { parse, stringify } from 'yaml';

const here = path.dirname(fileURLToPath(import.meta.url));
const problemsDir = path.resolve(here, '../../../problems');
const outDir = path.resolve(here, '../src/content/problems');

// Mirrors ProblemSeeder.ImplementedGraders (src/Web.Api/Database/Seeding/ProblemSeeder.cs): problems whose grader
// doesn't exist yet aren't seeded, so a "Solve in the editor" link to them would 404. Add a kind here when its
// grader ships (Phase 5: database, Phase 6: refactoring, ...).
const LIVE_GRADERS = new Set(['algorithms']);

const TRACKS = new Set(['algorithms', 'database', 'refactoring', 'architecture', 'secureCoding', 'testWriting']);
const DIFFICULTIES = new Set(['easy', 'medium', 'hard']);

function fail(file, message) {
  throw new Error(`${path.relative(process.cwd(), file)}: ${message}`);
}

function requireString(file, value, field) {
  if (typeof value !== 'string' || value.trim() === '') fail(file, `'${field}' must be a non-empty string`);
  return value.trim();
}

/** First paragraph of the Markdown statement, line breaks normalised (CRLF on Windows checkouts). */
function excerptOf(statement) {
  const normalised = statement.replace(/\r\n?/g, '\n').trim();
  const [first] = normalised.split(/\n\s*\n/);
  return first.split('\n').map((line) => line.trim()).join(' ');
}

function summarise(file, raw) {
  const p = parse(raw);
  if (p === null || typeof p !== 'object') fail(file, 'not a YAML mapping');

  const slug = requireString(file, p.slug, 'slug');
  if (!/^[a-z0-9]+(-[a-z0-9]+)*$/.test(slug)) fail(file, `slug '${slug}' must be kebab-case`);
  const track = requireString(file, p.track, 'track');
  if (!TRACKS.has(track)) fail(file, `unknown track '${track}'`);
  const difficulty = requireString(file, p.difficulty, 'difficulty');
  if (!DIFFICULTIES.has(difficulty)) fail(file, `unknown difficulty '${difficulty}'`);
  const tests = Array.isArray(p.tests) ? p.tests : [];

  return {
    grader: requireString(file, p.grader, 'grader'),
    data: {
      slug,
      title: requireString(file, p.title, 'title'),
      track,
      difficulty,
      free: p.free === true,
      sortOrder: Number.isInteger(p.sortOrder) ? p.sortOrder : 1000,
      tags: Array.isArray(p.tags) ? p.tags.map((t) => String(t)) : [],
      limits: {
        timeoutSeconds: Number(p.limits?.timeoutSeconds ?? 10),
        memoryMb: Number(p.limits?.memoryMb ?? 256),
      },
      visibleTestCount: tests.filter((t) => t?.hidden !== true).length,
      hiddenTestCount: tests.filter((t) => t?.hidden === true).length,
    },
    excerpt: excerptOf(requireString(file, p.statement, 'statement')),
  };
}

async function main() {
  if (!existsSync(problemsDir)) {
    throw new Error(`problems folder not found at ${problemsDir} (Docker: pass the "problems" additional build context)`);
  }

  await rm(outDir, { recursive: true, force: true });
  await mkdir(outDir, { recursive: true });

  const folders = (await readdir(problemsDir, { withFileTypes: true })).filter((d) => d.isDirectory());
  const written = [];
  const skipped = [];

  for (const folder of folders) {
    const file = path.join(problemsDir, folder.name, 'problem.yaml');
    if (!existsSync(file)) continue;

    const { grader, data, excerpt } = summarise(file, await readFile(file, 'utf8'));
    if (data.slug !== folder.name) fail(file, `slug '${data.slug}' doesn't match its folder '${folder.name}'`);
    if (!LIVE_GRADERS.has(grader)) {
      skipped.push(`${data.slug} (${grader})`);
      continue;
    }

    const body = `---\n${stringify(data).trimEnd()}\n---\n\n${excerpt}\n`;
    await writeFile(path.join(outDir, `${data.slug}.md`), body, 'utf8');
    written.push(data.slug);
  }

  console.log(`gen-problems: ${written.length} summaries [${written.join(', ')}]` +
    (skipped.length ? `; waiting for their grader: ${skipped.join(', ')}` : ''));
}

main().catch((error) => {
  console.error(`gen-problems: ${error.message}`);
  process.exit(1);
});
