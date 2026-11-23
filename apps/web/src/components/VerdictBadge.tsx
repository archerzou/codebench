import type { LucideIcon } from 'lucide-react'
import { Braces, CircleCheck, CircleX, Info, LoaderCircle, Timer, TriangleAlert } from 'lucide-react'
import { Badge } from '@/components/Badge'
import { cn } from '@/lib/utils'

export type Verdict = 'Accepted' | 'WrongAnswer' | 'CompileError' | 'Timeout' | 'BudgetExceeded' | 'InternalError'
export type PendingStatus = 'Pending' | 'Running'

type VerdictBadgeProps = ({ verdict: Verdict; status?: never } | { status: PendingStatus; verdict?: never }) & {
  className?: string
}

type Look = { label: string; icon: LucideIcon; tone: 'pass' | 'fail' | 'warn' | 'neutral' }

const verdicts: Record<Verdict, Look> = {
  Accepted: { label: 'Accepted', icon: CircleCheck, tone: 'pass' },
  WrongAnswer: { label: 'Wrong answer', icon: CircleX, tone: 'fail' },
  CompileError: { label: 'Compile error', icon: Braces, tone: 'fail' },
  Timeout: { label: 'Timeout', icon: Timer, tone: 'warn' },
  BudgetExceeded: { label: 'Budget exceeded', icon: TriangleAlert, tone: 'warn' },
  InternalError: { label: 'Internal error', icon: Info, tone: 'neutral' },
}

const pending: Record<PendingStatus, string> = { Pending: 'Queued', Running: 'Running' }

/** Icon + word for every verdict — colour is never the only signal (FE-10). */
export function VerdictBadge(props: VerdictBadgeProps) {
  if (props.status !== undefined) {
    return (
      <Badge tone="neutral" className={props.className}>
        <LoaderCircle aria-hidden="true" className="animate-spin" />
        {pending[props.status]}
      </Badge>
    )
  }

  const look = verdicts[props.verdict]
  const Icon = look.icon
  return (
    <Badge tone={look.tone} className={cn(props.className)}>
      <Icon aria-hidden="true" />
      {look.label}
    </Badge>
  )
}
