import { describe, expect, it } from 'vitest'

import { detectFySelection, fiscalMonthsInOrder, fiscalYearOf, fiscalYearRange, listFiscalYears } from './fiscalYear'

describe('fiscalYearOf — FY starts July (FISCAL_YEAR_START_MONTH = 7), named by its ending year', () => {
  it('2026-06-30 (the day before the boundary) is still FY 2026', () => {
    expect(fiscalYearOf('2026-06-30')).toBe(2026)
  })

  it('2026-07-01 (the boundary itself) is FY 2027', () => {
    expect(fiscalYearOf('2026-07-01')).toBe(2027)
  })

  it('2027-06-30 is the last day of FY 2027', () => {
    expect(fiscalYearOf('2027-06-30')).toBe(2027)
  })
})

describe('fiscalYearRange', () => {
  it('"FY 2027" = 2026-07-01..2027-06-30', () => {
    expect(fiscalYearRange(2027)).toEqual({ from: '2026-07-01', to: '2027-06-30' })
  })
})

describe('listFiscalYears', () => {
  it('lists every FY from the one containing dataAvailableSince to the one containing today', () => {
    expect(listFiscalYears('2025-02-20', '2026-03-11')).toEqual([2025, 2026])
  })

  it('is a single year when both dates fall in the same FY', () => {
    expect(listFiscalYears('2026-08-01', '2027-01-01')).toEqual([2027])
  })
})

describe('fiscalMonthsInOrder', () => {
  it('lists FY 2027\'s 12 months in fiscal order, Jul 2026 .. Jun 2027', () => {
    const months = fiscalMonthsInOrder(2027)

    expect(months.map((m) => m.label)).toEqual([
      'Jul 2026', 'Aug 2026', 'Sep 2026', 'Oct 2026', 'Nov 2026', 'Dec 2026',
      'Jan 2027', 'Feb 2027', 'Mar 2027', 'Apr 2027', 'May 2027', 'Jun 2027',
    ])
    expect(months[0].range).toEqual({ from: '2026-07-01', to: '2026-07-31' })
    expect(months[11].range).toEqual({ from: '2027-06-01', to: '2027-06-30' })
  })
})

describe('detectFySelection — §B.8, fy/month are derived from from/to, never stored', () => {
  it('detects a range matching a whole fiscal year', () => {
    expect(detectFySelection({ from: '2026-07-01', to: '2027-06-30' })).toEqual({ kind: 'fy', fy: 2027 })
  })

  it('detects a range matching a whole fiscal month', () => {
    expect(detectFySelection({ from: '2026-07-01', to: '2026-07-31' })).toEqual({ kind: 'month', fy: 2027, month: 7, year: 2026 })
  })

  it('falls back to custom for any other range', () => {
    expect(detectFySelection({ from: '2026-07-05', to: '2026-07-20' })).toEqual({ kind: 'custom' })
  })
})
