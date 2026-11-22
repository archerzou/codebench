import { useId } from 'react'
import { Label } from '@/components/ui/label'
import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from '@/components/ui/select'

export type FilterOption = { value: string; label: string }

type FilterSelectProps = {
  label: string
  value: string
  options: readonly FilterOption[]
  onChange: (value: string) => void
}

/** A labelled Radix select (keyboard + ARIA from Radix, FE-10). */
export function FilterSelect({ label, value, options, onChange }: FilterSelectProps) {
  const id = useId()
  return (
    <div className="flex items-center gap-2">
      <Label htmlFor={id} className="text-sm font-normal text-ink-muted">
        {label}
      </Label>
      <Select value={value} onValueChange={onChange}>
        <SelectTrigger id={id} size="sm" className="min-w-32">
          <SelectValue />
        </SelectTrigger>
        <SelectContent>
          {options.map((option) => (
            <SelectItem key={option.value} value={option.value}>
              {option.label}
            </SelectItem>
          ))}
        </SelectContent>
      </Select>
    </div>
  )
}
