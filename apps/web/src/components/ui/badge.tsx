import * as React from "react"
import { cva, type VariantProps } from "class-variance-authority"
import { cn } from "@/lib/utils"
import { Slot } from "radix-ui"

// Re-skinned to docs/design/components.md (FE-16): tone neutral | accent | pass | fail | warn.
// Status tones use the *-soft surface + the status ink (>= 5.59:1); always paired with a word (FE-10).
const badgeVariants = cva(
  "inline-flex w-fit shrink-0 items-center justify-center gap-1 overflow-hidden rounded-sm border px-1.5 py-0.5 text-xs font-medium whitespace-nowrap [&>svg]:pointer-events-none [&>svg]:size-3.5",
  {
    variants: {
      tone: {
        neutral: "border-line bg-surface-sunken text-ink",
        accent: "border-accent text-accent",
        pass: "border-transparent bg-pass-soft text-pass",
        fail: "border-transparent bg-fail-soft text-fail",
        warn: "border-transparent bg-warn-soft text-warn",
      },
    },
    defaultVariants: {
      tone: "neutral",
    },
  }
)

function Badge({
  className,
  tone = "neutral",
  asChild = false,
  ...props
}: React.ComponentProps<"span"> &
  VariantProps<typeof badgeVariants> & { asChild?: boolean }) {
  const Comp = asChild ? Slot.Root : "span"

  return (
    <Comp
      data-slot="badge"
      data-tone={tone}
      className={cn(badgeVariants({ tone }), className)}
      {...props}
    />
  )
}

export { Badge, badgeVariants }
