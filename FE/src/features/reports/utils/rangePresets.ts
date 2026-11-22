/**
 * `docs/design/SPEC.md` §11.2 range presets, computed from `GET /workflow`'s
 * `today` (never the browser clock, per F09/F10) and Monday-start weeks
 * (R13/B7). Dates are plain `yyyy-MM-dd` calendar strings throughout — there
 * is no time-of-day or timezone conversion here, only calendar arithmetic,
 * done through `Date.UTC` so no local-timezone DST rule can shift a day.
 */

export interface DateRange {
  from: string
  to: string
}

export type RangePresetKey = 'thisWeek' | 'lastWeek' | 'thisMonth' | 'lastMonth' | 'thisQuarter' | 'lastQuarter'

function parseIso(iso: string): Date {
  const [year, month, day] = iso.split('-').map(Number)
  return new Date(Date.UTC(year, month - 1, day))
}

function toIso(date: Date): string {
  return date.toISOString().slice(0, 10)
}

function addDays(date: Date, days: number): Date {
  const next = new Date(date)
  next.setUTCDate(next.getUTCDate() + days)
  return next
}

/** Days since Monday: JS `getUTCDay()` is 0 (Sun)..6 (Sat); this maps Monday to 0. */
function daysSinceMonday(date: Date): number {
  return (date.getUTCDay() + 6) % 7
}

function startOfMonth(date: Date): Date {
  return new Date(Date.UTC(date.getUTCFullYear(), date.getUTCMonth(), 1))
}

function endOfMonth(date: Date): Date {
  return new Date(Date.UTC(date.getUTCFullYear(), date.getUTCMonth() + 1, 0))
}

function quarterStartMonth(month: number): number {
  return Math.floor(month / 3) * 3
}

function startOfQuarter(date: Date): Date {
  return new Date(Date.UTC(date.getUTCFullYear(), quarterStartMonth(date.getUTCMonth()), 1))
}

function endOfQuarter(date: Date): Date {
  return new Date(Date.UTC(date.getUTCFullYear(), quarterStartMonth(date.getUTCMonth()) + 3, 0))
}

export function rangePresets(todayIso: string): Record<RangePresetKey, DateRange> {
  const today = parseIso(todayIso)
  const thisWeekStart = addDays(today, -daysSinceMonday(today))
  const lastWeekEnd = addDays(thisWeekStart, -1)
  const lastWeekStart = addDays(lastWeekEnd, -6)

  const thisMonthStart = startOfMonth(today)
  const lastMonthEnd = addDays(thisMonthStart, -1)
  const lastMonthStart = startOfMonth(lastMonthEnd)

  const thisQuarterStart = startOfQuarter(today)
  const lastQuarterEnd = addDays(thisQuarterStart, -1)
  const lastQuarterStart = startOfQuarter(lastQuarterEnd)

  return {
    thisWeek: { from: toIso(thisWeekStart), to: toIso(today) },
    lastWeek: { from: toIso(lastWeekStart), to: toIso(lastWeekEnd) },
    thisMonth: { from: toIso(thisMonthStart), to: toIso(today) },
    lastMonth: { from: toIso(lastMonthStart), to: toIso(endOfMonth(lastMonthEnd)) },
    thisQuarter: { from: toIso(thisQuarterStart), to: toIso(today) },
    lastQuarter: { from: toIso(lastQuarterStart), to: toIso(endOfQuarter(lastQuarterEnd)) },
  }
}

/** R14: `Day` granularity is only offered when the range is 92 days or fewer. */
export function isDayGranularityAllowed(range: DateRange): boolean {
  const days = Math.round((parseIso(range.to).getTime() - parseIso(range.from).getTime()) / 86_400_000) + 1
  return days <= 92
}
