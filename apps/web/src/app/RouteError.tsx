import { Wordmark } from '@/components/Wordmark'

/**
 * Last-resort error screen for render errors (incl. a failed lazy chunk after a deploy). Plain markup only:
 * it may render outside ClerkProvider and the router context.
 */
export function RouteError() {
  return (
    <main id="main" className="mx-auto flex max-w-xl flex-col gap-4 p-8">
      <Wordmark />
      <h1 className="text-xl font-semibold text-ink">This page failed to load</h1>
      <p className="text-ink-muted">
        Something broke on our side. Reload the page; if a new version was just released, that fetches it.
      </p>
      <div>
        <button
          type="button"
          className="h-9 rounded-md bg-accent px-4 text-sm font-medium text-on-accent hover:bg-accent-hover"
          onClick={() => {
            window.location.reload()
          }}
        >
          Reload
        </button>
      </div>
    </main>
  )
}
