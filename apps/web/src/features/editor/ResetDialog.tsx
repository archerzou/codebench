import { RotateCcw } from 'lucide-react'
import { Button } from '@/components/Button'
import {
  Dialog,
  DialogClose,
  DialogContent,
  DialogDescription,
  DialogFooter,
  DialogHeader,
  DialogTitle,
  DialogTrigger,
} from '@/components/ui/dialog'

type ResetDialogProps = {
  onConfirm: () => void
  /** Where focus goes when the dialog closes (the editor, per the wireframe). */
  onClosed: () => void
}

/** "Reset" restores the starter code after a confirm; focus returns to the editor (FE-10). */
export function ResetDialog({ onConfirm, onClosed }: ResetDialogProps) {
  return (
    <Dialog>
      <DialogTrigger asChild>
        <Button variant="ghost" size="sm">
          <RotateCcw aria-hidden="true" />
          Reset
        </Button>
      </DialogTrigger>
      <DialogContent
        showCloseButton={false}
        onCloseAutoFocus={(event) => {
          event.preventDefault()
          onClosed()
        }}
      >
        <DialogHeader>
          <DialogTitle>Reset to the starter code?</DialogTitle>
          <DialogDescription>Your draft for this problem is replaced. This can't be undone.</DialogDescription>
        </DialogHeader>
        <DialogFooter>
          <DialogClose asChild>
            <Button variant="secondary">Keep my code</Button>
          </DialogClose>
          <DialogClose asChild>
            <Button variant="danger" onClick={onConfirm}>
              Reset
            </Button>
          </DialogClose>
        </DialogFooter>
      </DialogContent>
    </Dialog>
  )
}
