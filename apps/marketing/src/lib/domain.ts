/** API enums (camelCase on the wire, docs/api-contract.md) — the same values apps/web uses. */
export type Track = 'algorithms' | 'database' | 'refactoring' | 'architecture' | 'secureCoding' | 'testWriting';
export type Difficulty = 'easy' | 'medium' | 'hard';

/** API enum → the lowercase mono label the design uses (`secure-coding`) — same as apps/web `trackLabel`. */
export function trackLabel(track: Track): string {
  return track.replace(/[A-Z]/g, (letter) => `-${letter.toLowerCase()}`);
}

export type TrackInfo = {
  track: Track;
  /** What the grader measures, in one line. */
  measures: string;
  /** `live` = gradeable today; `next` = grader in the build plan; `later` = planned. */
  status: 'live' | 'next' | 'later';
};

/** Track overview shown on the home and pricing pages. Keep in step with the build plan's grader phases. */
export const tracks: readonly TrackInfo[] = [
  { track: 'algorithms', measures: 'Correctness, time per test and bytes allocated, against stated budgets.', status: 'live' },
  { track: 'database', measures: 'EF Core queries graded on the SQL they emit and the query plan Postgres picks.', status: 'next' },
  { track: 'refactoring', measures: 'Behaviour kept identical while complexity and size metrics go down.', status: 'next' },
  { track: 'architecture', measures: 'Dependency rules between layers and projects, checked like a build gate.', status: 'later' },
  { track: 'secureCoding', measures: 'Vulnerable code fixed without breaking the tests that pin its behaviour.', status: 'later' },
  { track: 'testWriting', measures: 'Your tests run against seeded bugs: the more they catch, the better.', status: 'later' },
];

export const statusLabel: Record<TrackInfo['status'], string> = {
  live: 'Live',
  next: 'Next',
  later: 'Planned',
};
