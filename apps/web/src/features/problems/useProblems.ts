import { keepPreviousData, useQuery } from '@tanstack/react-query'
import { useApi } from '@/lib/api'
import { type ProblemFilters, problemDetailSchema, problemPageSchema } from './types'

export const PAGE_SIZE = 20

export const problemKeys = {
  list: (filters: ProblemFilters) => ['problems', filters] as const,
  detail: (slug: string) => ['problems', 'detail', slug] as const,
}

export function toQueryString(filters: ProblemFilters): string {
  const params = new URLSearchParams()
  if (filters.track) params.set('track', filters.track)
  if (filters.difficulty) params.set('difficulty', filters.difficulty)
  if (filters.q) params.set('q', filters.q)
  if (filters.sort !== 'recommended') params.set('sort', filters.sort)
  params.set('page', String(filters.page))
  params.set('pageSize', String(PAGE_SIZE))
  return params.toString()
}

/** `GET /problems` — the catalog page. Keeps the previous page on screen while the next one loads. */
export function useProblems(filters: ProblemFilters) {
  const api = useApi()
  return useQuery({
    queryKey: problemKeys.list(filters),
    queryFn: ({ signal }) => api(`/problems?${toQueryString(filters)}`, problemPageSchema, { signal }),
    placeholderData: keepPreviousData,
  })
}

/** `GET /problems/{slug}` — statement, starter code, limits, visible tests and the hidden count. */
export function useProblem(slug: string) {
  const api = useApi()
  return useQuery({
    queryKey: problemKeys.detail(slug),
    queryFn: ({ signal }) => api(`/problems/${encodeURIComponent(slug)}`, problemDetailSchema, { signal }),
  })
}
