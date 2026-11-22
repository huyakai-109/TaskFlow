import { describe, expect, it } from 'vitest'

import { formatDuration } from './formatDuration'

describe('formatDuration', () => {
  it('renders a whole-second count under a minute', () => {
    expect(formatDuration(0)).toBe('0 s')
    expect(formatDuration(59)).toBe('59 s')
  })

  it('renders exactly one hour as "1h"', () => {
    expect(formatDuration(3600)).toBe('1h')
  })

  it('renders two days four hours as "2d 4h"', () => {
    expect(formatDuration(2 * 86400 + 4 * 3600)).toBe('2d 4h')
  })

  it('renders null (no data, or p85 suppressed by the small-sample rule) as an em dash', () => {
    expect(formatDuration(null)).toBe('—')
  })
})
