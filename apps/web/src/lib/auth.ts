import { useAuth } from '@clerk/clerk-react'

export type Session = {
  isLoaded: boolean
  isSignedIn: boolean
  /** Clerk's short-lived session JWT, refreshed by Clerk. Call per request; never store it (FE-04, FE-15). */
  getToken: () => Promise<string | null>
}

/**
 * The thin wrapper around Clerk's `useAuth` the rest of the app uses — tests mock this module to toggle
 * signed-in state without a Clerk instance (FE-12, TS-04).
 */
export function useSession(): Session {
  const { isLoaded, isSignedIn, getToken } = useAuth()
  return { isLoaded, isSignedIn: isSignedIn === true, getToken }
}
