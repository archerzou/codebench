import { screen, waitFor, within } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { http, HttpResponse } from 'msw'
import { describe, expect, it } from 'vitest'
import { page, summary } from '@/test/fixtures'
import { renderWithProviders } from '@/test/render'
import { API, server } from '@/test/server'
import { CatalogPage } from './CatalogPage'
import { ProblemList } from './ProblemList'

describe('ProblemList', () => {
  it('renders one link per problem with track, difficulty and Free/Pro as text', async () => {
    server.use(
      http.get(`${API}/problems`, () =>
        HttpResponse.json(
          page([
            summary(),
            summary({ id: 'p2', slug: 'top-k-words', title: 'Top K Words', difficulty: 'medium', isFree: false, tags: [] }),
          ]),
        ),
      ),
    )

    renderWithProviders(<ProblemList />)

    const list = await screen.findByRole('list', { name: 'Problems' })
    const links = within(list).getAllByRole('link')
    expect(links).toHaveLength(2)
    expect(links[0]).toHaveAttribute('href', '/problems/sum-of-evens')
    expect(links[0]).toHaveTextContent('Sum of Evens')
    expect(links[0]).toHaveTextContent('algorithms')
    expect(links[0]).toHaveTextContent('easy')
    expect(links[0]).toHaveTextContent('Free')
    expect(links[1]).toHaveTextContent('medium')
    expect(links[1]).toHaveTextContent('Pro')
  })

  it('sends the URL filters to the API', async () => {
    let seen: URLSearchParams | undefined
    server.use(
      http.get(`${API}/problems`, ({ request }) => {
        seen = new URL(request.url).searchParams
        return HttpResponse.json(page([summary()]))
      }),
    )

    renderWithProviders(<ProblemList />, { url: '/?track=database&difficulty=hard&q=sum&page=2&sort=title' })

    await screen.findByRole('list', { name: 'Problems' })
    expect(seen?.get('track')).toBe('database')
    expect(seen?.get('difficulty')).toBe('hard')
    expect(seen?.get('q')).toBe('sum')
    expect(seen?.get('sort')).toBe('title')
    expect(seen?.get('page')).toBe('2')
  })

  it('ignores unknown filter values instead of sending them', async () => {
    let seen: URLSearchParams | undefined
    server.use(
      http.get(`${API}/problems`, ({ request }) => {
        seen = new URL(request.url).searchParams
        return HttpResponse.json(page([summary()]))
      }),
    )

    renderWithProviders(<ProblemList />, { url: '/?track=cooking&sort=random' })

    await screen.findByRole('list', { name: 'Problems' })
    expect(seen?.has('track')).toBe(false)
    expect(seen?.has('sort')).toBe(false)
  })

  it('shows the empty state and clears the filters from it', async () => {
    server.use(http.get(`${API}/problems`, () => HttpResponse.json(page([]))))
    const user = userEvent.setup()

    const { router } = renderWithProviders(<ProblemList />, { url: '/?track=database' })

    expect(await screen.findByText('No problems match these filters')).toBeInTheDocument()
    await user.click(screen.getByRole('button', { name: 'Clear filters' }))
    await waitFor(() => {
      expect(router.state.location.search).toBe('')
    })
  })

  it('shows the ProblemDetails title and retries', async () => {
    let calls = 0
    server.use(
      http.get(`${API}/problems`, () => {
        calls++
        return calls === 1
          ? HttpResponse.json(
              { title: 'Problems.InvalidFilter', detail: 'Unknown track.', status: 400 },
              { status: 400, headers: { 'Content-Type': 'application/problem+json' } },
            )
          : HttpResponse.json(page([summary()]))
      }),
    )
    const user = userEvent.setup()

    renderWithProviders(<ProblemList />)

    const alert = await screen.findByRole('alert')
    expect(alert).toHaveTextContent('Problems.InvalidFilter')
    expect(alert).toHaveTextContent('Unknown track.')
    await user.click(within(alert).getByRole('button', { name: /retry/i }))
    expect(await screen.findByRole('list', { name: 'Problems' })).toBeInTheDocument()
  })

  it('pages forward through the URL', async () => {
    server.use(
      http.get(`${API}/problems`, () =>
        HttpResponse.json(page([summary()], { totalCount: 25, hasNextPage: true })),
      ),
    )
    const user = userEvent.setup()

    const { router } = renderWithProviders(<ProblemList />)

    await screen.findByRole('list', { name: 'Problems' })
    expect(screen.getByText('Page 1 of 2')).toBeInTheDocument()
    await user.click(screen.getByRole('button', { name: /next/i }))
    await waitFor(() => {
      expect(router.state.location.search).toBe('?page=2')
    })
  })
})

describe('CatalogPage', () => {
  it('shows the problem count and debounces search into the URL', async () => {
    server.use(http.get(`${API}/problems`, () => HttpResponse.json(page([summary()], { totalCount: 6 }))))
    const user = userEvent.setup()

    const { router } = renderWithProviders(<CatalogPage />)

    expect(await screen.findByText('6 problems')).toBeInTheDocument()
    await user.type(screen.getByRole('searchbox', { name: 'Search problems' }), 'merge')
    await waitFor(() => {
      expect(router.state.location.search).toBe('?q=merge')
    })
  })
})
