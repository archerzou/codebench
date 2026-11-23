/**
 * Runtime configuration (FE-05). In Docker, `docker/40-config.sh` writes `/config.js` at container start
 * (`window.__CONFIG__`), so one image runs in every environment; `npm run dev` falls back to `VITE_*` from
 * `.env.local`. Everything here is public by design — only the publishable Clerk key, never server keys.
 * This is the only file that reads `import.meta.env`.
 */
type RuntimeConfig = Partial<Record<'API_URL' | 'CLERK_PUBLISHABLE_KEY' | 'MARKETING_URL', string>>

declare global {
  // `interface` is required for declaration merging with lib.dom's Window.
  // eslint-disable-next-line @typescript-eslint/consistent-type-definitions
  interface Window {
    __CONFIG__?: RuntimeConfig
  }
}

const runtime: RuntimeConfig = window.__CONFIG__ ?? {}

/** An unset value in config.js is written as "" by the entrypoint script; treat it as missing. */
function pick(value: string | undefined, fallback: string | undefined): string {
  return value !== undefined && value !== '' ? value : (fallback ?? '')
}

function trimSlash(url: string): string {
  return url.endsWith('/') ? url.slice(0, -1) : url
}

export const config = {
  apiUrl: trimSlash(pick(runtime.API_URL, import.meta.env.VITE_API_URL)),
  clerkPublishableKey: pick(runtime.CLERK_PUBLISHABLE_KEY, import.meta.env.VITE_CLERK_PUBLISHABLE_KEY),
  marketingUrl: trimSlash(pick(runtime.MARKETING_URL, import.meta.env.VITE_MARKETING_URL)),
} as const

/** Vite inlines this as a literal, so dev-only branches (e.g. zustand devtools) are dropped from dist/ (FE-15). */
export const isDev = import.meta.env.DEV
