import { Bar, BarChart, CartesianGrid, Cell, ReferenceDot, ResponsiveContainer, Tooltip, XAxis, YAxis } from 'recharts'

import type { ReportMetricSet, ReportTeamMedian } from '../../../api/types'
import { formatDuration } from '../utils/formatDuration'
import { formatRate } from '../utils/formatRate'
import { summarizeMemberVsTeam } from '../utils/chartSummary'
import { useReducedMotion } from '../../../shared/hooks/useReducedMotion'
import type { MemberVsTeamRow } from '../utils/chartSummary'

import { ChartFigure } from './ChartFigure'

interface MemberVsTeamChartProps {
  totals: ReportMetricSet
  teamMedian: ReportTeamMedian | null
  height: number
}

function buildRows(totals: ReportMetricSet, teamMedian: ReportTeamMedian | null): (MemberVsTeamRow & { memberLabel: string; teamLabel: string })[] {
  return [
    {
      label: 'Cycle time (h)',
      memberValue: totals.speed.cycleTime.medianSeconds !== null ? totals.speed.cycleTime.medianSeconds / 3600 : null,
      teamMedian: teamMedian?.cycleTimeMedianSeconds != null ? teamMedian.cycleTimeMedianSeconds / 3600 : null,
      memberLabel: formatDuration(totals.speed.cycleTime.medianSeconds),
      teamLabel: teamMedian?.cycleTimeMedianSeconds != null ? formatDuration(teamMedian.cycleTimeMedianSeconds) : '—',
    },
    {
      label: 'On-time rate (%)',
      memberValue: totals.reliability.onTimeRate.value !== null ? totals.reliability.onTimeRate.value * 100 : null,
      teamMedian: teamMedian?.onTimeRate != null ? teamMedian.onTimeRate * 100 : null,
      memberLabel: formatRate(totals.reliability.onTimeRate.value),
      teamLabel: teamMedian?.onTimeRate != null ? formatRate(teamMedian.onTimeRate) : '—',
    },
    {
      label: 'Rework rate (%)',
      memberValue: totals.quality.reworkRate.value !== null ? totals.quality.reworkRate.value * 100 : null,
      teamMedian: teamMedian?.reworkRate != null ? teamMedian.reworkRate * 100 : null,
      memberLabel: formatRate(totals.quality.reworkRate.value),
      teamLabel: teamMedian?.reworkRate != null ? formatRate(teamMedian.reworkRate) : '—',
    },
    {
      label: 'Avg in progress',
      memberValue: totals.load?.avgInProgress ?? null,
      teamMedian: teamMedian?.avgInProgress ?? null,
      memberLabel: totals.load !== null ? totals.load.avgInProgress.toFixed(1) : '—',
      teamLabel: teamMedian?.avgInProgress != null ? teamMedian.avgInProgress.toFixed(1) : '—',
    },
  ]
}

/** §11.2 chart (c), member view only — horizontal paired bars, member bar + team-median tick, one row per metric, never per person. */
export function MemberVsTeamChart({ totals, teamMedian, height }: MemberVsTeamChartProps) {
  const reducedMotion = useReducedMotion()
  const rows = buildRows(totals, teamMedian)
  const summary = summarizeMemberVsTeam(rows)

  return (
    <ChartFigure
      title="You vs team median"
      summary={summary}
      height={height}
      chart={
        <ResponsiveContainer width="100%" height="100%">
          <BarChart data={rows} layout="vertical" accessibilityLayer margin={{ left: 24 }}>
            <CartesianGrid stroke="var(--color-border)" horizontal={false} />
            <XAxis type="number" stroke="var(--color-text-muted)" fontSize={11} />
            <YAxis type="category" dataKey="label" stroke="var(--color-text-muted)" fontSize={11} width={110} />
            <Tooltip />
            <Bar dataKey="memberValue" name="You" isAnimationActive={!reducedMotion}>
              {rows.map((row) => (
                <Cell key={row.label} fill="var(--color-accent)" />
              ))}
            </Bar>
            {rows.map((row) =>
              row.teamMedian !== null ? (
                <ReferenceDot key={row.label} x={row.teamMedian} y={row.label} r={5} fill="var(--color-text)" stroke="none" />
              ) : null,
            )}
          </BarChart>
        </ResponsiveContainer>
      }
      table={
        <table className="w-full min-w-[360px] border-collapse text-[length:var(--font-size-sm)]">
          <caption className="sr-only">You vs team median per metric</caption>
          <thead>
            <tr className="border-b border-border text-left text-text-muted">
              <th scope="col" className="py-[var(--space-1)]">Metric</th>
              <th scope="col" className="py-[var(--space-1)]">You</th>
              <th scope="col" className="py-[var(--space-1)]">Team median</th>
            </tr>
          </thead>
          <tbody>
            {rows.map((row) => (
              <tr key={row.label} className="border-b border-border">
                <th scope="row" className="py-[var(--space-1)] text-left font-normal">
                  {row.label}
                </th>
                <td className="py-[var(--space-1)]">{row.memberLabel}</td>
                <td className="py-[var(--space-1)]">{row.teamLabel}</td>
              </tr>
            ))}
          </tbody>
        </table>
      }
    />
  )
}
