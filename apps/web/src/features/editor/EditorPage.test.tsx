import { screen } from '@testing-library/react'
import { http, HttpResponse } from 'msw'
import { describe, expect, it, vi } from 'vitest'
import { detail } from '@/test/fixtures'
import { renderWithProviders } from '@/test/render'
import { API, server } from '@/test/server'
import { EditorPage } from './EditorPage'

// Monaco needs a real browser; the statement side and the page states are what this test covers.
vi.mock('./CodeEditor', () => ({
  CodeEditor: ({ initialCode }: { initialCode: string }) => <pre data-testid="code">{initialCode}</pre>,
}))

const renderEditor = (slug = 'sum-of-evens') =>
  renderWithProviders(<EditorPage />, { url: `/problems/${slug}`, path: '/problems/:slug' })

describe('EditorPage', () => {
  it('shows the statement, budgets, examples and only a count of hidden tests', async () => {
    server.use(http.get(`${API}/problems/sum-of-evens`, () => HttpResponse.json(detail())))

    renderEditor()

    expect(await screen.findByRole('heading', { name: 'Sum of Evens', level: 1 })).toBeInTheDocument()
    expect(screen.getByText('nums', { selector: 'code' })).toBeInTheDocument()
    expect(screen.getByText(/nums = \[1, 2, 3, 4\]/)).toHaveTextContent('nums = [1, 2, 3, 4] → 6')
    expect(screen.getByText('+ 2 hidden tests')).toBeInTheDocument()

    const budgets = screen.getByRole('region', { name: 'Budgets' })
    expect(budgets).toHaveTextContent('50 ms per test')
    expect(budgets).toHaveTextContent('1 KB per test')
    expect(budgets).toHaveTextContent('10 s · 256 MB')
  })

  it('opens the starter code, or the saved draft when there is one', async () => {
    server.use(http.get(`${API}/problems/sum-of-evens`, () => HttpResponse.json(detail())))
    localStorage.setItem(
      'codebench-drafts',
      JSON.stringify({ state: { drafts: { 'sum-of-evens': '// my draft' } }, version: 1 }),
    )
    const { useDraftStore } = await import('@/stores/useDraftStore')
    await useDraftStore.persist.rehydrate()

    renderEditor()

    expect(await screen.findByTestId('code')).toHaveTextContent('// my draft')
    useDraftStore.setState({ drafts: {} })
  })

  it('keeps Run and Submit disabled until grading exists (Phase 1)', async () => {
    server.use(http.get(`${API}/problems/sum-of-evens`, () => HttpResponse.json(detail())))

    renderEditor()

    await screen.findByRole('heading', { name: 'Sum of Evens', level: 1 })
    for (const button of screen.getAllByRole('button', { name: /run|submit/i })) {
      expect(button).toBeDisabled()
    }
  })

  it('shows "Problem not found" for a 404 (missing, unpublished, or Pro without the plan)', async () => {
    server.use(
      http.get(`${API}/problems/top-k-words`, () =>
        HttpResponse.json({ title: 'Problems.NotFound', status: 404 }, { status: 404 }),
      ),
    )

    renderEditor('top-k-words')

    expect(await screen.findByText('Problem not found')).toBeInTheDocument()
    expect(screen.getByRole('link', { name: 'Back to problems' })).toHaveAttribute('href', '/')
  })
})
