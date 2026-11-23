import { Fragment } from 'react'
import { cn } from '@/lib/utils'

type KbdProps = {
  keys: string[]
  className?: string
}

/** Shows a shortcut, e.g. `Ctrl` `Enter`. */
export function Kbd({ keys, className }: KbdProps) {
  return (
    <span className={cn('inline-flex items-center gap-0.5', className)}>
      {keys.map((key, index) => (
        <Fragment key={key}>
          {index > 0 && <span aria-hidden="true" className="text-ink-muted">+</span>}
          <kbd className="rounded-sm border border-line bg-surface-sunken px-1 font-mono text-[0.6875rem] leading-4 text-ink-muted">
            {key}
          </kbd>
        </Fragment>
      ))}
    </span>
  )
}
