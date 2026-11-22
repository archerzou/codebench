import { Play, Send } from 'lucide-react'
import { Button } from '@/components/Button'
import { Kbd } from '@/components/Kbd'
import { Tooltip, TooltipContent, TooltipTrigger } from '@/components/ui/tooltip'

const isMac = typeof navigator !== 'undefined' && /Mac|iPhone|iPad/.test(navigator.userAgent)
const mod = isMac ? '⌘' : 'Ctrl'

/**
 * Run / Submit. Phase 1 has no grading pipeline, so both are disabled with an explanation (editor wireframe,
 * "Phase 1" state); Phase 2 replaces this with RunButton / SubmitButton from features/submissions.
 * Disabled buttons don't receive pointer events, so the tooltip hangs off a focusable wrapper.
 */
export function GradingActions() {
  return (
    <Tooltip>
      <TooltipTrigger asChild>
        {/* eslint-disable-next-line jsx-a11y/no-noninteractive-tabindex -- keyboard users must reach the explanation */}
        <span tabIndex={0} className="inline-flex items-center gap-2 rounded-md" aria-describedby="grading-note">
          <Button variant="secondary" size="sm" disabled>
            <Play aria-hidden="true" />
            Run
            <Kbd keys={[mod, 'Enter']} className="ml-1 hidden editor:inline-flex" />
          </Button>
          <Button variant="primary" size="sm" disabled>
            <Send aria-hidden="true" />
            Submit
          </Button>
        </span>
      </TooltipTrigger>
      <TooltipContent id="grading-note">Grading arrives in Phase 2</TooltipContent>
    </Tooltip>
  )
}
