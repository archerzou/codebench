import { ChevronLeft, ChevronRight } from 'lucide-react'
import { Button } from '@/components/Button'

type PaginationProps = {
  page: number
  totalPages: number
  hasPrevious: boolean
  hasNext: boolean
  onPage: (page: number) => void
}

export function Pagination({ page, totalPages, hasPrevious, hasNext, onPage }: PaginationProps) {
  return (
    <nav aria-label="Pagination" className="flex items-center justify-center gap-4 text-sm">
      <Button
        variant="ghost"
        size="sm"
        disabled={!hasPrevious}
        onClick={() => {
          onPage(page - 1)
        }}
      >
        <ChevronLeft aria-hidden="true" />
        Prev
      </Button>
      <span className="tabular text-ink-muted">
        Page {page} of {Math.max(totalPages, 1)}
      </span>
      <Button
        variant="ghost"
        size="sm"
        disabled={!hasNext}
        onClick={() => {
          onPage(page + 1)
        }}
      >
        Next
        <ChevronRight aria-hidden="true" />
      </Button>
    </nav>
  )
}
