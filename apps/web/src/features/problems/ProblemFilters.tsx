import { useCallback, useId } from 'react'
import { Search, SlidersHorizontal } from 'lucide-react'
import { Button } from '@/components/Button'
import {
  Dialog,
  DialogContent,
  DialogDescription,
  DialogFooter,
  DialogHeader,
  DialogTitle,
  DialogTrigger,
} from '@/components/ui/dialog'
import { Input } from '@/components/ui/input'
import { FilterSelects } from './FilterSelects'
import { useProblemFilters } from './useProblemFilters'
import { useSearchBox } from './useSearchBox'

/** Search (debounced 250 ms) + track / difficulty / sort, all in the URL. Below 72 rem the selects move into a dialog. */
export function ProblemFilters() {
  const { filters, update, clear, activeCount } = useProblemFilters()
  const commitSearch = useCallback(
    (q: string | undefined) => {
      update({ q }, { replace: true })
    },
    [update],
  )
  const search = useSearchBox(filters.q, commitSearch)
  const searchId = useId()
  const selectCount = activeCount - (filters.q ? 1 : 0)

  return (
    <div className="flex flex-wrap items-center gap-3">
      <div className="relative min-w-56 flex-1 editor:max-w-sm">
        <label htmlFor={searchId} className="sr-only">
          Search problems
        </label>
        <Search
          aria-hidden="true"
          className="pointer-events-none absolute top-1/2 left-2.5 size-4 -translate-y-1/2 text-ink-muted"
        />
        <Input
          id={searchId}
          type="search"
          placeholder="Search problems…"
          className="pl-8"
          value={search.text}
          onChange={(event) => {
            search.onChange(event.target.value)
          }}
        />
      </div>

      <div className="hidden items-center gap-4 editor:flex">
        <FilterSelects filters={filters} onPatch={update} />
      </div>

      <Dialog>
        <DialogTrigger asChild>
          <Button variant="secondary" size="sm" className="editor:hidden">
            <SlidersHorizontal aria-hidden="true" />
            Filters{selectCount > 0 ? ` (${selectCount})` : ''}
          </Button>
        </DialogTrigger>
        <DialogContent>
          <DialogHeader>
            <DialogTitle>Filters</DialogTitle>
            <DialogDescription>Narrow the catalog by track and difficulty.</DialogDescription>
          </DialogHeader>
          <div className="flex flex-col items-start gap-3">
            <FilterSelects filters={filters} onPatch={update} />
          </div>
          <DialogFooter>
            <Button variant="ghost" size="sm" onClick={clear}>
              Clear filters
            </Button>
          </DialogFooter>
        </DialogContent>
      </Dialog>
    </div>
  )
}
