import { type ReactNode, useCallback, useRef } from 'react'
import { FileCode2 } from 'lucide-react'
import { useEditorLayoutStore } from '@/stores/useEditorLayoutStore'
import { CodeEditor, type CodeEditorHandle } from './CodeEditor'
import { GradingActions } from './GradingActions'
import { ResetDialog } from './ResetDialog'
import { SplitHandle } from './SplitHandle'
import { useDraft } from './useDraft'

type EditorWorkspaceProps = {
  slug: string
  starterCode: string
  statement: ReactNode
}

/** Two resizable columns ≥ 72 rem; stacked statement → editor → sticky actions below (editor wireframe). */
export function EditorWorkspace({ slug, starterCode, statement }: EditorWorkspaceProps) {
  const draft = useDraft(slug, starterCode)
  const editorRef = useRef<CodeEditorHandle | null>(null)
  const containerRef = useRef<HTMLDivElement | null>(null)
  const statementPercent = useEditorLayoutStore((s) => s.statementPercent)

  const reset = useCallback(() => {
    draft.reset()
    editorRef.current?.setValue(starterCode)
  }, [draft, starterCode])

  return (
    <div
      ref={containerRef}
      className="flex flex-1 flex-col editor:h-[calc(100dvh-3rem)] editor:flex-row editor:overflow-hidden"
    >
      {/* FE-11 exception: the column width is a computed, user-chosen value. */}
      <aside
        aria-label="Problem"
        className="min-w-0 border-b editor:min-w-[22rem] editor:overflow-y-auto editor:border-b-0"
        style={{ flexBasis: `${String(statementPercent)}%` }}
      >
        {statement}
      </aside>

      <SplitHandle containerRef={containerRef} />

      <section aria-label="Solution" className="flex min-h-[28rem] min-w-0 flex-1 flex-col">
        <div className="flex h-11 items-center justify-between gap-2 border-b px-3">
          <span className="inline-flex items-center gap-1.5 font-mono text-sm text-ink">
            <FileCode2 aria-hidden="true" className="size-4 text-ink-muted" />
            Solution.cs
          </span>
          <div className="flex items-center gap-2">
            <ResetDialog
              onConfirm={reset}
              onClosed={() => {
                editorRef.current?.focus()
              }}
            />
            <div className="hidden editor:block">
              <GradingActions />
            </div>
          </div>
        </div>
        <div className="h-[60dvh] min-h-0 flex-1 editor:h-auto">
          <CodeEditor
            slug={slug}
            initialCode={draft.initial}
            onChange={draft.save}
            onReady={(editor) => {
              editorRef.current = editor
            }}
          />
        </div>
        <div className="sticky bottom-0 flex justify-end border-t bg-surface p-3 editor:hidden">
          <GradingActions />
        </div>
      </section>
    </div>
  )
}
