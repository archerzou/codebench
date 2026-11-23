import '@testing-library/jest-dom/vitest'
import { cleanup } from '@testing-library/react'
import { afterAll, afterEach, beforeAll, vi } from 'vitest'
import { server } from './server'
import { session } from './session'

// The thin wrapper around Clerk's useAuth (FE-12): tests flip `session.signedIn` instead of talking to Clerk.
vi.mock('@/lib/auth', () => ({
  useSession: () => ({
    isLoaded: true,
    isSignedIn: session.signedIn,
    getToken: () => Promise.resolve(session.signedIn ? 'test-session-jwt' : null),
  }),
}))

// jsdom lacks these; Radix and the theme store use them.
Object.defineProperty(window, 'matchMedia', {
  writable: true,
  value: (query: string) => ({
    matches: false,
    media: query,
    onchange: null,
    addEventListener: () => undefined,
    removeEventListener: () => undefined,
    addListener: () => undefined,
    removeListener: () => undefined,
    dispatchEvent: () => false,
  }),
})
window.HTMLElement.prototype.scrollIntoView = () => undefined
window.HTMLElement.prototype.hasPointerCapture = () => false
window.HTMLElement.prototype.releasePointerCapture = () => undefined

beforeAll(() => {
  server.listen({ onUnhandledFrame: 'error' }) // MSW 3: fail on any request without a handler
})
afterEach(() => {
  cleanup()
  server.resetHandlers()
  session.signedIn = false
  localStorage.clear()
})
afterAll(() => {
  server.close()
})
