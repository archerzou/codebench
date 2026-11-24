import { cn } from '@/lib/utils'

type WordmarkProps = {
  className?: string
}

/**
 * CodeBench lockup: the mark (a prompt chevron + block cursor on an accent tile — a cursor waiting for input)
 * followed by lowercase `codebench` in the mono font. Geometry from docs/design/brand/mark.svg — keep identical to
 * apps/marketing/src/components/Wordmark.astro and the favicon (check-rules FE-18). Colours are the accent /
 * on-accent roles, so the mark follows light and dark (6.67:1 / 7.53:1, tokens.css).
 */
export function Wordmark({ className }: WordmarkProps) {
  return (
    <span className={cn('inline-flex items-center gap-2', className)}>
      <svg viewBox="0 0 32 32" aria-hidden="true" focusable="false" className="size-5 shrink-0">
        <rect width="32" height="32" rx="7" className="fill-accent" />
        <path
          d="M9.5 10.5 15 16l-5.5 5.5"
          fill="none"
          strokeWidth="3"
          strokeLinecap="round"
          strokeLinejoin="round"
          className="stroke-on-accent"
        />
        <rect x="17.5" y="14.5" width="7" height="8" rx="1.25" className="fill-on-accent" />
      </svg>
      <span className="font-mono text-base font-semibold tracking-tight text-ink">codebench</span>
    </span>
  )
}
