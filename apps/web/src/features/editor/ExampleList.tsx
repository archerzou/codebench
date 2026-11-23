import { EyeOff } from 'lucide-react'
import type { VisibleTest } from '@/features/problems'
import { formatValue, parameterNames } from './signature'

type ExampleListProps = {
  tests: VisibleTest[]
  signature: string
  hiddenTestCount: number
}

function describeArgs(args: unknown, names: string[]): string {
  if (!Array.isArray(args)) return formatValue(args)
  return args.map((arg, index) => `${names[index] ?? `arg${String(index + 1)}`} = ${formatValue(arg)}`).join(', ')
}

function describeResult(test: VisibleTest): string {
  if (test.expectedException) return `throws ${test.expectedException}`
  return test.expected === undefined || test.expected === null || isReference(test.expected)
    ? 'same as the reference solution'
    : formatValue(test.expected)
}

/** `{ "$ref": "reference" }` — the harness computes the expected value from the reference solution. */
function isReference(value: unknown): boolean {
  return typeof value === 'object' && value !== null && '$ref' in value
}

/** Visible tests as examples. Hidden tests only as a count — never args or expected values (QY-04). */
export function ExampleList({ tests, signature, hiddenTestCount }: ExampleListProps) {
  const names = parameterNames(signature)
  return (
    <section aria-labelledby="examples-heading" className="flex flex-col gap-2">
      <h2 id="examples-heading" className="text-sm font-medium text-ink">
        Examples
      </h2>
      {tests.length > 0 && (
        <ul className="flex flex-col gap-2">
          {tests.map((test) => (
            <li key={test.name} className="rounded-md border border-line bg-surface-code px-3 py-2">
              <p className="mb-1 text-xs text-ink-muted">{test.name}</p>
              <p className="overflow-x-auto font-mono text-sm whitespace-pre text-ink">
                {describeArgs(test.args, names)}
                <span className="text-ink-muted"> → </span>
                {describeResult(test)}
              </p>
            </li>
          ))}
        </ul>
      )}
      {hiddenTestCount > 0 && (
        <p className="flex items-center gap-1.5 text-sm text-ink-muted">
          <EyeOff aria-hidden="true" className="size-4" />+ {hiddenTestCount} hidden {hiddenTestCount === 1 ? 'test' : 'tests'}
        </p>
      )}
    </section>
  )
}
