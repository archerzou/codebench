import { Badge } from '@/components/Badge'
import { ErrorBanner } from '@/components/ErrorBanner'
import { Skeleton } from '@/components/ui/skeleton'
import { useMe } from './useMe'

/** Email, role and plan from `GET /me`. Phase 2 adds recent submissions, Phase 7 progress. */
export function AccountDetails() {
  const me = useMe()

  if (me.isPending) {
    return (
      <div aria-busy="true" aria-label="Loading account" className="flex flex-col gap-2">
        <Skeleton className="h-4 w-64" />
        <Skeleton className="h-4 w-40" />
      </div>
    )
  }

  if (me.isError) {
    // A 404 here is `Users.NotProvisioned` (no email claim in the session yet); its detail says so.
    return (
      <ErrorBanner
        error={me.error}
        retrying={me.isFetching}
        onRetry={() => {
          void me.refetch()
        }}
      />
    )
  }

  return (
    <dl className="grid grid-cols-[auto_1fr] gap-x-8 gap-y-3 text-sm">
      <dt className="text-ink-muted">Email</dt>
      <dd className="text-ink">{me.data.email}</dd>
      <dt className="text-ink-muted">Role</dt>
      <dd>
        <Badge tone="neutral" className="font-mono">
          {me.data.role}
        </Badge>
      </dd>
      <dt className="text-ink-muted">Plan</dt>
      <dd>
        <Badge tone={me.data.plan === 'pro' ? 'accent' : 'neutral'} className="font-mono">
          {me.data.plan}
        </Badge>
      </dd>
    </dl>
  )
}
