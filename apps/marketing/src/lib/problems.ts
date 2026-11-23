import { getCollection, type CollectionEntry } from 'astro:content';
import type { Track } from './domain';

export type Problem = CollectionEntry<'problems'>;

/** Every generated summary in catalog order (sortOrder, then title) — the same order the app's catalog uses. */
export async function getProblems(): Promise<Problem[]> {
  const all = await getCollection('problems');
  return all.sort((a, b) => a.data.sortOrder - b.data.sortOrder || a.data.title.localeCompare(b.data.title));
}

/** Plain-text one-liner from the excerpt (Markdown code ticks and emphasis dropped) for lists and meta tags. */
export function plainExcerpt(problem: Problem): string {
  return (problem.body ?? '').replace(/[`*_]/g, '').replace(/\s+/g, ' ').trim();
}

export function groupByTrack(problems: Problem[]): Map<Track, Problem[]> {
  const groups = new Map<Track, Problem[]>();
  for (const problem of problems) {
    const list = groups.get(problem.data.track) ?? [];
    list.push(problem);
    groups.set(problem.data.track, list);
  }
  return groups;
}
