import { useCallback, useEffect, useRef, useState } from 'react'

export const SEARCH_DEBOUNCE_MS = 250

/**
 * The search box's text, pushed to `commit` 250 ms after the last keystroke (catalog wireframe). When the
 * committed value changes from outside (Back button, "Clear filters"), the box follows it.
 */
export function useSearchBox(committed: string | undefined, commit: (q: string | undefined) => void) {
  const [text, setText] = useState(committed ?? '')
  const [seen, setSeen] = useState(committed)
  const timer = useRef<number | undefined>(undefined)

  // Adjust state while rendering when the URL changed underneath us (no effect round-trip).
  if (committed !== seen) {
    setSeen(committed)
    setText(committed ?? '')
  }

  useEffect(
    () => () => {
      window.clearTimeout(timer.current)
    },
    [],
  )

  const onChange = useCallback(
    (value: string) => {
      setText(value)
      window.clearTimeout(timer.current)
      timer.current = window.setTimeout(() => {
        const q = value.trim()
        commit(q === '' ? undefined : q)
      }, SEARCH_DEBOUNCE_MS)
    },
    [commit],
  )

  return { text, onChange }
}
