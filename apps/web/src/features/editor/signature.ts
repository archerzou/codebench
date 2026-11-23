/**
 * Parameter names from a C# method signature, e.g.
 * `public Task<List<string>> GetNames(ShopDb db, DateTime since)` → `['db', 'since']`.
 * Splits on top-level commas only (generics, arrays and tuples nest). Returns [] if there are no parentheses.
 */
export function parameterNames(signature: string): string[] {
  const open = signature.indexOf('(')
  const close = signature.lastIndexOf(')')
  if (open < 0 || close <= open) return []

  const inner = signature.slice(open + 1, close)
  const parts: string[] = []
  let depth = 0
  let current = ''
  for (const char of inner) {
    if (char === '<' || char === '[' || char === '(') depth++
    if (char === '>' || char === ']' || char === ')') depth--
    if (char === ',' && depth === 0) {
      parts.push(current)
      current = ''
    } else {
      current += char
    }
  }
  parts.push(current)

  return parts
    .map((part) => part.split('=')[0]?.trim() ?? '')
    .filter((part) => part !== '')
    .map((part) => part.split(/\s+/).at(-1) ?? part)
}

/** Compact JSON for examples: `[1, 2, 3]`, `{ "a": 1 }`, `"text"`. */
export function formatValue(value: unknown): string {
  if (Array.isArray(value)) {
    return `[${value.map(formatValue).join(', ')}]`
  }
  if (value !== null && typeof value === 'object') {
    const entries = Object.entries(value).map(([key, inner]) => `${JSON.stringify(key)}: ${formatValue(inner)}`)
    return entries.length === 0 ? '{}' : `{ ${entries.join(', ')} }`
  }
  return value === undefined ? 'undefined' : JSON.stringify(value)
}
