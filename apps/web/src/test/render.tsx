import type { ReactElement } from 'react'
import { QueryClient, QueryClientProvider } from '@tanstack/react-query'
import { render } from '@testing-library/react'
import { createMemoryRouter, RouterProvider } from 'react-router-dom'
import { TooltipProvider } from '@/components/ui/tooltip'

type Options = {
  /** Initial URL, e.g. `/?track=algorithms`. */
  url?: string
  /** Route pattern the element is mounted at, e.g. `/problems/:slug`. */
  path?: string
}

/** Renders `ui` inside a fresh QueryClient (no retries) and a memory router. Returns the router to inspect the URL. */
export function renderWithProviders(ui: ReactElement, { url = '/', path = '/' }: Options = {}) {
  const queryClient = new QueryClient({ defaultOptions: { queries: { retry: false }, mutations: { retry: false } } })
  const router = createMemoryRouter([{ path, element: ui }], { initialEntries: [url] })
  const result = render(
    <QueryClientProvider client={queryClient}>
      <TooltipProvider>
        <RouterProvider router={router} />
      </TooltipProvider>
    </QueryClientProvider>,
  )
  return { ...result, router, queryClient }
}
