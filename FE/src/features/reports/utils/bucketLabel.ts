import type { ReportBucket } from '../../../api/types'
import { formatDateSafe } from './safeDate'

const dayFormatter = new Intl.DateTimeFormat('en', { day: 'numeric', month: 'short' })

/**
 * Axis label for one bucket — `docs/design/SPEC.md` §11.6 "Partial bucket":
 * the label gets a trailing `*` when the bucket's measured window was
 * shortened by `dataAvailableSince` or `now` (`isPartial`), with the
 * footnote "* partial period" shown once under the chart.
 */
export function bucketLabel(bucket: Pick<ReportBucket, 'start' | 'end' | 'isPartial'>): string {
  const start = formatDateSafe(dayFormatter, bucket.start)
  const label = bucket.start === bucket.end ? start : `${start}–${formatDateSafe(dayFormatter, bucket.end)}`
  return bucket.isPartial ? `${label}*` : label
}
