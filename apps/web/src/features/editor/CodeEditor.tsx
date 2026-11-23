import { useEffect } from 'react'
import Editor, { type OnMount } from '@monaco-editor/react'
import { Spinner } from '@/components/Spinner'
import { useResolvedTheme } from '@/stores/useThemeStore'
import { defineCodebenchTheme, monaco } from './monaco'

export type CodeEditorHandle = Parameters<OnMount>[0]

type CodeEditorProps = {
  slug: string
  initialCode: string
  onChange: (code: string) => void
  onReady?: (editor: CodeEditorHandle) => void
}

const MONO_FALLBACK = '"JetBrains Mono", ui-monospace, "Cascadia Code", "SF Mono", Consolas, monospace'

function monoFont(): string {
  const value = getComputedStyle(document.documentElement).getPropertyValue('--font-mono').trim()
  return value === '' ? MONO_FALLBACK : value
}

/** Monaco (`csharp`) with the CodeBench theme. One model per problem (`{slug}/Solution.cs`). */
export function CodeEditor({ slug, initialCode, onChange, onReady }: CodeEditorProps) {
  const theme = useResolvedTheme()
  // Re-read the tokens when the theme flips: <html class="dark"> has already changed by now.
  useEffect(() => {
    monaco.editor.setTheme(defineCodebenchTheme(monaco, theme === 'dark'))
  }, [theme])

  return (
    <Editor
      path={`${slug}/Solution.cs`}
      defaultLanguage="csharp"
      defaultValue={initialCode}
      theme={theme === 'dark' ? 'codebench-dark' : 'codebench-light'}
      loading={<Spinner label="Loading editor" />}
      beforeMount={() => {
        defineCodebenchTheme(monaco, theme === 'dark')
      }}
      onMount={(editor) => {
        onReady?.(editor)
      }}
      onChange={(value) => {
        onChange(value ?? '')
      }}
      options={{
        fontFamily: monoFont(),
        fontSize: 14,
        lineHeight: 22,
        minimap: { enabled: false },
        scrollBeyondLastLine: false,
        tabSize: 4,
        automaticLayout: true,
        renderLineHighlight: 'line',
        padding: { top: 12 },
        ariaLabel: 'Solution code editor',
        accessibilitySupport: 'auto',
      }}
    />
  )
}
