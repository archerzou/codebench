import type { LucideIcon } from 'lucide-react'
import type { ReactNode } from 'react'
import { cn } from '@/lib/utils'

type EmptyStateProps = {
  title: string
  /** One sentence of what to do next. */
  body?: string
  icon?: LucideIcon
  action?: ReactNode
  className?: string
}

export function EmptyState({ title, body, icon: Icon, action, className }: EmptyStateProps) {
  return (
    <div className={cn('flex flex-col items-center gap-2 rounded-lg border border-dashed px-6 py-12 text-center', className)}>
      {Icon && <Icon aria-hidden="true" className="size-6 text-ink-muted" />}
      <p className="font-medium text-ink">{title}</p>
      {body && <p className="max-w-prose text-sm text-ink-muted">{body}</p>}
      {action && <div className="mt-2">{action}</div>}
    </div>
  )
}
