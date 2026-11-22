import { Bar, BarChart, CartesianGrid, ResponsiveContainer, Tooltip, XAxis, YAxis } from 'recharts'

import type { ReportBucket, ReportTaskMetric } from '../../../api/types'
import { bucketLabel } from '../utils/bucketLabel'
import { summarizeOnTimeVsLate } from '../utils/chartSummary'
import { useReducedMotion } from '../../../shared/hooks/useReducedMotion'

import { ChartFigure } from './ChartFigure'

interface OnTimeStackedBarChartProps {
  buckets: ReportBucket[]
  height: number
  onViewTasks: (bucketStart: string, bucketEnd: string, metric: ReportTaskMetric) => void
}

/** §11.2/§11.3 chart (a) — on-time vs late vs no-due-date, stacked, per bucket. */
export function OnTimeStackedBarChart({ buckets, height, onViewTasks }: OnTimeStackedBarChartProps) {
  const reducedMotion = useReducedMotion()

  const rows = buckets.map((bucket) => ({
    bucket,
    label: bucketLabel(bucket),
    onTime: bucket.hasData ? bucket.metrics.reliability.onTime : 0,
    late: bucket.hasData ? bucket.metrics.reliability.late : 0,
    noDueDate: bucket.hasData ? bucket.metrics.reliability.noDueDate : 0,
  }))

  const totals = rows.reduce(
    (acc, row) => ({ onTime: acc.onTime + row.onTime, late: acc.late + row.late, noDueDate: acc.noDueDate + row.noDueDate }),
    { onTime: 0, late: 0, noDueDate: 0 },
  )

  const summary = summarizeOnTimeVsLate(totals)

  return (
    <ChartFigure
      title="On-time vs late"
      summary={summary}
      height={height}
      chart={
        <ResponsiveContainer width="100%" height="100%">
          <BarChart data={rows} accessibilityLayer>
            <CartesianGrid stroke="var(--color-border)" vertical={false} />
            <XAxis dataKey="label" stroke="var(--color-text-muted)" fontSize={11} />
            <YAxis stroke="var(--color-text-muted)" fontSize={11} allowDecimals={false} />
            <Tooltip />
            <Bar dataKey="onTime" name="On time" stackId="a" fill="var(--color-accent)" isAnimationActive={!reducedMotion} />
            <Bar dataKey="late" name="Late" stackId="a" fill="var(--color-danger)" isAnimationActive={!reducedMotion} />
            <Bar
              dataKey="noDueDate"
              name="No due date"
              stackId="a"
              fill="var(--color-meter-empty)"
              stroke="var(--color-border-strong)"
              strokeWidth={1}
              isAnimationActive={!reducedMotion}
            />
          </BarChart>
        </ResponsiveContainer>
      }
      table={
        <table className="w-full min-w-[480px] border-collapse text-[length:var(--font-size-sm)]">
          <caption className="sr-only">On-time vs late per bucket</caption>
          <thead>
            <tr className="border-b border-border text-left text-text-muted">
              <th scope="col" className="py-[var(--space-1)]">Bucket</th>
              <th scope="col" className="py-[var(--space-1)]">On time</th>
              <th scope="col" className="py-[var(--space-1)]">Late</th>
              <th scope="col" className="py-[var(--space-1)]">No due date</th>
              <th scope="col" className="py-[var(--space-1)]">View</th>
            </tr>
          </thead>
          <tbody>
            {rows.map((row) => (
              <tr key={row.bucket.start} className="border-b border-border">
                <th scope="row" className="py-[var(--space-1)] text-left font-normal">
                  {row.label}
                  {!row.bucket.hasData && <span className="text-text-muted"> — no data</span>}
                </th>
                <td className="py-[var(--space-1)]">{row.bucket.hasData ? row.onTime : '—'}</td>
                <td className="py-[var(--space-1)]">{row.bucket.hasData ? row.late : '—'}</td>
                <td className="py-[var(--space-1)]">{row.bucket.hasData ? row.noDueDate : '—'}</td>
                <td className="flex flex-wrap gap-[var(--space-1)] py-[var(--space-1)]">
                  {row.bucket.hasData && (
                    <>
                      <button
                        type="button"
                        className="cursor-pointer rounded-sm border border-border bg-surface px-[var(--space-1)] text-[length:var(--font-size-2xs)] text-text-muted hover:bg-accent-soft hover:text-accent-ink"
                        onClick={() => onViewTasks(row.bucket.start, row.bucket.end, 'OnTime')}
                      >
                        On time
                      </button>
                      <button
                        type="button"
                        className="cursor-pointer rounded-sm border border-border bg-surface px-[var(--space-1)] text-[length:var(--font-size-2xs)] text-text-muted hover:bg-accent-soft hover:text-accent-ink"
                        onClick={() => onViewTasks(row.bucket.start, row.bucket.end, 'Late')}
                      >
                        Late
                      </button>
                    </>
                  )}
                </td>
              </tr>
            ))}
          </tbody>
        </table>
      }
    />
  )
}
