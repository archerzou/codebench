import { useEffect } from 'react'
import { SignUp } from '@clerk/clerk-react'

/** `/sign-up/*` — Clerk's component, path routing. */
export function SignUpPage() {
  useEffect(() => {
    document.title = 'Sign up · CodeBench'
  }, [])

  return (
    <main id="main" className="flex flex-1 justify-center px-4 py-12">
      <SignUp routing="path" path="/sign-up" signInUrl="/sign-in" fallbackRedirectUrl="/" />
    </main>
  )
}
