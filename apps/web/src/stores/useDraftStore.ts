import { create } from 'zustand'
import { createJSONStorage, persist } from 'zustand/middleware'

type DraftState = {
  /** slug → code. Never API data; the starter code itself stays in TanStack Query (FE-03, FE-15). */
  drafts: Record<string, string>
  setDraft: (slug: string, code: string) => void
  clearDraft: (slug: string) => void
}

/** Per-problem drafts that survive reloads (FE-07). Components select one slug: `useDraftStore((s) => s.drafts[slug])`. */
export const useDraftStore = create<DraftState>()(
  persist(
    (set) => ({
      drafts: {},
      setDraft: (slug, code) => {
        set((s) => ({ drafts: { ...s.drafts, [slug]: code } }))
      },
      clearDraft: (slug) => {
        set((s) => ({ drafts: Object.fromEntries(Object.entries(s.drafts).filter(([key]) => key !== slug)) }))
      },
    }),
    { name: 'codebench-drafts', version: 1, storage: createJSONStorage(() => localStorage) },
  ),
)
