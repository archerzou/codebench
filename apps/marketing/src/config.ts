/**
 * Build-time configuration — the only file that reads import.meta.env (same rule as apps/web, FE-05).
 * PUBLIC_APP_URL comes from the Docker build arg APP_URL (Dockerfile) or apps/marketing/.env locally.
 */
const appUrl = (import.meta.env.PUBLIC_APP_URL ?? 'http://app.codebench.localhost').replace(/\/+$/, '');

export const config = {
  appUrl,
  githubUrl: 'https://github.com/archerzou/codebench',
} as const;

/**
 * A link into the app that records which part of the site it came from (`?entry=` — pages.md, FE-13).
 * `entry` is a stable page-slot name such as `home-hero` or `problem-page`.
 */
export function appLink(path: string, entry: string): string {
  const url = new URL(path.startsWith('/') ? path : `/${path}`, `${appUrl}/`);
  url.searchParams.set('entry', entry);
  return url.toString();
}
