import { ClerkProvider } from '@clerk/clerk-react'
import { shadcn } from '@clerk/themes'
import { Outlet, useNavigate } from 'react-router-dom'
import { TooltipProvider } from '@/components/ui/tooltip'
import { Toaster } from '@/components/ui/sonner'
import { config } from '@/config'

/**
 * Root route element. ClerkProvider lives *inside* the router so Clerk navigates with react-router
 * (no full reloads between /sign-in steps). Clerk's shadcn theme reads our tokens (index.css).
 */
export function RootLayout() {
  const navigate = useNavigate()

  return (
    <ClerkProvider
      publishableKey={config.clerkPublishableKey}
      afterSignOutUrl={config.marketingUrl || '/'}
      signInUrl="/sign-in"
      signUpUrl="/sign-up"
      routerPush={(to) => void navigate(to)}
      routerReplace={(to) => void navigate(to, { replace: true })}
      appearance={{ baseTheme: shadcn }}
    >
      <TooltipProvider delayDuration={300}>
        <Outlet />
        <Toaster />
      </TooltipProvider>
    </ClerkProvider>
  )
}
