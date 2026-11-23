import type { ProblemDetail, ProblemPage, ProblemSummary } from '@/features/problems/types'

export function summary(overrides: Partial<ProblemSummary> = {}): ProblemSummary {
  return {
    id: '0b7e1c1e-0000-4000-8000-000000000001',
    slug: 'sum-of-evens',
    title: 'Sum of Evens',
    track: 'algorithms',
    difficulty: 'easy',
    isFree: true,
    tags: ['arrays', 'allocations'],
    ...overrides,
  }
}

export function page(items: ProblemSummary[], overrides: Partial<ProblemPage> = {}): ProblemPage {
  return {
    items,
    page: 1,
    pageSize: 20,
    totalCount: items.length,
    hasNextPage: false,
    hasPreviousPage: false,
    ...overrides,
  }
}

export function detail(overrides: Partial<ProblemDetail> = {}): ProblemDetail {
  return {
    id: '0b7e1c1e-0000-4000-8000-000000000001',
    slug: 'sum-of-evens',
    title: 'Sum of Evens',
    track: 'algorithms',
    difficulty: 'easy',
    isFree: true,
    version: 1,
    signature: 'public long SumOfEvens(int[] nums)',
    statementMarkdown: 'Return the sum of all even numbers in `nums`.',
    starterCode: 'public class Solution\n{\n}\n',
    limits: { timeoutSeconds: 10, memoryMb: 256 },
    visibleTests: [
      { name: 'Example', args: [[1, 2, 3, 4]], expected: 6, compare: 'exact', timeBudgetMs: null, allocBudgetBytes: null },
      { name: 'Empty', args: [[]], expected: 0, compare: 'exact', timeBudgetMs: 50, allocBudgetBytes: 1024 },
    ],
    hiddenTestCount: 2,
    planRules: null,
    metrics: null,
    ...overrides,
  }
}
