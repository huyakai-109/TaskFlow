import { describe, expect, it } from 'vitest'

import { formatRate } from './formatRate'

describe('formatRate', () => {
  it('rounds to the nearest whole percent', () => {
    expect(formatRate(0.3333)).toBe('33%')
  })

  it('renders null (zero denominator) as an em dash', () => {
    expect(formatRate(null)).toBe('—')
  })
})
