/**
 * Monaco, bundled with the app instead of @monaco-editor/react's default jsDelivr download: no third-party
 * script at runtime, works offline/behind CSP, and only the C# grammar ships (not all ~80 languages).
 * Imported only by the lazily loaded editor route, so the catalog never downloads it.
 */
import { loader } from '@monaco-editor/react'
import * as monaco from 'monaco-editor/editor/editor.api'
import 'monaco-editor/languages/definitions/csharp/register'
import EditorWorker from 'monaco-editor/editor/editor.worker?worker'

self.MonacoEnvironment = {
  getWorker: () => new EditorWorker(),
}

loader.config({ monaco })

export { monaco }
export type MonacoApi = typeof monaco
export type CodebenchTheme = 'codebench-light' | 'codebench-dark'

/** Reads a colour role from the live CSS (tokens.css), so Monaco follows the same palette as the app. */
function role(name: string): string {
  return getComputedStyle(document.documentElement).getPropertyValue(`--cb-${name}`).trim()
}

/** `#rrggbb` → `#rrggbbaa`. */
function withAlpha(hex: string, alpha: number): string {
  const channel = Math.round(alpha * 255).toString(16).padStart(2, '0')
  return `${hex}${channel}`
}

/**
 * Defines `codebench-light` / `codebench-dark` from the tokens currently applied to `<html>` (components.md
 * "Monaco theme"): background surface-code, foreground ink, line numbers ink-muted, selection = accent at 25 %,
 * cursor = accent, error squiggle = fail, warning squiggle = warn. Keywords take the accent; everything else
 * stays ink so nothing competes with the code (brief: calm editor).
 */
export function defineCodebenchTheme(api: MonacoApi, dark: boolean): CodebenchTheme {
  const name: CodebenchTheme = dark ? 'codebench-dark' : 'codebench-light'
  const surfaceCode = role('surface-code')
  const ink = role('ink')
  const inkMuted = role('ink-muted')
  const accent = role('accent')
  const line = role('line')

  api.editor.defineTheme(name, {
    base: dark ? 'vs-dark' : 'vs',
    inherit: false,
    rules: [
      { token: '', foreground: ink.slice(1), background: surfaceCode.slice(1) },
      { token: 'keyword', foreground: accent.slice(1) },
      { token: 'comment', foreground: inkMuted.slice(1), fontStyle: 'italic' },
      { token: 'string', foreground: ink.slice(1) },
      { token: 'number', foreground: ink.slice(1) },
      { token: 'type', foreground: ink.slice(1), fontStyle: 'bold' },
    ],
    colors: {
      'editor.background': surfaceCode,
      'editor.foreground': ink,
      'editorLineNumber.foreground': inkMuted,
      'editorLineNumber.activeForeground': ink,
      'editor.selectionBackground': withAlpha(accent, 0.25),
      'editor.inactiveSelectionBackground': withAlpha(accent, 0.15),
      'editorCursor.foreground': accent,
      'editor.lineHighlightBorder': line,
      'editorIndentGuide.background1': line,
      'editorError.foreground': role('fail'),
      'editorWarning.foreground': role('warn'),
      'editorWidget.background': role('surface-raised'),
      'editorWidget.border': line,
      'focusBorder': accent,
    },
  })
  return name
}
