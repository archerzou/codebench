import { LoaderCircle } from 'lucide-react'
import { cn } from '@/lib/utils'

type SpinnerProps = {
  size?: 'sm' | 'md' | 'lg'
  /** Announced to screen readers. */
  label?: string
  className?: string
}

const sizes = { sm: 'size-4', md: 'size-5', lg: 'size-8' } as const

/** `role="status"` + visually hidden label; the spin stops under reduced motion (tokens.css). */
export function Spinner({ size = 'md', label = 'Loading', className }: SpinnerProps) {
  return (
    <span role="status" className={cn('inline-flex items-center text-ink-muted', className)}>
      <LoaderCircle aria-hidden="true" className={cn('animate-spin', sizes[size])} />
      <span className="sr-only">{label}</span>
    </span>
  )
}
