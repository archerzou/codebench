import { useEffect } from 'react'
import { RedirectToSignIn, SignedIn, SignedOut } from '@clerk/clerk-react'
import { AccountDetails } from './AccountDetails'

/** `/me` — account basics. Signed-out visitors go to sign-in; this guard is UX only, the API enforces (FE-08). */
export function MePage() {
  useEffect(() => {
    document.title = 'Account · CodeBench'
  }, [])

  return (
    <main id="main" className="mx-auto flex w-full max-w-3xl flex-col gap-6 px-4 py-8 editor:px-6">
      <SignedIn>
        <h1 className="text-2xl font-semibold text-ink">Account</h1>
        <AccountDetails />
      </SignedIn>
      <SignedOut>
        <RedirectToSignIn />
      </SignedOut>
    </main>
  )
}
