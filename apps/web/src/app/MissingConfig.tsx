import { Wordmark } from '@/components/Wordmark'

/** Shown instead of the app when config.js / .env.local lacks the publishable key (Clerk would throw). */
export function MissingConfig() {
  return (
    <main className="mx-auto flex max-w-xl flex-col gap-4 p-8">
      <Wordmark />
      <h1 className="text-xl font-semibold text-ink">CodeBench isn't configured</h1>
      <p className="text-ink-muted">
        The Clerk publishable key is missing. Locally, set <code className="font-mono">VITE_CLERK_PUBLISHABLE_KEY</code>{' '}
        in <code className="font-mono">apps/web/.env.local</code>; in Docker, set{' '}
        <code className="font-mono">CLERK_PUBLISHABLE_KEY</code> in <code className="font-mono">deploy/.env</code>.
      </p>
    </main>
  )
}
