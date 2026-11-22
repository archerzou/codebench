import { useEffect } from 'react'
import { SignIn } from '@clerk/clerk-react'

/** `/sign-in/*` — Clerk's component, path routing, themed from our tokens via the shadcn theme. */
export function SignInPage() {
  useEffect(() => {
    document.title = 'Sign in · CodeBench'
  }, [])

  return (
    <main id="main" className="flex flex-1 justify-center px-4 py-12">
      <SignIn routing="path" path="/sign-in" signUpUrl="/sign-up" fallbackRedirectUrl="/" />
    </main>
  )
}
