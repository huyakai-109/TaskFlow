import type { ReportBucket } from '../../../api/types'

const dayFormatter = new Intl.DateTimeFormat('en', { day: 'numeric', month: 'short' })

/**
 * Axis label for one bucket — `docs/design/SPEC.md` §11.6 "Partial bucket":
 * the label gets a trailing `*` when the bucket's measured window was
 * shortened by `dataAvailableSince` or `now` (`isPartial`), with the
 * footnote "* partial period" shown once under the chart.
 */
export function bucketLabel(bucket: Pick<ReportBucket, 'start' | 'end' | 'isPartial'>): string {
  const start = dayFormatter.format(new Date(`${bucket.start}T00:00:00Z`))
  const label = bucket.start === bucket.end ? start : `${start}–${dayFormatter.format(new Date(`${bucket.end}T00:00:00Z`))}`
  return bucket.isPartial ? `${label}*` : label
}
