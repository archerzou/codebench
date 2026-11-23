import { cn } from '@/lib/utils'

type WordmarkProps = {
  className?: string
}

/** Lowercase `codebench` in the UI mono font + an accent block caret — a cursor waiting for input (brief). */
export function Wordmark({ className }: WordmarkProps) {
  return (
    <span className={cn('font-mono text-base font-semibold tracking-tight text-ink', className)}>
      codebench<span aria-hidden="true" className="text-accent">▍</span>
    </span>
  )
}
