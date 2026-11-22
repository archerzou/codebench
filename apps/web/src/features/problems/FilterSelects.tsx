import { trackLabel } from '@/lib/domain'
import { FilterSelect } from './FilterSelect'
import { difficulties, type ProblemFilters, sorts, tracks } from './types'

const ALL = 'all'

const trackOptions = [{ value: ALL, label: 'all' }, ...tracks.map((t) => ({ value: t, label: trackLabel(t) }))]
const difficultyOptions = [{ value: ALL, label: 'all' }, ...difficulties.map((d) => ({ value: d, label: d }))]
const sortOptions = sorts.map((s) => ({ value: s, label: s }))

type FilterSelectsProps = {
  filters: ProblemFilters
  onPatch: (patch: Partial<ProblemFilters>) => void
}

/** Track · Difficulty · Sort. "all" clears a filter; unknown values can't be chosen. */
export function FilterSelects({ filters, onPatch }: FilterSelectsProps) {
  return (
    <>
      <FilterSelect
        label="Track"
        value={filters.track ?? ALL}
        options={trackOptions}
        onChange={(value) => {
          onPatch({ track: tracks.find((t) => t === value) })
        }}
      />
      <FilterSelect
        label="Difficulty"
        value={filters.difficulty ?? ALL}
        options={difficultyOptions}
        onChange={(value) => {
          onPatch({ difficulty: difficulties.find((d) => d === value) })
        }}
      />
      <FilterSelect
        label="Sort"
        value={filters.sort}
        options={sortOptions}
        onChange={(value) => {
          onPatch({ sort: sorts.find((s) => s === value) ?? 'recommended' })
        }}
      />
    </>
  )
}
