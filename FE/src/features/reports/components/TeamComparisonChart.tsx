import { Bar, BarChart, CartesianGrid, Cell, ReferenceLine, ResponsiveContainer, Tooltip, XAxis, YAxis } from 'recharts'

import { formatComparisonPercent } from '../utils/reportComparison'
import type { TeamComparison } from '../utils/reportComparison'
import { summarizeTeamComparison } from '../utils/chartSummary'

import { ChartFigure } from './ChartFigure'
import { tooltipContentStyle, tooltipCursor, tooltipItemStyle, tooltipLabelStyle } from './chartTheme'

interface TeamComparisonChartProps {
  done: TeamComparison | null
  ticketsWorked: TeamComparison | null
  height: number
}

/**
 * §E (2026-11-22 brief) — replaces the old "You vs team median" chart with
 * percent differences against the team average, for the two count metrics
 * only (done, ticketsWorked — rates and durations keep their own scorecard
 * line, never a comparison here). Teal for above, muted red for below,
 * never colour alone (each bar's own label carries the sign and percent).
 */
export function TeamComparisonChart({ done, ticketsWorked, height }: TeamComparisonChartProps) {
  const rows = [
    { label: 'Finished tasks', percent: done?.percent ?? null },
    { label: 'Tickets worked', percent: ticketsWorked?.percent ?? null },
  ].filter((row) => row.percent !== null) as { label: string; percent: number }[]

  const summary = summarizeTeamComparison(rows.map((row) => ({ label: row.label, percent: row.percent })))

  if (rows.length === 0) {
    return (
      <div className="rounded-card-lg border border-border bg-surface p-[var(--space-4)] text-text-muted">
        No team comparison available yet.
      </div>
    )
  }

  return (
    <ChartFigure
      title="You vs team average"
      summary={summary}
      height={height}
      chart={
        <ResponsiveContainer width="100%" height="100%">
          <BarChart data={rows} layout="vertical" accessibilityLayer margin={{ left: 24 }}>
            <CartesianGrid stroke="var(--color-border)" horizontal={false} />
            <XAxis type="number" stroke="var(--color-text-muted)" fontSize={11} tickFormatter={(v: number) => `${v}%`} />
            <YAxis type="category" dataKey="label" stroke="var(--color-text-muted)" fontSize={11} width={110} />
            <ReferenceLine x={0} stroke="var(--color-border-strong)" />
            <Tooltip
              formatter={(value) => formatComparisonPercent(Number(value))}
              contentStyle={tooltipContentStyle}
              labelStyle={tooltipLabelStyle}
              itemStyle={tooltipItemStyle}
              cursor={tooltipCursor}
            />
            <Bar dataKey="percent" name="vs team average">
              {rows.map((row) => (
                <Cell key={row.label} fill={row.percent >= 0 ? 'var(--color-accent-ink)' : 'var(--color-danger)'} />
              ))}
            </Bar>
          </BarChart>
        </ResponsiveContainer>
      }
      table={
        <table className="w-full min-w-[280px] border-collapse text-[length:var(--font-size-sm)]">
          <caption className="sr-only">You vs team average, percent difference per metric</caption>
          <thead>
            <tr className="border-b border-border text-left text-text-muted">
              <th scope="col" className="py-[var(--space-1)]">Metric</th>
              <th scope="col" className="py-[var(--space-1)]">vs team average</th>
            </tr>
          </thead>
          <tbody>
            {rows.map((row) => (
              <tr key={row.label} className="border-b border-border">
                <th scope="row" className="py-[var(--space-1)] text-left font-normal">
                  {row.label}
                </th>
                <td className="py-[var(--space-1)]">{formatComparisonPercent(row.percent)}</td>
              </tr>
            ))}
          </tbody>
        </table>
      }
    />
  )
}
