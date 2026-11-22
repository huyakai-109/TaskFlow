import { describe, expect, it } from 'vitest'

import { autoGranularity, isDayGranularityAllowed, rangePresets } from './rangePresets'

const TODAY = '2026-03-11' // Wednesday

describe('rangePresets — fixed today = 2026-03-11 (Wed), Monday-start weeks (R13/B7)', () => {
  it('computes every preset against the real "today" from GET /workflow, never the browser clock', () => {
    const presets = rangePresets(TODAY)

    expect(presets.thisWeek).toEqual({ from: '2026-03-09', to: '2026-03-11' })
    expect(presets.lastWeek).toEqual({ from: '2026-03-02', to: '2026-03-08' })
    expect(presets.thisMonth).toEqual({ from: '2026-03-01', to: '2026-03-11' })
    expect(presets.lastMonth).toEqual({ from: '2026-02-01', to: '2026-02-28' })
    expect(presets.thisQuarter).toEqual({ from: '2026-01-01', to: '2026-03-11' })
    expect(presets.lastQuarter).toEqual({ from: '2025-10-01', to: '2025-12-31' })
  })
})

describe('isDayGranularityAllowed', () => {
  it('allows Day for a 92-day range', () => {
    expect(isDayGranularityAllowed({ from: '2026-01-01', to: '2026-04-02' })).toBe(true)
  })

  it('disables Day for a 93-day range', () => {
    expect(isDayGranularityAllowed({ from: '2026-01-01', to: '2026-04-03' })).toBe(false)
  })
})

describe('autoGranularity — thresholds at 31/32/92/93 days (§B.5)', () => {
  it('Day for a 31-day range (the inclusive boundary)', () => {
    expect(autoGranularity({ from: '2026-01-01', to: '2026-01-31' })).toBe('Day')
  })

  it('Week for a 32-day range (just over the Day boundary)', () => {
    expect(autoGranularity({ from: '2026-01-01', to: '2026-02-01' })).toBe('Week')
  })

  it('Week for a 92-day range (the inclusive boundary)', () => {
    expect(autoGranularity({ from: '2026-01-01', to: '2026-04-02' })).toBe('Week')
  })

  it('Month for a 93-day range (just over the Week boundary)', () => {
    expect(autoGranularity({ from: '2026-01-01', to: '2026-04-03' })).toBe('Month')
  })
})
