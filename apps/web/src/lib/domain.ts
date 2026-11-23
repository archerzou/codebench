/** API enums shared by components and features (camelCase strings on the wire, docs/api-contract.md). */
export type Track = 'algorithms' | 'database' | 'refactoring' | 'architecture' | 'secureCoding' | 'testWriting'
export type Difficulty = 'easy' | 'medium' | 'hard'

/** API enum (camelCase) → the lowercase, mono label the design uses (`secure-coding`). */
export function trackLabel(track: Track): string {
  return track.replace(/[A-Z]/g, (letter) => `-${letter.toLowerCase()}`)
}
