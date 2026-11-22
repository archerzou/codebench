/** Numbers first (brief, principle 1): tabular figures with a thin-space thousands separator, `1 000 µs`. */
const grouped = new Intl.NumberFormat('en-US', { maximumFractionDigits: 1 })

export function formatNumber(value: number): string {
  return grouped.format(value).replace(/,/g, ' ')
}

/** Bytes in binary units: `0 B`, `512 B`, `1 KB`, `4.1 KB`, `64 KB`, `256 MB`. */
export function formatBytes(bytes: number): string {
  if (bytes < 1024) {
    return `${formatNumber(bytes)} B`
  }
  const units = ['KB', 'MB', 'GB'] as const
  let value = bytes / 1024
  let unit = 0
  while (value >= 1024 && unit < units.length - 1) {
    value /= 1024
    unit++
  }
  return `${formatNumber(Math.round(value * 10) / 10)} ${units[unit] ?? 'GB'}`
}

export function formatMicros(micros: number): string {
  return `${formatNumber(micros)} µs`
}

export function formatMs(ms: number): string {
  return ms >= 1000 ? `${formatNumber(ms / 1000)} s` : `${formatNumber(ms)} ms`
}
