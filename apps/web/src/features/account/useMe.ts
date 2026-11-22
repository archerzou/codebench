import { useQuery } from '@tanstack/react-query'
import { z } from 'zod'
import { useApi } from '@/lib/api'
import { useSession } from '@/lib/auth'

/** `GET /me` (docs/api-contract.md). Permissions drive UI hints only; the API enforces them (FE-08). */
export const meSchema = z.object({
  id: z.string(),
  email: z.string(),
  role: z.enum(['learner', 'author', 'admin']),
  plan: z.enum(['free', 'pro']),
  permissions: z.array(z.string()),
})

export type Me = z.infer<typeof meSchema>

export const meKey = ['me'] as const

/** The signed-in user's account; disabled while signed out. */
export function useMe() {
  const api = useApi()
  const { isLoaded, isSignedIn } = useSession()
  return useQuery({
    queryKey: meKey,
    queryFn: ({ signal }) => api('/me', meSchema, { signal }),
    enabled: isLoaded && isSignedIn,
    staleTime: 5 * 60_000,
  })
}
