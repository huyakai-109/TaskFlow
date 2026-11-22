import { Bar, BarChart, CartesianGrid, ResponsiveContainer, Tooltip, XAxis, YAxis } from 'recharts'

import { PriorityIcon } from '../../../shared/components/PriorityIcon'
import { summarizeTicketsByPriority } from '../utils/chartSummary'
import { useReducedMotion } from '../../../shared/hooks/useReducedMotion'

import { ChartFigure } from './ChartFigure'
import { tooltipContentStyle, tooltipCursor, tooltipItemStyle, tooltipLabelStyle } from './chartTheme'

interface TicketsByPriorityChartProps {
  urgent: number
  high: number
  normal: number
  height: number
}

/**
 * §F.2 (2026-11-22 brief) — "Tickets by priority": counts and "% of total"
 * per priority. A single stacked bar (not a donut: recharts has no pie/donut
 * already in use elsewhere in this app, and a bar keeps the same accessible
 * table-fallback pattern as every other chart here) — tokens only, no
 * success-green (§10.18/§11.3's own rule extended to this new chart).
 */
export function TicketsByPriorityChart({ urgent, high, normal, height }: TicketsByPriorityChartProps) {
  const reducedMotion = useReducedMotion()
  const total = urgent + high + normal
  const pct = (n: number) => (total === 0 ? 0 : Math.round((n / total) * 100))
  const summary = summarizeTicketsByPriority({ urgent, high, normal })

  const rows = [{ label: 'Tickets', urgent, high, normal }]

  return (
    <ChartFigure
      title="Tickets by priority"
      summary={summary}
      height={height}
      chart={
        <ResponsiveContainer width="100%" height="100%">
          <BarChart data={rows} layout="vertical" accessibilityLayer margin={{ left: 8 }}>
            <CartesianGrid stroke="var(--color-border)" horizontal={false} />
            <XAxis type="number" stroke="var(--color-text-muted)" fontSize={11} allowDecimals={false} />
            <YAxis type="category" dataKey="label" hide />
            <Tooltip contentStyle={tooltipContentStyle} labelStyle={tooltipLabelStyle} itemStyle={tooltipItemStyle} cursor={tooltipCursor} />
            <Bar dataKey="urgent" name="Urgent" stackId="a" fill="var(--color-priority-urgent)" isAnimationActive={!reducedMotion} />
            <Bar dataKey="high" name="High" stackId="a" fill="var(--color-priority-high)" isAnimationActive={!reducedMotion} />
            <Bar dataKey="normal" name="Normal" stackId="a" fill="var(--color-priority-normal)" isAnimationActive={!reducedMotion} />
          </BarChart>
        </ResponsiveContainer>
      }
      table={
        <table className="w-full min-w-[320px] border-collapse text-[length:var(--font-size-sm)]">
          <caption className="sr-only">Tickets by priority, count and percent of total</caption>
          <thead>
            <tr className="border-b border-border text-left text-text-muted">
              <th scope="col" className="py-[var(--space-1)]">Priority</th>
              <th scope="col" className="py-[var(--space-1)]">Count</th>
              <th scope="col" className="py-[var(--space-1)]">% of total</th>
            </tr>
          </thead>
          <tbody>
            <tr className="border-b border-border">
              <th scope="row" className="flex items-center gap-[var(--space-1)] py-[var(--space-1)] text-left font-normal">
                <PriorityIcon priority="Urgent" /> Urgent
              </th>
              <td className="py-[var(--space-1)]">{urgent}</td>
              <td className="py-[var(--space-1)]">{pct(urgent)}%</td>
            </tr>
            <tr className="border-b border-border">
              <th scope="row" className="flex items-center gap-[var(--space-1)] py-[var(--space-1)] text-left font-normal">
                <PriorityIcon priority="High" /> High
              </th>
              <td className="py-[var(--space-1)]">{high}</td>
              <td className="py-[var(--space-1)]">{pct(high)}%</td>
            </tr>
            <tr className="border-b border-border">
              <th scope="row" className="flex items-center gap-[var(--space-1)] py-[var(--space-1)] text-left font-normal">
                <PriorityIcon priority="Normal" /> Normal
              </th>
              <td className="py-[var(--space-1)]">{normal}</td>
              <td className="py-[var(--space-1)]">{pct(normal)}%</td>
            </tr>
          </tbody>
        </table>
      }
    />
  )
}
