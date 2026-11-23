import { useEffect } from 'react'
import { ProblemFilters } from './ProblemFilters'
import { ProblemList } from './ProblemList'
import { useProblemFilters } from './useProblemFilters'
import { useProblems } from './useProblems'

/** `/` — the catalog (docs/design/wireframes/catalog.md). Primary action: open a problem. */
export function CatalogPage() {
  const { filters } = useProblemFilters()
  const { data } = useProblems(filters)

  useEffect(() => {
    document.title = 'Problems · CodeBench'
  }, [])

  return (
    <main id="main" className="mx-auto flex w-full max-w-6xl flex-col gap-6 px-4 py-8 editor:px-6">
      <header className="flex flex-wrap items-end justify-between gap-2">
        <div className="flex flex-col gap-1">
          <h1 className="text-2xl font-semibold text-ink">Problems</h1>
          <p className="text-ink-muted">Practise the parts that matter in production: speed, memory, SQL, readability.</p>
        </div>
        {data && (
          <p className="tabular text-sm text-ink-muted" aria-live="polite">
            {data.totalCount} {data.totalCount === 1 ? 'problem' : 'problems'}
          </p>
        )}
      </header>
      <ProblemFilters />
      <ProblemList />
    </main>
  )
}
