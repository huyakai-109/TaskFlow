import { useNavigate } from 'react-router-dom'

import type { ReportTeam } from '../../../api/types'
import { userLabel } from '../../../shared/lib/userLabel'

import { summarizeHeatmap } from '../utils/chartSummary'
import { formatDateSafe } from '../utils/safeDate'

interface WorkloadHeatmapProps {
  heatmap: ReportTeam['heatmap']
  members: ReportTeam['members']
}

const STEPS = [10, 30, 50, 70, 90]

function stepFor(value: number, max: number): number {
  const ratio = max === 0 ? 0 : Math.min(1, value / max)
  const index = Math.min(STEPS.length - 1, Math.floor(ratio * STEPS.length))
  return STEPS[index]
}

const weekFormatter = new Intl.DateTimeFormat('en', { day: 'numeric', month: 'short' })

/**
 * §11.2 chart (d) / §11.4 — the heatmap *is* the `<table>` (no separate
 * "Show table" toggle: there's nothing else to reveal). Horizontal scroll is
 * contained to this component's own region (`role="region"`,
 * `aria-label`, `tabIndex=0`) — the only horizontal scroll on the page at
 * 390px (§11.2 point 6) — with the first column (names) sticky.
 *
 * §D.2 (2026-11-22 brief) — each cell is an integer ticket count
 * (`ticketsWorked`, R8), never a time-weighted average; the colour scale is
 * relative to the highest count on screen, not a configured limit (there is
 * nothing left to divide by one).
 */
export function WorkloadHeatmap({ heatmap, members }: WorkloadHeatmapProps) {
  const navigate = useNavigate()
  const summary = summarizeHeatmap(members.length, heatmap.weeks.length)
  const maxValue = Math.max(0, ...heatmap.rows.flatMap((row) => row.ticketsWorked.filter((v): v is number => v !== null)))

  return (
    <figure className="m-0 flex flex-col gap-[var(--space-3)] rounded-card-lg border border-border bg-surface p-[var(--space-4)]">
      <figcaption className="flex flex-col gap-[2px]">
        <p className="m-0 text-[15px] font-semibold text-text">Workload heatmap</p>
        <p className="m-0 text-[length:var(--font-size-sm)] text-text-2">{summary}</p>
      </figcaption>

      <div
        role="region"
        aria-label="Workload heatmap"
        tabIndex={0}
        className="overflow-x-auto rounded-md border border-border"
      >
        <table className="w-full min-w-[480px] border-collapse text-[length:var(--font-size-sm)]">
          <caption className="sr-only">Tickets worked per member per week</caption>
          <thead>
            <tr>
              <th scope="col" className="sticky left-0 z-[1] bg-surface py-[var(--space-1)] px-[var(--space-2)] text-left text-text-muted">
                Member
              </th>
              {heatmap.weeks.map((week) => (
                <th key={week.start} scope="col" className="py-[var(--space-1)] px-[var(--space-2)] text-text-muted">
                  {formatDateSafe(weekFormatter, week.start)}
                  {week.isPartial && '*'}
                </th>
              ))}
            </tr>
          </thead>
          <tbody>
            {heatmap.rows.map((row) => {
              const member = members.find((m) => m.user.id === row.userId)
              const name = member === undefined ? `User ${row.userId}` : userLabel(member.user)

              return (
                <tr key={row.userId}>
                  <th scope="row" className="sticky left-0 z-[1] bg-surface py-[var(--space-1)] px-[var(--space-2)] text-left font-normal">
                    <button
                      type="button"
                      className="cursor-pointer border-0 bg-transparent p-0 text-accent-ink underline"
                      onClick={() => navigate(`/reports/members/${row.userId}`)}
                    >
                      {name}
                    </button>
                  </th>
                  {row.ticketsWorked.map((value, index) => {
                    const week = heatmap.weeks[index]

                    return (
                      <td key={week.start} className="p-[var(--space-1)] text-center">
                        <button
                          type="button"
                          disabled={value === null}
                          aria-label={
                            value === null
                              ? `${name}, week of ${formatDateSafe(weekFormatter, week.start)}: no data`
                              : `${name}, week of ${formatDateSafe(weekFormatter, week.start)}: ${value} ticket${value === 1 ? '' : 's'} worked`
                          }
                          className="flex h-8 w-14 items-center justify-center rounded-sm text-[length:var(--font-size-xs)] font-medium text-text disabled:cursor-default"
                          style={
                            value === null
                              ? { backgroundColor: 'var(--color-column)' }
                              : { backgroundColor: `color-mix(in oklab, var(--color-accent) ${stepFor(value, maxValue)}%, var(--color-surface))` }
                          }
                        >
                          {value === null ? '—' : value}
                        </button>
                      </td>
                    )
                  })}
                </tr>
              )
            })}
          </tbody>
        </table>
      </div>
    </figure>
  )
}
