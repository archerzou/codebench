import { useCallback, useEffect, useRef } from 'react'
import { useDraftStore } from '@/stores/useDraftStore'

export const DRAFT_DEBOUNCE_MS = 500

/**
 * The code to open with (saved draft, else the starter) and a debounced save (editor wireframe: 500 ms).
 * `initial` is read once per slug so typing never re-renders Monaco from the store.
 */
export function useDraft(slug: string, starterCode: string) {
  const saved = useDraftStore((s) => s.drafts[slug])
  const setDraft = useDraftStore((s) => s.setDraft)
  const clearDraft = useDraftStore((s) => s.clearDraft)
  const timer = useRef<number | undefined>(undefined)
  const pending = useRef<string | undefined>(undefined)

  const flush = useCallback(() => {
    window.clearTimeout(timer.current)
    if (pending.current !== undefined) {
      setDraft(slug, pending.current)
      pending.current = undefined
    }
  }, [setDraft, slug])

  // Save what's pending when leaving the page or switching problems.
  useEffect(() => flush, [flush])

  const save = useCallback(
    (code: string) => {
      pending.current = code
      window.clearTimeout(timer.current)
      timer.current = window.setTimeout(flush, DRAFT_DEBOUNCE_MS)
    },
    [flush],
  )

  const reset = useCallback(() => {
    window.clearTimeout(timer.current)
    pending.current = undefined
    clearDraft(slug)
  }, [clearDraft, slug])

  return { initial: saved ?? starterCode, hasDraft: saved !== undefined, save, reset }
}
