import { useNavigate } from 'react-router-dom'

import type { ReportTeam } from '../../../api/types'
import { summarizeHeatmap } from '../utils/chartSummary'

interface WorkloadHeatmapProps {
  heatmap: ReportTeam['heatmap']
  members: ReportTeam['members']
  inProgressLimit: number
}

const STEPS = [10, 30, 50, 70, 90]

function stepFor(value: number, limit: number): number {
  const ratio = Math.min(1, Math.max(0, value / limit))
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
 */
export function WorkloadHeatmap({ heatmap, members, inProgressLimit }: WorkloadHeatmapProps) {
  const navigate = useNavigate()
  const summary = summarizeHeatmap(members.length, heatmap.weeks.length)

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
          <caption className="sr-only">Average tasks in progress per member per week</caption>
          <thead>
            <tr>
              <th scope="col" className="sticky left-0 z-[1] bg-surface py-[var(--space-1)] px-[var(--space-2)] text-left text-text-muted">
                Member
              </th>
              {heatmap.weeks.map((week) => (
                <th key={week.start} scope="col" className="py-[var(--space-1)] px-[var(--space-2)] text-text-muted">
                  {weekFormatter.format(new Date(`${week.start}T00:00:00Z`))}
                  {week.isPartial && '*'}
                </th>
              ))}
            </tr>
          </thead>
          <tbody>
            {heatmap.rows.map((row) => {
              const member = members.find((m) => m.user.id === row.userId)
              const name = member?.user.displayName ?? member?.user.userName ?? `User ${row.userId}`

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
                  {row.avgInProgress.map((value, index) => {
                    const week = heatmap.weeks[index]
                    const isFull = value !== null && value >= inProgressLimit

                    return (
                      <td key={week.start} className="p-[var(--space-1)] text-center">
                        <button
                          type="button"
                          disabled={value === null}
                          aria-label={
                            value === null
                              ? `${name}, week of ${weekFormatter.format(new Date(`${week.start}T00:00:00Z`))}: no data`
                              : `${name}, week of ${weekFormatter.format(new Date(`${week.start}T00:00:00Z`))}: ${value} in progress on average`
                          }
                          className="flex h-8 w-14 items-center justify-center rounded-sm text-[length:var(--font-size-xs)] font-medium text-text disabled:cursor-default"
                          style={
                            value === null
                              ? { backgroundColor: 'var(--color-column)' }
                              : {
                                  backgroundColor: `color-mix(in oklab, var(--color-accent) ${stepFor(value, inProgressLimit)}%, var(--color-surface))`,
                                  outline: isFull ? '2px solid var(--color-danger-border)' : undefined,
                                  outlineOffset: isFull ? '-2px' : undefined,
                                }
                          }
                        >
                          {value === null ? '—' : isFull ? 'Full' : value.toFixed(1)}
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
