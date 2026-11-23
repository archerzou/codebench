import { renderHook } from '@testing-library/react'
import { http, HttpResponse } from 'msw'
import { describe, expect, it } from 'vitest'
import { z } from 'zod'
import { API, server } from '@/test/server'
import { session } from '@/test/session'
import { ApiError, useApi } from './api'

const okSchema = z.object({ ok: z.boolean() })

describe('useApi', () => {
  it('sends no Authorization header when signed out', async () => {
    let auth: string | null = 'unset'
    server.use(
      http.get(`${API}/me`, ({ request }) => {
        auth = request.headers.get('Authorization')
        return HttpResponse.json({ ok: true })
      }),
    )

    const { result } = renderHook(() => useApi())
    await expect(result.current('/me', okSchema)).resolves.toEqual({ ok: true })
    expect(auth).toBeNull()
  })

  it('attaches the Clerk session JWT per request when signed in', async () => {
    session.signedIn = true
    let auth: string | null = null
    server.use(
      http.get(`${API}/me`, ({ request }) => {
        auth = request.headers.get('Authorization')
        return HttpResponse.json({ ok: true })
      }),
    )

    const { result } = renderHook(() => useApi())
    await result.current('/me', okSchema)
    expect(auth).toBe('Bearer test-session-jwt')
  })

  it('turns ProblemDetails into ApiError', async () => {
    server.use(
      http.get(`${API}/problems/nope`, () =>
        HttpResponse.json(
          { title: 'Problems.NotFound', detail: "The problem 'nope' was not found.", status: 404 },
          { status: 404, headers: { 'Content-Type': 'application/problem+json' } },
        ),
      ),
    )

    const { result } = renderHook(() => useApi())
    const error: unknown = await result.current('/problems/nope', okSchema).catch((e: unknown) => e)

    expect(error).toBeInstanceOf(ApiError)
    expect(error).toMatchObject({ status: 404, code: 'Problems.NotFound', isNotFound: true })
  })

  it('falls back to the status text when the body is not ProblemDetails', async () => {
    server.use(http.get(`${API}/boom`, () => new HttpResponse('oops', { status: 502, statusText: 'Bad Gateway' })))

    const { result } = renderHook(() => useApi())
    const error: unknown = await result.current('/boom', okSchema).catch((e: unknown) => e)

    expect(error).toMatchObject({ status: 502, code: 'Bad Gateway' })
  })

  it('rejects a response that does not match the schema (FE-06)', async () => {
    server.use(http.get(`${API}/me`, () => HttpResponse.json({ ok: 'yes' })))

    const { result } = renderHook(() => useApi())
    await expect(result.current('/me', okSchema)).rejects.toBeInstanceOf(z.ZodError)
  })
})
