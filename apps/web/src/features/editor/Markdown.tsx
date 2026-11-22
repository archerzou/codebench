import ReactMarkdown from 'react-markdown'
import { cn } from '@/lib/utils'

type MarkdownProps = {
  children: string
  className?: string
}

/**
 * Problem statements. react-markdown renders to React elements and ignores raw HTML by default, so a statement
 * can't inject markup or scripts. Styled with descendant utilities, colour roles only (FE-11).
 */
export function Markdown({ children, className }: MarkdownProps) {
  return (
    <div
      className={cn(
        'max-w-prose text-[0.9375rem] leading-7 text-ink',
        '[&_p]:mb-3 [&_ul]:mb-3 [&_ul]:list-disc [&_ul]:pl-5 [&_ol]:mb-3 [&_ol]:list-decimal [&_ol]:pl-5',
        '[&_h1]:mb-2 [&_h1]:text-lg [&_h1]:font-semibold [&_h2]:mb-2 [&_h2]:font-semibold [&_h3]:mb-1 [&_h3]:font-medium',
        '[&_code]:rounded-sm [&_code]:bg-surface-sunken [&_code]:px-1 [&_code]:py-0.5 [&_code]:font-mono [&_code]:text-[0.8125rem]',
        '[&_pre]:mb-3 [&_pre]:overflow-x-auto [&_pre]:rounded-md [&_pre]:bg-surface-code [&_pre]:p-3 [&_pre_code]:bg-transparent [&_pre_code]:p-0',
        '[&_a]:text-accent [&_a]:underline [&_a]:underline-offset-2',
        className,
      )}
    >
      <ReactMarkdown>{children}</ReactMarkdown>
    </div>
  )
}
