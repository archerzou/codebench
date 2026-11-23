import { z } from 'zod'
import type { ProblemDetail } from '@/features/problems'
import { formatBytes, formatMs } from '@/lib/format'

export type BudgetRow = { label: string; value: string }

function summarise(values: number[], format: (n: number) => string): string | undefined {
  if (values.length === 0) return undefined
  const distinct = [...new Set(values)].sort((a, b) => a - b)
  const first = distinct[0]
  const last = distinct.at(-1)
  if (first === undefined || last === undefined) return undefined
  return distinct.length === 1 ? `${format(first)} per test` : `${format(first)} – ${format(last)} per test`
}

/** Plan rules (database) and metric thresholds (refactoring), shown up front. Shape: `{ id, type, ...}`. */
const ruleSchema = z.array(
  z.looseObject({
    id: z.string().optional(),
    type: z.string(),
    value: z.union([z.number(), z.string()]).optional(),
    index: z.string().optional(),
    table: z.string().optional(),
  }),
)

function ruleRows(rules: unknown): BudgetRow[] {
  const parsed = ruleSchema.safeParse(rules)
  if (!parsed.success) return []
  return parsed.data.map((rule) => ({
    label: rule.type,
    value: String(rule.value ?? rule.index ?? rule.table ?? ''),
  }))
}

/** Budgets up front (brief, principle 5): per-test budgets from the visible examples, run limits, rules. */
export function budgetRows(problem: ProblemDetail): BudgetRow[] {
  const time = summarise(
    problem.visibleTests.flatMap((t) => (t.timeBudgetMs == null ? [] : [t.timeBudgetMs])),
    formatMs,
  )
  const alloc = summarise(
    problem.visibleTests.flatMap((t) => (t.allocBudgetBytes == null ? [] : [t.allocBudgetBytes])),
    formatBytes,
  )

  const rows: BudgetRow[] = []
  if (time) rows.push({ label: 'time', value: time })
  if (alloc) rows.push({ label: 'alloc', value: alloc })
  rows.push({
    label: 'limits',
    value: `${String(problem.limits.timeoutSeconds)} s · ${formatBytes(problem.limits.memoryMb * 1024 * 1024)}`,
  })
  return [...rows, ...ruleRows(problem.planRules), ...ruleRows(problem.metrics)]
}
