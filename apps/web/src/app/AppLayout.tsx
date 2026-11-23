import { Suspense } from 'react'
import { Outlet } from 'react-router-dom'
import { Spinner } from '@/components/Spinner'
import { AppHeader } from './AppHeader'

/** Header + page. Lazy routes (the editor, later /admin) suspend inside the page area, never the header. */
export function AppLayout() {
  return (
    <>
      <a
        href="#main"
        className="sr-only z-50 rounded-md bg-surface px-3 py-2 text-sm text-ink focus:not-sr-only focus:fixed focus:top-2 focus:left-2"
      >
        Skip to content
      </a>
      <AppHeader />
      <Suspense
        fallback={
          <div className="flex flex-1 items-center justify-center p-8">
            <Spinner size="lg" />
          </div>
        }
      >
        <Outlet />
      </Suspense>
    </>
  )
}
