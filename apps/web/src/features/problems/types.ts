import { z } from 'zod'
import type { Difficulty, Track } from '@/lib/domain'

/** Response shapes from docs/api-contract.md, parsed at the edge (FE-06). Types are `z.infer` of these. */

export const tracks = ['algorithms', 'database', 'refactoring', 'architecture', 'secureCoding', 'testWriting'] as const satisfies readonly Track[]
export const difficulties = ['easy', 'medium', 'hard'] as const satisfies readonly Difficulty[]
export const sorts = ['recommended', 'title', 'difficulty', 'newest'] as const

export const trackSchema = z.enum(tracks)
export const difficultySchema = z.enum(difficulties)
export const compareSchema = z.enum(['exact', 'unordered', 'unorderedNested', 'approx'])

export function pagedListSchema<T extends z.ZodType>(item: T) {
  return z.object({
    items: z.array(item),
    page: z.number().int(),
    pageSize: z.number().int(),
    totalCount: z.number().int(),
    hasNextPage: z.boolean(),
    hasPreviousPage: z.boolean(),
  })
}

export const problemSummarySchema = z.object({
  id: z.string(),
  slug: z.string(),
  title: z.string(),
  track: trackSchema,
  difficulty: difficultySchema,
  isFree: z.boolean(),
  tags: z.array(z.string()),
  /** Phase 7. Hidden until the API sends it — no empty column. */
  solved: z.boolean().optional(),
})

export const problemPageSchema = pagedListSchema(problemSummarySchema)

/** Visible tests only. The schema has no field for hidden data, so none could be kept even if sent (QY-04). */
export const visibleTestSchema = z.object({
  name: z.string(),
  args: z.unknown(),
  expected: z.unknown().optional(),
  expectedException: z.string().nullish(),
  compare: compareSchema,
  timeBudgetMs: z.number().nullish(),
  allocBudgetBytes: z.number().nullish(),
})

export const problemDetailSchema = z.object({
  id: z.string(),
  slug: z.string(),
  title: z.string(),
  track: trackSchema,
  difficulty: difficultySchema,
  isFree: z.boolean(),
  version: z.number().int(),
  signature: z.string(),
  statementMarkdown: z.string(),
  starterCode: z.string(),
  limits: z.object({ timeoutSeconds: z.number(), memoryMb: z.number() }),
  visibleTests: z.array(visibleTestSchema),
  hiddenTestCount: z.number().int(),
  planRules: z.unknown().optional(),
  metrics: z.unknown().optional(),
})

export type ProblemSummary = z.infer<typeof problemSummarySchema>
export type ProblemPage = z.infer<typeof problemPageSchema>
export type ProblemDetail = z.infer<typeof problemDetailSchema>
export type VisibleTest = z.infer<typeof visibleTestSchema>
export type Sort = (typeof sorts)[number]

export type ProblemFilters = {
  track?: Track
  difficulty?: Difficulty
  q?: string
  sort: Sort
  page: number
}
