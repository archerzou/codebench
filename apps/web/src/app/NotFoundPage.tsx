import { useEffect } from 'react'
import { Compass } from 'lucide-react'
import { Link } from 'react-router-dom'
import { Button } from '@/components/Button'
import { EmptyState } from '@/components/EmptyState'

export function NotFoundPage() {
  useEffect(() => {
    document.title = 'Not found · CodeBench'
  }, [])

  return (
    <main id="main" className="mx-auto w-full max-w-3xl p-8">
      <EmptyState
        icon={Compass}
        title="There's no page at this address"
        body="Check the link, or go back to the problem catalog."
        action={
          <Button asChild variant="secondary" size="sm">
            <Link to="/">Back to problems</Link>
          </Button>
        }
      />
    </main>
  )
}
