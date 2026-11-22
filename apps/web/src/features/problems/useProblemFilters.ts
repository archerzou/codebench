import { useCallback, useMemo } from 'react'
import { useSearchParams } from 'react-router-dom'
import type { Difficulty, Track } from '@/lib/domain'
import { difficulties, type ProblemFilters, type Sort, sorts, tracks } from './types'

function oneOf<T extends string>(values: readonly T[], value: string | null): T | undefined {
  return values.find((candidate) => candidate === value)
}

export function parseFilters(params: URLSearchParams): ProblemFilters {
  const page = Number.parseInt(params.get('page') ?? '1', 10)
  const q = params.get('q')?.trim()
  return {
    track: oneOf<Track>(tracks, params.get('track')),
    difficulty: oneOf<Difficulty>(difficulties, params.get('difficulty')),
    q: q === '' ? undefined : q,
    sort: oneOf<Sort>(sorts, params.get('sort')) ?? 'recommended',
    page: Number.isFinite(page) && page >= 1 ? page : 1,
  }
}

type FilterPatch = Partial<ProblemFilters>

const filterKeys = ['track', 'difficulty', 'q', 'sort', 'page'] as const satisfies readonly (keyof ProblemFilters)[]

/** Filters and page live in the URL (`?track=algorithms&difficulty=easy&page=2`): shareable, Back works. */
export function useProblemFilters() {
  const [params, setParams] = useSearchParams()
  const filters = useMemo(() => parseFilters(params), [params])

  const update = useCallback(
    (patch: FilterPatch, options?: { replace?: boolean }) => {
      setParams(
        (current) => {
          const next = new URLSearchParams(current)
          for (const key of filterKeys) {
            if (!(key in patch)) continue
            const value = patch[key]
            if (value === undefined || value === '' || (key === 'sort' && value === 'recommended')) {
              next.delete(key)
            } else {
              next.set(key, String(value))
            }
          }
          // Any filter change starts again at page 1.
          if (!('page' in patch) || patch.page === 1) next.delete('page')
          return next
        },
        { replace: options?.replace ?? false },
      )
    },
    [setParams],
  )

  const clear = useCallback(() => {
    setParams(new URLSearchParams())
  }, [setParams])

  const activeCount = (filters.track ? 1 : 0) + (filters.difficulty ? 1 : 0) + (filters.q ? 1 : 0)

  return { filters, update, clear, activeCount }
}
