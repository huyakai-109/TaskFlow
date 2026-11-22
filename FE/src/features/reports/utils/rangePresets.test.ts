import { describe, expect, it } from 'vitest'

import { isDayGranularityAllowed, rangePresets } from './rangePresets'

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
