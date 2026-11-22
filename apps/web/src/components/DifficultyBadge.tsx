import { Badge } from '@/components/Badge'
import type { Difficulty } from '@/lib/domain'
import { cn } from '@/lib/utils'

type DifficultyBadgeProps = {
  difficulty: Difficulty
  className?: string
}

const pips: Record<Difficulty, number> = { easy: 1, medium: 2, hard: 3 }

/** Neutral badge with 1–3 filled pips + the word. Difficulty isn't a status, so no red/green. */
export function DifficultyBadge({ difficulty, className }: DifficultyBadgeProps) {
  const filled = pips[difficulty]
  return (
    <Badge tone="neutral" className={cn('gap-1.5', className)}>
      <span aria-hidden="true" className="inline-flex gap-0.5">
        {[1, 2, 3].map((pip) => (
          <span
            key={pip}
            className={cn('size-1.5 rounded-full border border-ink-muted', pip <= filled && 'bg-ink-muted')}
          />
        ))}
      </span>
      {difficulty}
    </Badge>
  )
}
