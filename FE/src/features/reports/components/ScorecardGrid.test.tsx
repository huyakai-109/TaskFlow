import { cleanup, render, screen } from '@testing-library/react'
import { afterEach, describe, expect, it } from 'vitest'

import type { ReportMetricSet } from '../../../api/types'

import { ScorecardGrid } from './ScorecardGrid'

afterEach(() => cleanup())

function fixtureTotals(overrides: Partial<ReportMetricSet> = {}): ReportMetricSet {
  return {
    delivery: { done: 3, byPriority: { urgent: 0, high: 1, normal: 2 } },
    speed: {
      cycleTime: { sampleSize: 3, medianSeconds: 248400, p85Seconds: null, lowSample: true },
      leadTime: { sampleSize: 3, medianSeconds: 176400, p85Seconds: null, lowSample: true },
      reviewWait: { sampleSize: 3, medianSeconds: 75600, p85Seconds: null, lowSample: true },
    },
    reliability: { onTime: 1, late: 1, noDueDate: 1, onTimeRate: { value: 0.5, denominator: 2, lowSample: true }, overdueMarked: 1 },
    quality: { submissionsDecided: 5, sentBack: 1, reworkRate: { value: 0.2, denominator: 5, lowSample: false }, reopenedAfterDone: 1 },
    load: { ticketsWorked: 4 },
    attribution: { takeOvers: 0, takenFrom: 1 },
    ...overrides,
  }
}

describe('ScorecardGrid — Speed card review wait (R5b), now with its "cycle time can include review wait" note', () => {
  it('renders the review-wait Typical/Most within figures from the fixture response, with its caption', () => {
    render(<ScorecardGrid totals={fixtureTotals()} teamMedian={null} smallSampleThreshold={5} />)

    expect(screen.getByText('Review wait — Typical / Most within')).not.toBeNull()
    expect(screen.getByText('21h / —')).not.toBeNull()
    expect(screen.getByText(/not counted against cycle time\. Cycle time itself can include review wait\./)).not.toBeNull()
  })

  it('still shows cycle and lead time alongside review wait', () => {
    render(<ScorecardGrid totals={fixtureTotals()} teamMedian={null} smallSampleThreshold={5} />)

    expect(screen.getByText('Cycle time — Typical / Most within')).not.toBeNull()
    expect(screen.getByText('Lead time — Typical / Most within')).not.toBeNull()
  })

  it('shows no colour-judgement words ("above"/"below") and no composite score', () => {
    render(<ScorecardGrid totals={fixtureTotals()} teamMedian={null} smallSampleThreshold={5} />)

    const text = document.body.textContent ?? ''
    expect(text).not.toMatch(/above|below/i)
  })
})

describe('ScorecardGrid — ticket counts, not averages (§D)', () => {
  it('shows "tickets" as an integer, never an average-in-progress or capacity-utilisation figure', () => {
    render(<ScorecardGrid totals={fixtureTotals()} teamMedian={null} smallSampleThreshold={5} />)

    expect(screen.getByText('4')).not.toBeNull()
    expect(screen.queryByText(/average in progress/i)).toBeNull()
    expect(screen.queryByText(/capacity utilisation/i)).toBeNull()
  })
})

describe('ScorecardGrid — priority split hidden when all three are zero (§C.3)', () => {
  it('hides Urgent/High/Normal when every count is 0', () => {
    render(<ScorecardGrid totals={fixtureTotals({ delivery: { done: 0, byPriority: { urgent: 0, high: 0, normal: 0 } } })} teamMedian={null} smallSampleThreshold={5} />)

    expect(screen.queryByText(/Urgent/)).toBeNull()
    expect(screen.queryByText(/^High /)).toBeNull()
  })

  it('shows the breakdown when at least one priority is non-zero', () => {
    render(<ScorecardGrid totals={fixtureTotals()} teamMedian={null} smallSampleThreshold={5} />)

    expect(screen.getByText(/Urgent/)).not.toBeNull()
  })
})

describe('ScorecardGrid — "% vs team" comparison (§E)', () => {
  it('shows the comparison badge with its sign and direction when provided', () => {
    render(
      <ScorecardGrid
        totals={fixtureTotals()}
        teamMedian={null}
        smallSampleThreshold={5}
        teamComparison={{ done: { percent: 22, direction: 'above' }, ticketsWorked: { percent: -10, direction: 'below' } }}
      />,
    )

    expect(screen.getByText(/\+22% vs team/)).not.toBeNull()
    expect(screen.getByText(/-10% vs team/)).not.toBeNull()
  })

  it('shows nothing when teamComparison is null (team scope, or a Member without ViewReports)', () => {
    render(<ScorecardGrid totals={fixtureTotals()} teamMedian={null} smallSampleThreshold={5} teamComparison={null} />)

    expect(screen.queryByText(/vs team/)).toBeNull()
  })
})
