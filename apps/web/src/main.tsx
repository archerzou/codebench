import { StrictMode } from 'react'
import { createRoot } from 'react-dom/client'
import { QueryClientProvider } from '@tanstack/react-query'
import { RouterProvider } from 'react-router-dom'
import { MissingConfig } from '@/app/MissingConfig'
import { config } from '@/config'
import { createQueryClient } from '@/lib/queryClient'
import { router } from '@/routes'
import { startThemeSync } from '@/stores/useThemeStore'
import './index.css'

startThemeSync()

const root = document.getElementById('root')
if (!root) throw new Error('index.html has no #root element')

const queryClient = createQueryClient()

// Providers: QueryClientProvider → RouterProvider → (root route) ClerkProvider, so Clerk can use the router.
createRoot(root).render(
  <StrictMode>
    {config.clerkPublishableKey === '' ? (
      <MissingConfig />
    ) : (
      <QueryClientProvider client={queryClient}>
        <RouterProvider router={router} />
      </QueryClientProvider>
    )}
  </StrictMode>,
)
