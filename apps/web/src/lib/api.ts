import { useCallback } from 'react'
import { z } from 'zod'
import { config } from '@/config'
import { useSession } from '@/lib/auth'

/** RFC 7807 body written by the API's `CustomResults.Problem`: `title` is the error code, `detail` the sentence. */
const problemDetailsSchema = z.looseObject({
  type: z.string().optional(),
  title: z.string().optional(),
  status: z.number().optional(),
  detail: z.string().optional(),
  errors: z.unknown().optional(),
})

export type ValidationIssue = { propertyName?: string; errorMessage?: string; code?: string }

/** Every non-2xx response (and every network failure) surfaces as an `ApiError` (FE-04). */
export class ApiError extends Error {
  readonly status: number
  /** Error code such as `Problems.NotFound` (the ProblemDetails `title`). */
  readonly code: string
  readonly detail: string | undefined
  readonly errors: unknown

  constructor(status: number, code: string, detail?: string, errors?: unknown) {
    super(detail ?? code)
    this.name = 'ApiError'
    this.status = status
    this.code = code
    this.detail = detail
    this.errors = errors
  }

  get isNotFound() {
    return this.status === 404
  }

  get isUnauthorized() {
    return this.status === 401
  }

  static async from(response: Response): Promise<ApiError> {
    let body: unknown = undefined
    if (response.headers.get('content-type')?.includes('json')) {
      body = await response.json().catch(() => undefined)
    }

    const parsed = problemDetailsSchema.safeParse(body)
    const problem = parsed.success ? parsed.data : {}
    return new ApiError(
      problem.status ?? response.status,
      problem.title ?? (response.statusText || `HTTP ${response.status}`),
      problem.detail,
      problem.errors,
    )
  }

  static network(cause: unknown): ApiError {
    const error = new ApiError(0, 'Network.Unavailable', "Couldn't reach the CodeBench API. Check your connection and try again.")
    error.cause = cause
    return error
  }
}

export type ApiRequest = Omit<RequestInit, 'body'> & {
  /** Serialised as JSON. */
  body?: unknown
}

export type Api = <T>(path: string, schema: z.ZodType<T>, request?: ApiRequest) => Promise<T>

/**
 * The only way the app talks HTTP (FE-04). Attaches `Authorization: Bearer <Clerk session JWT>` per request
 * when signed in (never cached), sends/accepts JSON, parses the response with the caller's zod schema at
 * the edge (FE-06), and throws `ApiError` from ProblemDetails on failure.
 */
export function useApi(): Api {
  const { isSignedIn, getToken } = useSession()

  return useCallback(
    async <T,>(path: string, schema: z.ZodType<T>, request: ApiRequest = {}): Promise<T> => {
      const { body, headers: initHeaders, ...init } = request
      const headers = new Headers(initHeaders)
      headers.set('Accept', 'application/json')
      if (body !== undefined) {
        headers.set('Content-Type', 'application/json')
      }
      if (isSignedIn) {
        const jwt = await getToken()
        if (jwt) {
          headers.set('Authorization', `Bearer ${jwt}`)
        }
      }

      let response: Response
      try {
        response = await fetch(`${config.apiUrl}${path}`, {
          ...init,
          headers,
          body: body === undefined ? undefined : JSON.stringify(body),
        })
      } catch (cause) {
        if (cause instanceof DOMException && cause.name === 'AbortError') {
          throw cause
        }
        throw ApiError.network(cause)
      }

      if (!response.ok) {
        throw await ApiError.from(response)
      }

      const data: unknown = response.status === 204 ? undefined : await response.json()
      return schema.parse(data)
    },
    [isSignedIn, getToken],
  )
}
