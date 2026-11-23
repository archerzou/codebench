// postbuild guard — fails the build if anything secret from ../../problems reached dist/ (FE-13, QY-04).
// A local, always-on version of the REA check RA-06 (build plan Appendix D).
//
// Secret material, from EVERY problem.yaml (live grader or not):
//   - reference-solution lines that aren't also in the public starter/signature/statement
//   - hidden tests' args and expected values (JSON-serialised)
//   - database seed/schema lines and plan rules
// Short fragments (< 16 chars, e.g. `{`, `return sum;`) are ignored to avoid false positives.
import { readdir, readFile } from 'node:fs/promises';
import { existsSync } from 'node:fs';
import path from 'node:path';
import { fileURLToPath } from 'node:url';
import { parse } from 'yaml';

const here = path.dirname(fileURLToPath(import.meta.url));
const problemsDir = path.resolve(here, '../../../problems');
const distDir = path.resolve(here, '../dist');
const MIN_LENGTH = 16;
const TEXT_EXTENSIONS = new Set(['.html', '.xml', '.js', '.mjs', '.css', '.json', '.txt', '.svg']);

const lines = (text) =>
  typeof text === 'string' ? text.replace(/\r\n?/g, '\n').split('\n').map((l) => l.trim()) : [];

function secretsOf(problem) {
  const publicText = new Set([...lines(problem.starter), ...lines(problem.statement), problem.signature?.trim()]);
  const secrets = [];

  for (const line of lines(problem.reference)) {
    if (line.length >= MIN_LENGTH && !publicText.has(line)) secrets.push(line);
  }
  for (const test of Array.isArray(problem.tests) ? problem.tests : []) {
    if (test?.hidden !== true) continue;
    for (const value of [test.args, test.expected, test.expectedException]) {
      if (value === undefined) continue;
      const json = JSON.stringify(value);
      if (json.length >= MIN_LENGTH) secrets.push(json);
    }
  }
  for (const field of [problem.schema, problem.seedSql, problem.model]) {
    for (const line of lines(field)) if (line.length >= MIN_LENGTH) secrets.push(line);
  }
  if (problem.planRules !== undefined) {
    const json = JSON.stringify(problem.planRules);
    if (json.length >= MIN_LENGTH) secrets.push(json);
  }
  return secrets;
}

async function* walk(dir) {
  for (const entry of await readdir(dir, { withFileTypes: true })) {
    const full = path.join(dir, entry.name);
    if (entry.isDirectory()) yield* walk(full);
    else yield full;
  }
}

async function main() {
  if (!existsSync(distDir)) throw new Error('dist/ not found — run astro build first');

  const secrets = [];
  for (const folder of await readdir(problemsDir, { withFileTypes: true })) {
    const file = path.join(problemsDir, folder.name, 'problem.yaml');
    if (!folder.isDirectory() || !existsSync(file)) continue;
    for (const secret of secretsOf(parse(await readFile(file, 'utf8')))) secrets.push({ slug: folder.name, secret });
  }

  const findings = [];
  let files = 0;
  for await (const file of walk(distDir)) {
    if (file.endsWith('.map')) findings.push(`${path.relative(distDir, file)}: source map shipped (FE-14)`);
    if (!TEXT_EXTENSIONS.has(path.extname(file))) continue;
    files++;
    // Compare against the decoded text as well, so HTML-escaped code (&lt; &gt; &amp; &quot;) is caught.
    const raw = await readFile(file, 'utf8');
    const text = raw + '\n' + raw.replace(/&lt;/g, '<').replace(/&gt;/g, '>').replace(/&quot;/g, '"').replace(/&#39;/g, "'").replace(/&amp;/g, '&');
    for (const { slug, secret } of secrets) {
      if (text.includes(secret)) findings.push(`${path.relative(distDir, file)}: contains hidden material of '${slug}': ${secret.slice(0, 60)}`);
    }
  }

  if (findings.length > 0) {
    console.error(`verify-dist: FAILED — ${findings.length} finding(s):\n  ${findings.join('\n  ')}`);
    process.exit(1);
  }
  console.log(`verify-dist: ok — ${files} files scanned against ${secrets.length} secret fragments, no source maps`);
}

main().catch((error) => {
  console.error(`verify-dist: ${error.message}`);
  process.exit(1);
});
