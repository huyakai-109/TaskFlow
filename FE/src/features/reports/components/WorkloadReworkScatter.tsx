import { useNavigate } from 'react-router-dom'
import { CartesianGrid, ResponsiveContainer, Scatter, ScatterChart, Tooltip, XAxis, YAxis, ZAxis } from 'recharts'

import type { ReportTeam } from '../../../api/types'
import { formatRate } from '../utils/formatRate'
import { summarizeScatter } from '../utils/chartSummary'
import { useReducedMotion } from '../../../shared/hooks/useReducedMotion'

import { ChartFigure } from './ChartFigure'
import { tooltipContentStyle, tooltipCursor, tooltipItemStyle, tooltipLabelStyle } from './chartTheme'

interface WorkloadReworkScatterProps {
  members: ReportTeam['members']
  height: number
}

const SMALL_SAMPLE_DENOMINATOR_THRESHOLD = 5

/** §11.2 chart (e), team view only — one point per member, x = tickets worked (integer, §D.3 — replaces average in progress), y = rework rate. */
export function WorkloadReworkScatter({ members, height }: WorkloadReworkScatterProps) {
  const navigate = useNavigate()
  const reducedMotion = useReducedMotion()

  const rows = members
    .filter((member) => member.totals.load !== null)
    .map((member) => ({
      userId: member.user.id,
      name: member.user.displayName ?? member.user.userName,
      x: member.totals.load!.ticketsWorked,
      y: member.totals.quality.reworkRate.value ?? 0,
      lowSample: member.totals.quality.reworkRate.denominator < SMALL_SAMPLE_DENOMINATOR_THRESHOLD,
    }))

  const summary = summarizeScatter(rows.length)

  return (
    <ChartFigure
      title="Workload vs rework"
      summary={summary}
      height={height}
      chart={
        <ResponsiveContainer width="100%" height="100%">
          <ScatterChart accessibilityLayer margin={{ left: 8, bottom: 8 }}>
            <CartesianGrid stroke="var(--color-border)" />
            <XAxis type="number" dataKey="x" name="Tickets worked" stroke="var(--color-text-muted)" fontSize={11} allowDecimals={false} />
            <YAxis
              type="number"
              dataKey="y"
              name="Rework rate"
              stroke="var(--color-text-muted)"
              fontSize={11}
              domain={[0, 1]}
              tickFormatter={(v: number) => formatRate(v)}
            />
            <ZAxis range={[80, 80]} />
            <Tooltip
              formatter={(value, name) => (name === 'Rework rate' ? formatRate(Number(value)) : (value as number))}
              contentStyle={tooltipContentStyle}
              labelStyle={tooltipLabelStyle}
              itemStyle={tooltipItemStyle}
              cursor={tooltipCursor}
            />
            <Scatter
              data={rows}
              isAnimationActive={!reducedMotion}
              shape={(props) => {
                const point = props as unknown as { cx?: number; cy?: number; payload?: (typeof rows)[number] }
                return (
                  <circle
                    cx={point.cx ?? 0}
                    cy={point.cy ?? 0}
                    r={6}
                    fill={point.payload?.lowSample === true ? 'transparent' : 'var(--color-accent)'}
                    stroke="var(--color-accent)"
                    strokeWidth={2}
                    onClick={() => point.payload && navigate(`/reports/members/${point.payload.userId}`)}
                    style={{ cursor: 'pointer' }}
                  />
                )
              }}
            />
          </ScatterChart>
        </ResponsiveContainer>
      }
      table={
        <table className="w-full min-w-[420px] border-collapse text-[length:var(--font-size-sm)]">
          <caption className="sr-only">Tickets worked vs rework rate per member</caption>
          <thead>
            <tr className="border-b border-border text-left text-text-muted">
              <th scope="col" className="py-[var(--space-1)]">Member</th>
              <th scope="col" className="py-[var(--space-1)]">Tickets worked</th>
              <th scope="col" className="py-[var(--space-1)]">Rework rate</th>
              <th scope="col" className="py-[var(--space-1)]">View</th>
            </tr>
          </thead>
          <tbody>
            {rows.map((row) => (
              <tr key={row.userId} className="border-b border-border">
                <th scope="row" className="py-[var(--space-1)] text-left font-normal">
                  {row.name}
                  {row.lowSample && <span className="text-text-muted"> (few data)</span>}
                </th>
                <td className="py-[var(--space-1)]">{row.x}</td>
                <td className="py-[var(--space-1)]">{formatRate(row.y)}</td>
                <td className="py-[var(--space-1)]">
                  <button
                    type="button"
                    className="cursor-pointer rounded-sm border border-border bg-surface px-[var(--space-1)] text-[length:var(--font-size-2xs)] text-text-muted hover:bg-accent-soft hover:text-accent-ink"
                    onClick={() => navigate(`/reports/members/${row.userId}`)}
                  >
                    View
                  </button>
                </td>
              </tr>
            ))}
          </tbody>
        </table>
      }
    />
  )
}
