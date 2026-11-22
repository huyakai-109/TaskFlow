import type { DateRange } from './rangePresets'

/**
 * The only place the fiscal year's start month lives (§B.2 of the
 * 2026-11-22 brief) — change this one constant to move the fiscal year
 * boundary; every helper below derives from it. `7` = July, so "FY 2027"
 * runs 2026-07-01..2027-06-30 (a fiscal year is named by its *ending*
 * year, the convention the brief asked for).
 */
export const FISCAL_YEAR_START_MONTH: number = 7

function pad(n: number): string {
  return String(n).padStart(2, '0')
}

function daysInMonth(year: number, month: number): number {
  return new Date(Date.UTC(year, month, 0)).getUTCDate()
}

function parseIso(iso: string): { year: number; month: number; day: number } {
  const [year, month, day] = iso.split('-').map(Number)
  return { year, month, day }
}

/** The fiscal year (its ending year) that contains this calendar date. */
export function fiscalYearOf(dateIso: string): number {
  const { year, month } = parseIso(dateIso)

  if (FISCAL_YEAR_START_MONTH === 1) {
    return year
  }

  return month >= FISCAL_YEAR_START_MONTH ? year + 1 : year
}

/** The full `from`..`to` span of one fiscal year. */
export function fiscalYearRange(fy: number): DateRange {
  const startYear = FISCAL_YEAR_START_MONTH === 1 ? fy : fy - 1
  const endMonth = FISCAL_YEAR_START_MONTH === 1 ? 12 : FISCAL_YEAR_START_MONTH - 1
  const endYear = fy

  return {
    from: `${startYear}-${pad(FISCAL_YEAR_START_MONTH)}-01`,
    to: `${endYear}-${pad(endMonth)}-${pad(daysInMonth(endYear, endMonth))}`,
  }
}

/** Every fiscal year from the one containing `earliestIso` up to the one containing `latestIso`, ascending. */
export function listFiscalYears(earliestIso: string, latestIso: string): number[] {
  const first = fiscalYearOf(earliestIso)
  const last = fiscalYearOf(latestIso)
  const years: number[] = []

  for (let fy = first; fy <= last; fy++) {
    years.push(fy)
  }

  return years
}

export interface FiscalMonth {
  /** Calendar month, 1-12. */
  month: number
  year: number
  label: string
  range: DateRange
}

const MONTH_NAMES = ['Jan', 'Feb', 'Mar', 'Apr', 'May', 'Jun', 'Jul', 'Aug', 'Sep', 'Oct', 'Nov', 'Dec']

/** The 12 months of one fiscal year, in fiscal order (starting at `FISCAL_YEAR_START_MONTH`, wrapping through December into the next calendar year). */
export function fiscalMonthsInOrder(fy: number): FiscalMonth[] {
  const months: FiscalMonth[] = []

  for (let offset = 0; offset < 12; offset++) {
    const month = ((FISCAL_YEAR_START_MONTH - 1 + offset) % 12) + 1
    const year = month >= FISCAL_YEAR_START_MONTH ? fy - 1 : fy
    const effectiveYear = FISCAL_YEAR_START_MONTH === 1 ? fy : year

    months.push({
      month,
      year: effectiveYear,
      label: `${MONTH_NAMES[month - 1]} ${effectiveYear}`,
      range: { from: `${effectiveYear}-${pad(month)}-01`, to: `${effectiveYear}-${pad(month)}-${pad(daysInMonth(effectiveYear, month))}` },
    })
  }

  return months
}

export type FySelection = { kind: 'fy'; fy: number } | { kind: 'month'; fy: number; month: number; year: number } | { kind: 'custom' }

/**
 * §B.8 — `fy`/`month` are never stored in the URL, only derived from
 * `from`/`to` on read. A range that matches neither a full fiscal year nor
 * a full fiscal month is `'custom'`.
 */
export function detectFySelection(range: DateRange): FySelection {
  const fy = fiscalYearOf(range.from)
  const fyRange = fiscalYearRange(fy)

  if (fyRange.from === range.from && fyRange.to === range.to) {
    return { kind: 'fy', fy }
  }

  for (const monthFy of [fy, fy + 1, fy - 1]) {
    const month = fiscalMonthsInOrder(monthFy).find((m) => m.range.from === range.from && m.range.to === range.to)

    if (month !== undefined) {
      return { kind: 'month', fy: monthFy, month: month.month, year: month.year }
    }
  }

  return { kind: 'custom' }
}
