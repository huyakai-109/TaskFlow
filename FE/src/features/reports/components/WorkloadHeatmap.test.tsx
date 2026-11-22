import { cleanup, render, screen } from '@testing-library/react'
import { MemoryRouter } from 'react-router-dom'
import { afterEach, describe, expect, it } from 'vitest'

import type { ReportTeam } from '../../../api/types'

import { WorkloadHeatmap } from './WorkloadHeatmap'

afterEach(() => cleanup())

function fixture(): { heatmap: ReportTeam['heatmap']; members: ReportTeam['members'] } {
  return {
    heatmap: {
      weeks: [{ start: '2026-03-02', end: '2026-03-08', isPartial: false }],
      rows: [{ userId: 1, ticketsWorked: [3] }],
    },
    members: [{ user: { id: 1, userName: 'member1', displayName: 'Lan', avatarUrl: null, isActive: true }, totals: {} as never }],
  }
}

describe('WorkloadHeatmap — integer ticket counts (§D.2), never a time-weighted average', () => {
  it('renders the integer count in the cell and its aria-label', () => {
    const { heatmap, members } = fixture()
    render(
      <MemoryRouter>
        <WorkloadHeatmap heatmap={heatmap} members={members} />
      </MemoryRouter>,
    )

    expect(screen.getByText('3')).not.toBeNull()
    expect(screen.getByLabelText('Lan, week of Mar 2: 3 tickets worked')).not.toBeNull()
  })

  it('the table caption describes tickets worked, not an average', () => {
    const { heatmap, members } = fixture()
    render(
      <MemoryRouter>
        <WorkloadHeatmap heatmap={heatmap} members={members} />
      </MemoryRouter>,
    )

    expect(screen.getByText('Tickets worked per member per week')).not.toBeNull()
  })
})
