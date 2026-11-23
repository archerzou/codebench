import type { ComponentProps } from 'react'
import { LoaderCircle } from 'lucide-react'
import { Button as ButtonPrimitive } from '@/components/ui/button'
import { cn } from '@/lib/utils'

type ButtonProps = ComponentProps<typeof ButtonPrimitive> & {
  /** Shows a spinner, disables the button and keeps its width (components.md). */
  loading?: boolean
}

/** CodeBench button: `primary | secondary | ghost | danger`, `sm | md`, `loading`. One primary per view. */
export function Button({ loading = false, disabled, children, asChild, className, ...props }: ButtonProps) {
  if (asChild) {
    return (
      <ButtonPrimitive asChild className={className} {...props}>
        {children}
      </ButtonPrimitive>
    )
  }

  return (
    <ButtonPrimitive
      disabled={disabled === true || loading}
      aria-busy={loading || undefined}
      className={cn('relative', className)}
      {...props}
    >
      {loading ? (
        <>
          <span className="invisible inline-flex items-center gap-2">{children}</span>
          <LoaderCircle aria-hidden="true" className="absolute animate-spin" />
        </>
      ) : (
        children
      )}
    </ButtonPrimitive>
  )
}
