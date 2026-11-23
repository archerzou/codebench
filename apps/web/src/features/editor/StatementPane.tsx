import { ChevronLeft, Lock } from 'lucide-react'
import { Link } from 'react-router-dom'
import { Badge } from '@/components/Badge'
import { DifficultyBadge } from '@/components/DifficultyBadge'
import { TrackBadge } from '@/components/TrackBadge'
import type { ProblemDetail } from '@/features/problems'
import { BudgetsCard } from './BudgetsCard'
import { ExampleList } from './ExampleList'
import { Markdown } from './Markdown'

type StatementPaneProps = {
  problem: ProblemDetail
}

/** Left column: title, badges, budgets (before any run), statement, visible examples, hidden count. */
export function StatementPane({ problem }: StatementPaneProps) {
  return (
    <div className="flex flex-col gap-5 p-4 editor:p-6">
      <Link to="/" className="inline-flex w-fit items-center gap-1 text-sm text-ink-muted hover:text-ink">
        <ChevronLeft aria-hidden="true" className="size-4" />
        Problems
      </Link>

      <header className="flex flex-col gap-2">
        <h1 className="text-xl font-semibold text-ink">{problem.title}</h1>
        <div className="flex flex-wrap items-center gap-3">
          <TrackBadge track={problem.track} />
          <DifficultyBadge difficulty={problem.difficulty} />
          {problem.isFree ? (
            <Badge tone="accent">Free</Badge>
          ) : (
            <Badge tone="neutral">
              <Lock aria-hidden="true" />
              Pro
            </Badge>
          )}
        </div>
        <p className="overflow-x-auto font-mono text-xs whitespace-pre text-ink-muted">{problem.signature}</p>
      </header>

      <BudgetsCard problem={problem} />

      <section aria-label="Statement">
        <Markdown>{problem.statementMarkdown}</Markdown>
      </section>

      <ExampleList tests={problem.visibleTests} signature={problem.signature} hiddenTestCount={problem.hiddenTestCount} />
    </div>
  )
}
