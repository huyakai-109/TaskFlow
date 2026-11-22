/**
 * One-sentence `figcaption` text per chart, generated from the data itself
 * (`ChartFigure`, `docs/design/SPEC.md` §11.4) — never a static caption, so
 * it always reflects what's actually drawn.
 */

export interface OnTimeTotals {
  onTime: number
  late: number
  noDueDate: number
}

/** Matches §11.4's own example verbatim: "12 of 15 finished on time (80%); 2 late; 1 had no due date." */
export function summarizeOnTimeVsLate(totals: OnTimeTotals): string {
  const total = totals.onTime + totals.late + totals.noDueDate

  if (total === 0) {
    return 'No completions in this range.'
  }

  const rate = Math.round((totals.onTime / total) * 100)
  return `${totals.onTime} of ${total} finished on time (${rate}%); ${totals.late} late; ${totals.noDueDate} had no due date.`
}

/** §D.2 (2026-11-22 brief) — ticket counts, not a time-weighted average. */
export function summarizeHeatmap(memberCount: number, weekCount: number): string {
  return `Tickets worked per member per week — ${memberCount} ${memberCount === 1 ? 'member' : 'members'} across ${weekCount} ${weekCount === 1 ? 'week' : 'weeks'}.`
}

/** §D.3 — x = ticket count, not an average in-progress figure. */
export function summarizeScatter(memberCount: number): string {
  return `Tickets worked against rework rate for ${memberCount} ${memberCount === 1 ? 'member' : 'members'}.`
}

export interface ComparisonRow {
  label: string
  percent: number | null
}

/** §E — the "You vs team" chart, now percent differences instead of raw medians. */
export function summarizeTeamComparison(rows: ComparisonRow[]): string {
  const withValue = rows.filter((row) => row.percent !== null)

  if (withValue.length === 0) {
    return 'No team comparison available yet.'
  }

  return `Compared with the team average across ${withValue.length} ${withValue.length === 1 ? 'metric' : 'metrics'}.`
}

export interface PriorityTotals {
  urgent: number
  high: number
  normal: number
}

/** §F.2 — "Tickets by priority". */
export function summarizeTicketsByPriority(totals: PriorityTotals): string {
  const total = totals.urgent + totals.high + totals.normal

  if (total === 0) {
    return 'No finished tasks in this period.'
  }

  const pct = (n: number) => Math.round((n / total) * 100)
  return `${totals.urgent} urgent (${pct(totals.urgent)}%), ${totals.high} high (${pct(totals.high)}%), ${totals.normal} normal (${pct(totals.normal)}%).`
}
