import { SearchX } from 'lucide-react'
import { Button } from '@/components/Button'
import { EmptyState } from '@/components/EmptyState'
import { ErrorBanner } from '@/components/ErrorBanner'
import { Skeleton } from '@/components/ui/skeleton'
import { Pagination } from './Pagination'
import { ProblemRow } from './ProblemRow'
import { useProblemFilters } from './useProblemFilters'
import { PAGE_SIZE, useProblems } from './useProblems'

const SKELETON_ROWS = 6

/** The catalog list: 6 skeleton rows while loading, EmptyState when nothing matches, inline error + Retry. */
export function ProblemList() {
  const { filters, update, clear } = useProblemFilters()
  const query = useProblems(filters)

  if (query.isPending) {
    return (
      <div aria-busy="true" aria-label="Loading problems" className="divide-y rounded-lg border">
        {Array.from({ length: SKELETON_ROWS }, (_, index) => (
          <div key={index} className="flex flex-col gap-2 px-4 py-3 pl-12">
            <Skeleton className="h-4 w-2/5" />
            <Skeleton className="h-3 w-1/4" />
          </div>
        ))}
      </div>
    )
  }

  if (query.isError) {
    return (
      <ErrorBanner
        error={query.error}
        retrying={query.isFetching}
        onRetry={() => {
          void query.refetch()
        }}
      />
    )
  }

  const { items, totalCount, page, hasNextPage, hasPreviousPage } = query.data

  if (items.length === 0) {
    return (
      <EmptyState
        icon={SearchX}
        title="No problems match these filters"
        body="Try a different track or difficulty, or clear the search."
        action={
          <Button variant="secondary" size="sm" onClick={clear}>
            Clear filters
          </Button>
        }
      />
    )
  }

  return (
    <div className="flex flex-col gap-6">
      <ul aria-label="Problems" aria-busy={query.isPlaceholderData} className="divide-y rounded-lg border">
        {items.map((problem) => (
          <ProblemRow key={problem.id} problem={problem} />
        ))}
      </ul>
      <Pagination
        page={page}
        totalPages={Math.ceil(totalCount / PAGE_SIZE)}
        hasPrevious={hasPreviousPage}
        hasNext={hasNextPage}
        onPage={(next) => {
          update({ page: next })
        }}
      />
    </div>
  )
}
