import { create } from 'zustand'
import { createJSONStorage, persist } from 'zustand/middleware'

export const STATEMENT_MIN_PERCENT = 25
export const STATEMENT_MAX_PERCENT = 60
export const STATEMENT_DEFAULT_PERCENT = 40

type EditorLayoutState = {
  /** Width of the statement column on wide screens, in percent (wireframe: 40 %, remembered). */
  statementPercent: number
  setStatementPercent: (percent: number) => void
}

const clamp = (value: number) => Math.min(STATEMENT_MAX_PERCENT, Math.max(STATEMENT_MIN_PERCENT, Math.round(value)))

export const useEditorLayoutStore = create<EditorLayoutState>()(
  persist(
    (set) => ({
      statementPercent: STATEMENT_DEFAULT_PERCENT,
      setStatementPercent: (percent) => {
        set({ statementPercent: clamp(percent) })
      },
    }),
    { name: 'codebench-editor-layout', version: 1, storage: createJSONStorage(() => localStorage) },
  ),
)
