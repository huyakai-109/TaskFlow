import { describe, expect, it } from 'vitest'

import { formatComparisonPercent, percentVsTeam } from './reportComparison'

describe('percentVsTeam', () => {
  it('above: member higher than the team average', () => {
    // team average = 20/5 = 4; member 5 -> (5-4)/4 = 25%
    expect(percentVsTeam(5, 20, 5)).toEqual({ percent: 25, direction: 'above' })
  })

  it('below: member lower than the team average', () => {
    // team average = 20/5 = 4; member 3 -> (3-4)/4 = -25%
    expect(percentVsTeam(3, 20, 5)).toEqual({ percent: -25, direction: 'below' })
  })

  it('equal: member matches the team average exactly', () => {
    expect(percentVsTeam(4, 20, 5)).toEqual({ percent: 0, direction: 'equal' })
  })

  it('a zero team average is never a division — shows null, not NaN/Infinity', () => {
    expect(percentVsTeam(0, 0, 5)).toEqual({ percent: null, direction: null })
  })

  it('no contributors at all — null, not a divide-by-zero', () => {
    expect(percentVsTeam(0, 0, 0)).toEqual({ percent: null, direction: null })
  })
})

describe('formatComparisonPercent', () => {
  it('always shows the sign', () => {
    expect(formatComparisonPercent(22)).toBe('+22%')
    expect(formatComparisonPercent(-8)).toBe('-8%')
    expect(formatComparisonPercent(0)).toBe('0%')
  })

  it('null renders as an em dash', () => {
    expect(formatComparisonPercent(null)).toBe('—')
  })
})
