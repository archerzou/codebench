import { lazy } from 'react'
import { createBrowserRouter } from 'react-router-dom'
import { AppLayout } from '@/app/AppLayout'
import { NotFoundPage } from '@/app/NotFoundPage'
import { RootLayout } from '@/app/RootLayout'
import { RouteError } from '@/app/RouteError'
import { MePage, SignInPage, SignUpPage } from '@/features/account'
import { CatalogPage } from '@/features/problems'

// Monaco is big; only the editor route downloads it.
const EditorPage = lazy(async () => ({ default: (await import('@/features/editor')).EditorPage }))

/** Routes (build plan 1.8, docs/design/pages.md). /admin (3A) is added lazily behind RequirePermission. */
export const router = createBrowserRouter([
  {
    element: <RootLayout />,
    errorElement: <RouteError />,
    children: [
      {
        element: <AppLayout />,
        children: [
          { index: true, element: <CatalogPage /> },
          { path: 'problems/:slug', element: <EditorPage /> },
          { path: 'sign-in/*', element: <SignInPage /> },
          { path: 'sign-up/*', element: <SignUpPage /> },
          { path: 'me', element: <MePage /> },
          { path: '*', element: <NotFoundPage /> },
        ],
      },
    ],
  },
])
