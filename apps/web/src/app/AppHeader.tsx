import { SignedIn, SignedOut, UserButton } from '@clerk/clerk-react'
import { Link, NavLink } from 'react-router-dom'
import { Button } from '@/components/Button'
import { ThemeToggle } from '@/components/ThemeToggle'
import { Wordmark } from '@/components/Wordmark'
import { cn } from '@/lib/utils'

/**
 * App header (48 px): wordmark · Problems · spacer · theme toggle · Clerk <UserButton> or "Sign in"
 * (docs/design/pages.md). The Admin link (admin:access) arrives with the console in 3A; Paths in Phase 7.
 */
export function AppHeader() {
  return (
    <header className="sticky top-0 z-40 border-b bg-surface">
      <div className="flex h-12 items-center gap-6 px-4 editor:px-6">
        <Link to="/" aria-label="CodeBench home" className="rounded-sm">
          <Wordmark />
        </Link>
        <nav aria-label="Main">
          <NavLink
            to="/"
            end
            className={({ isActive }) =>
              cn('rounded-sm text-sm text-ink-muted hover:text-ink', isActive && 'font-medium text-ink')
            }
          >
            Problems
          </NavLink>
        </nav>
        <div className="flex-1" />
        <ThemeToggle />
        <SignedIn>
          <UserButton userProfileMode="modal">
            <UserButton.MenuItems>
              <UserButton.Link label="Account" labelIcon={null} href="/me" />
            </UserButton.MenuItems>
          </UserButton>
        </SignedIn>
        <SignedOut>
          <Button asChild variant="secondary" size="sm">
            <Link to="/sign-in">Sign in</Link>
          </Button>
        </SignedOut>
      </div>
    </header>
  )
}
