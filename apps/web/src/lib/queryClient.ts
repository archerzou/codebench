import { QueryClient } from '@tanstack/react-query'
import { ApiError } from '@/lib/api'

/** Don't retry what retrying can't fix: 4xx (not found, forbidden, bad filter). Network blips and 5xx get 2 retries. */
function shouldRetry(failureCount: number, error: unknown): boolean {
  if (error instanceof ApiError && error.status >= 400 && error.status < 500) {
    return false
  }
  return failureCount < 2
}

export function createQueryClient(): QueryClient {
  return new QueryClient({
    defaultOptions: {
      queries: {
        staleTime: 60_000,
        retry: shouldRetry,
        refetchOnWindowFocus: false,
      },
      mutations: { retry: false },
    },
  })
}
