import { cleanup, render, screen } from '@testing-library/react'
import { afterEach, describe, expect, it } from 'vitest'

import type { ReportMetricSet } from '../../../api/types'

import { ScorecardGrid } from './ScorecardGrid'

afterEach(() => cleanup())

function fixtureTotals(): ReportMetricSet {
  return {
    delivery: { done: 3, byPriority: { urgent: 0, high: 1, normal: 2 } },
    speed: {
      cycleTime: { sampleSize: 3, medianSeconds: 248400, p85Seconds: null, lowSample: true },
      leadTime: { sampleSize: 3, medianSeconds: 176400, p85Seconds: null, lowSample: true },
      reviewWait: { sampleSize: 3, medianSeconds: 75600, p85Seconds: null, lowSample: true },
    },
    reliability: { onTime: 1, late: 1, noDueDate: 1, onTimeRate: { value: 0.5, denominator: 2, lowSample: true }, overdueMarked: 1 },
    quality: { submissionsDecided: 5, sentBack: 1, reworkRate: { value: 0.2, denominator: 5, lowSample: false }, reopenedAfterDone: 1 },
    load: { avgInProgress: 1.71, capacityUtilization: 0.5694 },
    attribution: { takeOvers: 0, takenFrom: 1 },
  }
}

describe('ScorecardGrid — Speed card review wait (R5b)', () => {
  it('renders the review-wait median and p85 from the fixture response, with its caption', () => {
    render(<ScorecardGrid totals={fixtureTotals()} teamMedian={null} inProgressLimit={3} smallSampleThreshold={5} />)

    expect(screen.getByText('Review wait — median / p85')).not.toBeNull()
    // medianSeconds 75600 = 21h -> formatDuration -> "21h"; p85 null -> em dash.
    expect(screen.getByText('21h / —')).not.toBeNull()
    expect(screen.getByText('Time in the final review pass — not counted against cycle time')).not.toBeNull()
  })

  it('still shows cycle and lead time alongside review wait', () => {
    render(<ScorecardGrid totals={fixtureTotals()} teamMedian={null} inProgressLimit={3} smallSampleThreshold={5} />)

    expect(screen.getByText('Cycle time — median / p85')).not.toBeNull()
    expect(screen.getByText('Lead time — median / p85')).not.toBeNull()
  })

  it('shows no colour-judgement words and no composite score', () => {
    render(<ScorecardGrid totals={fixtureTotals()} teamMedian={null} inProgressLimit={3} smallSampleThreshold={5} />)

    const text = document.body.textContent ?? ''
    expect(text).not.toMatch(/above|below/i)
  })
})
