import { describe, expect, it } from 'vitest'

import { summarizeDelivery, summarizeQuality, summarizeReliability, summarizeSpeed } from './scorecardSummary'

describe('summarizeDelivery', () => {
  it('"Finished 12 tasks"', () => {
    expect(summarizeDelivery(12)).toBe('Finished 12 tasks')
  })

  it('singular for exactly one', () => {
    expect(summarizeDelivery(1)).toBe('Finished 1 task')
  })

  it('"No finished tasks in this period" when done is 0', () => {
    expect(summarizeDelivery(0)).toBe('No finished tasks in this period')
  })
})

describe('summarizeSpeed', () => {
  it('"Typically finished in 2d; most within 5d"', () => {
    expect(summarizeSpeed({ sampleSize: 10, medianSeconds: 2 * 86400, p85Seconds: 5 * 86400, lowSample: false })).toBe(
      'Typically finished in 2d; most within 5d',
    )
  })

  it('omits the "most within" half when p85 is null', () => {
    expect(summarizeSpeed({ sampleSize: 3, medianSeconds: 2 * 86400, p85Seconds: null, lowSample: true })).toBe(
      'Typically finished in 2d (few data)',
    )
  })

  it('"No finished tasks in this period" when there is no sample', () => {
    expect(summarizeSpeed({ sampleSize: 0, medianSeconds: null, p85Seconds: null, lowSample: false })).toBe('No finished tasks in this period')
  })
})

describe('summarizeReliability', () => {
  it('"8 of 10 on time (80%)"', () => {
    expect(summarizeReliability(8, 2)).toBe('8 of 10 on time (80%)')
  })

  it('"No finished tasks in this period" when onTime + late is 0', () => {
    expect(summarizeReliability(0, 0)).toBe('No finished tasks in this period')
  })
})

describe('summarizeQuality', () => {
  it('"Worked on 4 tickets; 1 of 9 sent back (11%)"', () => {
    expect(summarizeQuality(4, 1, 9, false)).toBe('Worked on 4 tickets; 1 of 9 sent back (11%)')
  })

  it('appends "(few data)" for a low-sample rework rate', () => {
    expect(summarizeQuality(4, 1, 2, true)).toBe('Worked on 4 tickets; 1 of 2 sent back (50%) (few data)')
  })

  it('"No finished tasks in this period" when nothing happened at all', () => {
    expect(summarizeQuality(0, 0, 0, false)).toBe('No finished tasks in this period')
  })
})
