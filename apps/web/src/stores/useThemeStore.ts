import { useSyncExternalStore } from 'react'
import { create } from 'zustand'
import { createJSONStorage, persist } from 'zustand/middleware'

export type ThemePreference = 'light' | 'dark' | 'system'
export type ResolvedTheme = 'light' | 'dark'

/** Must match the inline pre-paint script in index.html. */
export const THEME_STORAGE_KEY = 'codebench-theme'

type ThemeState = {
  preference: ThemePreference
  setPreference: (preference: ThemePreference) => void
}

/** UI preference only (FE-15): light / dark / follow the OS. Applied to `<html class="dark">` by `useApplyTheme`. */
export const useThemeStore = create<ThemeState>()(
  persist(
    (set) => ({
      preference: 'system',
      setPreference: (preference) => {
        set({ preference })
      },
    }),
    {
      name: THEME_STORAGE_KEY,
      version: 1,
      storage: createJSONStorage(() => localStorage),
      partialize: (state) => ({ preference: state.preference }),
    },
  ),
)

const darkQuery = () => window.matchMedia('(prefers-color-scheme: dark)')

export function resolveTheme(preference: ThemePreference): ResolvedTheme {
  if (preference === 'system') {
    return darkQuery().matches ? 'dark' : 'light'
  }
  return preference
}

export function applyTheme(theme: ResolvedTheme) {
  document.documentElement.classList.toggle('dark', theme === 'dark')
}

function subscribeToOs(onChange: () => void) {
  const query = darkQuery()
  query.addEventListener('change', onChange)
  return () => {
    query.removeEventListener('change', onChange)
  }
}

/** The theme actually shown (preference, or the OS setting for "system") — e.g. for Monaco. */
export function useResolvedTheme(): ResolvedTheme {
  const preference = useThemeStore((s) => s.preference)
  const osDark = useSyncExternalStore(subscribeToOs, () => darkQuery().matches)
  if (preference === 'system') return osDark ? 'dark' : 'light'
  return preference
}

/** Keeps `<html>` in sync with the preference and, for "system", with OS changes. Subscribe once at the root. */
export function startThemeSync(): () => void {
  const sync = () => {
    applyTheme(resolveTheme(useThemeStore.getState().preference))
  }
  sync()
  const query = darkQuery()
  query.addEventListener('change', sync)
  const unsubscribe = useThemeStore.subscribe(sync)
  return () => {
    query.removeEventListener('change', sync)
    unsubscribe()
  }
}
