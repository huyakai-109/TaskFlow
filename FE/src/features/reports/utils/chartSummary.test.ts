import { describe, expect, it } from 'vitest'

import { summarizeOnTimeVsLate, summarizeTicketsByPriority } from './chartSummary'

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

describe('summarizeTicketsByPriority', () => {
  it('reports counts and percentages per priority', () => {
    expect(summarizeTicketsByPriority({ urgent: 1, high: 2, normal: 1 })).toBe('1 urgent (25%), 2 high (50%), 1 normal (25%).')
  })

  it('reports no finished tasks without dividing by zero', () => {
    expect(summarizeTicketsByPriority({ urgent: 0, high: 0, normal: 0 })).toBe('No finished tasks in this period.')
  })
})
