import { Badge } from '@/components/Badge'
import { type Track, trackLabel } from '@/lib/domain'
import { cn } from '@/lib/utils'

type TrackBadgeProps = {
  track: Track
  className?: string
}

/** Mono, lowercase track name. */
export function TrackBadge({ track, className }: TrackBadgeProps) {
  return (
    <Badge tone="neutral" className={cn('border-transparent bg-transparent px-0 font-mono text-ink-muted', className)}>
      {trackLabel(track)}
    </Badge>
  )
}
