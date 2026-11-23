import { useEffect } from 'react'
import { SearchX } from 'lucide-react'
import { Link, useParams } from 'react-router-dom'
import { Button } from '@/components/Button'
import { EmptyState } from '@/components/EmptyState'
import { ErrorBanner } from '@/components/ErrorBanner'
import { Spinner } from '@/components/Spinner'
import { useProblem } from '@/features/problems'
import { ApiError } from '@/lib/api'
import { EditorWorkspace } from './EditorWorkspace'
import { StatementPane } from './StatementPane'

/** `/problems/:slug` — statement + budgets, Monaco with the starter code or your draft. */
export function EditorPage() {
  const { slug = '' } = useParams()
  const query = useProblem(slug)

  useEffect(() => {
    document.title = query.data ? `${query.data.title} · CodeBench` : 'CodeBench'
  }, [query.data])

  if (query.isPending) {
    return (
      <main id="main" className="flex flex-1 items-center justify-center p-8">
        <Spinner size="lg" label="Loading problem" />
      </main>
    )
  }

  if (query.isError) {
    const notFound = query.error instanceof ApiError && query.error.isNotFound
    return (
      <main id="main" className="mx-auto w-full max-w-3xl p-8">
        {notFound ? (
          <EmptyState
            icon={SearchX}
            title="Problem not found"
            body="It may not be published yet, or it's a Pro problem and your plan doesn't include it."
            action={
              <Button asChild variant="secondary" size="sm">
                <Link to="/">Back to problems</Link>
              </Button>
            }
          />
        ) : (
          <ErrorBanner
            error={query.error}
            retrying={query.isFetching}
            onRetry={() => {
              void query.refetch()
            }}
          />
        )}
      </main>
    )
  }

  const problem = query.data
  return (
    <main id="main" className="flex flex-1 flex-col">
      <EditorWorkspace
        key={problem.slug}
        slug={problem.slug}
        starterCode={problem.starterCode}
        statement={<StatementPane problem={problem} />}
      />
    </main>
  )
}
