import { formatDuration } from './formatDuration'

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

export interface CycleTrendTotals {
  medianSeconds: number | null
  p85Seconds: number | null
  bucketCount: number
}

export function summarizeCycleTimeTrend(totals: CycleTrendTotals): string {
  return `Cycle time across ${totals.bucketCount} ${totals.bucketCount === 1 ? 'period' : 'periods'}: median ${formatDuration(totals.medianSeconds)}, p85 ${formatDuration(totals.p85Seconds)}.`
}

export interface MemberVsTeamRow {
  label: string
  memberValue: number | null
  teamMedian: number | null
}

export function summarizeMemberVsTeam(rows: MemberVsTeamRow[]): string {
  const withBoth = rows.filter((row) => row.memberValue !== null && row.teamMedian !== null)

  if (withBoth.length === 0) {
    return 'No team median available to compare against yet.'
  }

  return `Compared against the team median across ${withBoth.length} ${withBoth.length === 1 ? 'metric' : 'metrics'}.`
}

export function summarizeHeatmap(memberCount: number, weekCount: number): string {
  return `Average tasks in progress for ${memberCount} ${memberCount === 1 ? 'member' : 'members'} across ${weekCount} ${weekCount === 1 ? 'week' : 'weeks'}.`
}

export function summarizeScatter(memberCount: number): string {
  return `Average in-progress load against rework rate for ${memberCount} ${memberCount === 1 ? 'member' : 'members'}.`
}
