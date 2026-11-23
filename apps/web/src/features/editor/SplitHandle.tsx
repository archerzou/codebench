import { type RefObject, useRef } from 'react'
import {
  STATEMENT_DEFAULT_PERCENT,
  STATEMENT_MAX_PERCENT,
  STATEMENT_MIN_PERCENT,
  useEditorLayoutStore,
} from '@/stores/useEditorLayoutStore'

const STEP = 2

type SplitHandleProps = {
  /** The element whose width the percentage refers to. */
  containerRef: RefObject<HTMLElement | null>
}

/**
 * Resizes the statement / code split. A WAI-ARIA window splitter: drag with a pointer, or focus and use
 * ← → (Home / End for min / max, Enter to reset). The split is remembered (useEditorLayoutStore).
 */
export function SplitHandle({ containerRef }: SplitHandleProps) {
  const percent = useEditorLayoutStore((s) => s.statementPercent)
  const setPercent = useEditorLayoutStore((s) => s.setStatementPercent)
  const dragging = useRef(false)

  const fromPointer = (clientX: number) => {
    const box = containerRef.current?.getBoundingClientRect()
    if (box && box.width > 0) {
      setPercent(((clientX - box.left) / box.width) * 100)
    }
  }

  return (
    // A focusable separator *is* interactive (WAI-ARIA window splitter); jsx-a11y treats every separator as static.
    // eslint-disable-next-line jsx-a11y/no-noninteractive-element-interactions
    <div
      role="separator"
      aria-orientation="vertical"
      aria-label="Resize statement and code"
      aria-valuenow={percent}
      aria-valuemin={STATEMENT_MIN_PERCENT}
      aria-valuemax={STATEMENT_MAX_PERCENT}
      // eslint-disable-next-line jsx-a11y/no-noninteractive-tabindex -- see above
      tabIndex={0}
      className="group relative hidden w-1.5 shrink-0 cursor-col-resize touch-none bg-line hover:bg-line-strong editor:block"
      onPointerDown={(event) => {
        dragging.current = true
        event.currentTarget.setPointerCapture(event.pointerId)
      }}
      onPointerMove={(event) => {
        if (dragging.current) fromPointer(event.clientX)
      }}
      onPointerUp={(event) => {
        dragging.current = false
        event.currentTarget.releasePointerCapture(event.pointerId)
      }}
      onKeyDown={(event) => {
        const next: Record<string, number> = {
          ArrowLeft: percent - STEP,
          ArrowRight: percent + STEP,
          Home: STATEMENT_MIN_PERCENT,
          End: STATEMENT_MAX_PERCENT,
          Enter: STATEMENT_DEFAULT_PERCENT,
        }
        const value = next[event.key]
        if (value !== undefined) {
          event.preventDefault()
          setPercent(value)
        }
      }}
    />
  )
}
