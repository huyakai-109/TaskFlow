/**
 * `Rate.value` is 0..1 to 4 dp (f09 contract). Rendered as a whole-number
 * percentage, rounded to the nearest percent — `null` (zero denominator)
 * renders as an em dash, never "0%" or "NaN%".
 */
export function formatRate(value: number | null): string {
  if (value === null) {
    return '—'
  }

  return `${Math.round(value * 100)}%`
}
