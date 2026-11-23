import { formatBytes, formatMicros } from '@/lib/format'
import { cn } from '@/lib/utils'

type BudgetBarProps = {
  used: number
  budget: number
  unit: 'µs' | 'B'
  /** Accessible name, e.g. "Time" or "Allocations". */
  label: string
  className?: string
}

const NEAR = 0.8
const MAX_FILL = 1.25

function budgetState(used: number, budget: number): 'under' | 'near' | 'over' {
  if (used > budget) return 'over'
  if (used >= budget * NEAR) return 'near'
  return 'under'
}

const fills = { under: 'bg-pass', near: 'bg-warn', over: 'bg-fail' } as const
const words = { under: 'within budget', near: 'near budget', over: 'over budget' } as const

/** Usage against a budget: bar + tabular label, `role="meter"`. Over budget is visibly not a pass. */
export function BudgetBar({ used, budget, unit, label, className }: BudgetBarProps) {
  const state = budgetState(used, budget)
  const format = unit === 'µs' ? formatMicros : formatBytes
  const ratio = budget > 0 ? Math.min(used / budget, MAX_FILL) : MAX_FILL
  const text = `${format(used)} / ${format(budget)}`

  return (
    <div className={cn('flex items-center gap-2', className)}>
      <div
        role="meter"
        aria-label={label}
        aria-valuemin={0}
        aria-valuemax={budget}
        aria-valuenow={used}
        aria-valuetext={`${text}, ${words[state]}`}
        className="relative h-1.5 w-24 overflow-hidden rounded-sm bg-surface-sunken"
      >
        {/* FE-11 exception: the width is a computed value. 100 % of the track = 1.25 × budget. */}
        <div className={cn('h-full', fills[state])} style={{ width: `${(ratio / MAX_FILL) * 100}%` }} />
        <div aria-hidden="true" className="absolute inset-y-0 w-px bg-ink-muted" style={{ left: `${100 / MAX_FILL}%` }} />
      </div>
      <span className={cn('tabular text-xs whitespace-nowrap', state === 'over' ? 'text-fail' : 'text-ink-muted')}>{text}</span>
    </div>
  )
}
