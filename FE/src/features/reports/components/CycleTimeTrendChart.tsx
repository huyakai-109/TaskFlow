import { CartesianGrid, Line, LineChart, ReferenceArea, ResponsiveContainer, Tooltip, XAxis, YAxis } from 'recharts'

import type { ReportBucket } from '../../../api/types'
import { bucketLabel } from '../utils/bucketLabel'
import { summarizeCycleTimeTrend } from '../utils/chartSummary'
import { formatDuration } from '../utils/formatDuration'
import { useReducedMotion } from '../../../shared/hooks/useReducedMotion'

import { ChartFigure } from './ChartFigure'
import { tooltipContentStyle, tooltipCursor, tooltipItemStyle, tooltipLabelStyle } from './chartTheme'
import { NO_DATA_HATCH_FILL, NoDataHatchPattern } from './noDataHatch'

interface CycleTimeTrendChartProps {
  buckets: ReportBucket[]
  height: number
}

/**
 * §11.2 chart (b) — median + p85 cycle time line. The review-wait footnote
 * (R5b) sits directly under this chart rather than becoming a sixth chart
 * type (ADR 0008 §7): review wait is scorecard-only.
 */
export function CycleTimeTrendChart({ buckets, height }: CycleTimeTrendChartProps) {
  const reducedMotion = useReducedMotion()

  const rows = buckets.map((bucket) => ({
    bucket,
    label: bucketLabel(bucket),
    medianHours: bucket.hasData && bucket.metrics.speed.cycleTime.medianSeconds !== null ? bucket.metrics.speed.cycleTime.medianSeconds / 3600 : null,
    p85Hours: bucket.hasData && bucket.metrics.speed.cycleTime.p85Seconds !== null ? bucket.metrics.speed.cycleTime.p85Seconds / 3600 : null,
  }))

  const withData = buckets.filter((bucket) => bucket.hasData)
  const lastMedian = [...withData].reverse().find((bucket) => bucket.metrics.speed.cycleTime.medianSeconds !== null)
  const lastP85 = [...withData].reverse().find((bucket) => bucket.metrics.speed.cycleTime.p85Seconds !== null)

  const summary = summarizeCycleTimeTrend({
    medianSeconds: lastMedian?.metrics.speed.cycleTime.medianSeconds ?? null,
    p85Seconds: lastP85?.metrics.speed.cycleTime.p85Seconds ?? null,
    bucketCount: buckets.length,
  })

  return (
    <div className="flex flex-col gap-[var(--space-1)]">
      <ChartFigure
        title="Cycle time trend"
        summary={summary}
        height={height}
        chart={
          <ResponsiveContainer width="100%" height="100%">
            <LineChart data={rows} accessibilityLayer>
              <NoDataHatchPattern />
              <CartesianGrid stroke="var(--color-border)" vertical={false} />
              <XAxis dataKey="label" stroke="var(--color-text-muted)" fontSize={11} />
              <YAxis stroke="var(--color-text-muted)" fontSize={11} unit="h" />
              <Tooltip
                formatter={(value) => `${Number(value).toFixed(1)}h`}
                contentStyle={tooltipContentStyle}
                labelStyle={tooltipLabelStyle}
                itemStyle={tooltipItemStyle}
                cursor={tooltipCursor}
              />
              {/* §11.6 "Partially before data" — a hatched band for each bucket with no data, instead of silently skipping it. */}
              {rows
                .filter((row) => !row.bucket.hasData)
                .map((row) => (
                  <ReferenceArea key={row.bucket.start} x1={row.label} x2={row.label} fill={NO_DATA_HATCH_FILL} fillOpacity={1} stroke="none" ifOverflow="visible" />
                ))}
              <Line
                type="monotone"
                dataKey="medianHours"
                name="Median"
                stroke="var(--color-accent)"
                strokeWidth={2}
                dot={{ r: 3 }}
                isAnimationActive={!reducedMotion}
                connectNulls
              />
              <Line
                type="monotone"
                dataKey="p85Hours"
                name="p85"
                stroke="var(--color-priority-high)"
                strokeWidth={2}
                strokeDasharray="4 3"
                dot={{ r: 3 }}
                isAnimationActive={!reducedMotion}
                connectNulls
              />
            </LineChart>
          </ResponsiveContainer>
        }
        table={
          <table className="w-full min-w-[420px] border-collapse text-[length:var(--font-size-sm)]">
            <caption className="sr-only">Cycle time median and p85 per bucket</caption>
            <thead>
              <tr className="border-b border-border text-left text-text-muted">
                <th scope="col" className="py-[var(--space-1)]">Bucket</th>
                <th scope="col" className="py-[var(--space-1)]">Median</th>
                <th scope="col" className="py-[var(--space-1)]">p85</th>
              </tr>
            </thead>
            <tbody>
              {rows.map((row) => (
                <tr key={row.bucket.start} className="border-b border-border">
                  <th scope="row" className="py-[var(--space-1)] text-left font-normal">
                    {row.label}
                  </th>
                  <td className="py-[var(--space-1)]">
                    {row.bucket.hasData ? formatDuration(row.bucket.metrics.speed.cycleTime.medianSeconds) : '—'}
                  </td>
                  <td className="py-[var(--space-1)]">
                    {row.bucket.hasData ? formatDuration(row.bucket.metrics.speed.cycleTime.p85Seconds) : '—'}
                  </td>
                </tr>
              ))}
            </tbody>
          </table>
        }
      />
      <p className="m-0 text-[length:var(--font-size-2xs)] text-text-muted">
        Cycle time runs start → Done and can include review wait — see Speed&rsquo;s review-wait figures above for the
        reviewer&rsquo;s share.
      </p>
      {buckets.some((bucket) => bucket.isPartial) && (
        <p className="m-0 text-[length:var(--font-size-2xs)] text-text-muted">* partial period</p>
      )}
    </div>
  )
}
