import { describe, expect, it } from 'vitest'

import { summarizeOnTimeVsLate } from './chartSummary'

describe('summarizeOnTimeVsLate', () => {
  it('matches the SPEC §11.4 example sentence for a sample dataset', () => {
    expect(summarizeOnTimeVsLate({ onTime: 12, late: 2, noDueDate: 1 })).toBe(
      '12 of 15 finished on time (80%); 2 late; 1 had no due date.',
    )
  })

  it('reports no completions without dividing by zero', () => {
    expect(summarizeOnTimeVsLate({ onTime: 0, late: 0, noDueDate: 0 })).toBe('No completions in this range.')
  })
})
