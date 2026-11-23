import { CircleCheck, Lock } from 'lucide-react'
import { Link } from 'react-router-dom'
import { Badge } from '@/components/Badge'
import { DifficultyBadge } from '@/components/DifficultyBadge'
import { TrackBadge } from '@/components/TrackBadge'
import type { ProblemSummary } from './types'

type ProblemRowProps = {
  problem: ProblemSummary
}

/** One `<a>` per row; the badges are text inside it — no nested interactive elements (wireframe). */
export function ProblemRow({ problem }: ProblemRowProps) {
  return (
    <li>
      <Link
        to={`/problems/${problem.slug}`}
        className="grid grid-cols-[1.25rem_1fr] gap-x-3 gap-y-1 px-4 py-3 transition-colors hover:bg-surface-sunken focus-visible:-outline-offset-2 editor:grid-cols-[1.25rem_minmax(0,1fr)_auto]"
      >
        <span className="row-span-2 pt-0.5">
          {problem.solved === true && (
            <>
              <CircleCheck aria-hidden="true" className="size-4 text-pass" />
              <span className="sr-only">Solved</span>
            </>
          )}
        </span>

        <span className="flex min-w-0 flex-wrap items-center gap-x-3 gap-y-1">
          <span className="font-medium text-ink">{problem.title}</span>
          <TrackBadge track={problem.track} />
          <DifficultyBadge difficulty={problem.difficulty} />
        </span>

        <span className="col-start-2 flex items-center editor:col-start-3 editor:row-start-1 editor:justify-end">
          {problem.isFree ? (
            <Badge tone="accent">Free</Badge>
          ) : (
            <Badge tone="neutral">
              <Lock aria-hidden="true" />
              Pro
            </Badge>
          )}
        </span>

        {problem.tags.length > 0 && (
          <span className="col-start-2 font-mono text-xs text-ink-muted editor:col-span-2">
            {problem.tags.join(' · ')}
          </span>
        )}
      </Link>
    </li>
  )
}
