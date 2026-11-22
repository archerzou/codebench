import type { ComponentProps } from 'react'
import { Badge as BadgePrimitive } from '@/components/ui/badge'

type BadgeProps = ComponentProps<typeof BadgePrimitive>

/** `tone: neutral | accent | pass | fail | warn` — text (plus an optional icon), never colour alone (FE-10). */
export function Badge(props: BadgeProps) {
  return <BadgePrimitive {...props} />
}
