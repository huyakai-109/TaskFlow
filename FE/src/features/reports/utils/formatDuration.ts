/**
 * `docs/design/SPEC.md` §11.2 "format '2d 4h'" — whole-second durations
 * (`Distribution.medianSeconds`/`p85Seconds`, F09 R5) rendered as the two
 * largest non-zero units, never a raw second count. `null` (no data, or a
 * `p85` suppressed by the small-sample rule, R16) renders as an em dash.
 */
export function formatDuration(totalSeconds: number | null): string {
  if (totalSeconds === null) {
    return '—'
  }

  if (totalSeconds < 60) {
    return `${totalSeconds} s`
  }

  const days = Math.floor(totalSeconds / 86400)
  const hours = Math.floor((totalSeconds % 86400) / 3600)
  const minutes = Math.floor((totalSeconds % 3600) / 60)

  if (days > 0) {
    return hours > 0 ? `${days}d ${hours}h` : `${days}d`
  }

  if (hours > 0) {
    return minutes > 0 ? `${hours}h ${minutes}m` : `${hours}h`
  }

  return `${minutes} min`
}
