/**
 * §E (2026-11-22 brief) — "% vs team average" for a count metric (done,
 * ticketsWorked). The team reference is the team total divided by the
 * number of members who *contributed* to that metric in the period (R12's
 * `contributors` for `done`, the equivalent ticketsWorked-contributors
 * count for load) — never the full roster size, and never a rate or
 * duration (those keep their own scorecard line, no comparison).
 */

export type ComparisonDirection = 'above' | 'below' | 'equal'

export interface TeamComparison {
  /** Whole percent, `null` when there is nothing sound to compare against (no contributors, or a zero team average). */
  percent: number | null
  direction: ComparisonDirection | null
}

export function percentVsTeam(memberValue: number, teamTotal: number, contributors: number): TeamComparison {
  if (contributors === 0) {
    return { percent: null, direction: null }
  }

  const teamAverage = teamTotal / contributors

  if (teamAverage === 0) {
    return { percent: null, direction: null }
  }

  const percent = Math.round(((memberValue - teamAverage) / teamAverage) * 100)
  const direction: ComparisonDirection = percent > 0 ? 'above' : percent < 0 ? 'below' : 'equal'

  return { percent, direction }
}

/** `"+22%"` / `"-8%"` / `"0%"` — the sign is always shown, never implied by colour alone. */
export function formatComparisonPercent(percent: number | null): string {
  if (percent === null) {
    return '—'
  }

  if (percent === 0) {
    return '0%'
  }

  return percent > 0 ? `+${percent}%` : `${percent}%`
}
