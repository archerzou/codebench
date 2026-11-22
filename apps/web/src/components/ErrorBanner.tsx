import { CircleAlert, RotateCw } from 'lucide-react'
import { Button } from '@/components/Button'
import { ApiError } from '@/lib/api'
import { cn } from '@/lib/utils'

type ErrorBannerProps = {
  error: unknown
  onRetry?: () => void
  retrying?: boolean
  className?: string
}

function describe(error: unknown): { title: string; detail?: string } {
  if (error instanceof ApiError) {
    return { title: error.code, detail: error.detail }
  }
  return { title: 'Something on our side failed', detail: 'Try again in a moment.' }
}

/** Inline error with the ProblemDetails title and a Retry button (catalog wireframe "Error"). */
export function ErrorBanner({ error, onRetry, retrying = false, className }: ErrorBannerProps) {
  const { title, detail } = describe(error)
  return (
    <div role="alert" className={cn('flex items-start gap-3 rounded-md border border-fail bg-fail-soft p-3 text-sm', className)}>
      <CircleAlert aria-hidden="true" className="mt-0.5 size-4 shrink-0 text-fail" />
      <div className="flex-1">
        <p className="font-medium text-fail">{title}</p>
        {detail && <p className="text-ink">{detail}</p>}
      </div>
      {onRetry && (
        <Button variant="secondary" size="sm" onClick={onRetry} loading={retrying}>
          <RotateCw aria-hidden="true" />
          Retry
        </Button>
      )}
    </div>
  )
}
