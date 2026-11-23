import type { ProblemDetail } from '@/features/problems'
import { budgetRows } from './budgets'

type BudgetsCardProps = {
  problem: ProblemDetail
}

/** Constraints visible before you write a line. Hidden tests may carry their own budgets; results show them. */
export function BudgetsCard({ problem }: BudgetsCardProps) {
  const rows = budgetRows(problem)
  return (
    <section aria-labelledby="budgets-heading" className="rounded-md border border-line bg-surface-raised p-3">
      <h2 id="budgets-heading" className="mb-2 text-xs font-medium tracking-wide text-ink-muted uppercase">
        Budgets
      </h2>
      <dl className="grid grid-cols-[auto_1fr] gap-x-6 gap-y-1 text-sm">
        {rows.map((row) => (
          <div key={`${row.label}-${row.value}`} className="contents">
            <dt className="font-mono text-ink-muted">{row.label}</dt>
            <dd className="tabular text-ink">{row.value}</dd>
          </div>
        ))}
      </dl>
    </section>
  )
}
