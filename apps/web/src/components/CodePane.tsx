import { useEffect, useState } from 'react'
import { Check, Copy } from 'lucide-react'
import { Button } from '@/components/Button'
import { cn } from '@/lib/utils'

type CodePaneProps = {
  code: string
  /** Accessible name for the copy button, e.g. "Copy example". */
  label?: string
  className?: string
}

/** Read-only code / SQL / plan JSON: `bg-surface-code`, mono, horizontal scroll, copy button. */
export function CodePane({ code, label = 'Copy code', className }: CodePaneProps) {
  const [copied, setCopied] = useState(false)

  useEffect(() => {
    if (!copied) return
    const timer = window.setTimeout(() => {
      setCopied(false)
    }, 1500)
    return () => {
      window.clearTimeout(timer)
    }
  }, [copied])

  const copy = async () => {
    try {
      await navigator.clipboard.writeText(code)
      setCopied(true)
    } catch {
      // Clipboard can be unavailable (permissions, insecure origin); the code stays selectable.
    }
  }

  return (
    <div className={cn('group relative rounded-md border border-line bg-surface-code', className)}>
      <pre className="overflow-x-auto p-3 pr-10 font-mono text-sm leading-6 text-ink">
        <code>{code}</code>
      </pre>
      <Button
        type="button"
        variant="ghost"
        size="icon-sm"
        className="absolute top-1.5 right-1.5"
        aria-label={copied ? 'Copied' : label}
        onClick={() => void copy()}
      >
        {copied ? <Check aria-hidden="true" /> : <Copy aria-hidden="true" />}
      </Button>
    </div>
  )
}
