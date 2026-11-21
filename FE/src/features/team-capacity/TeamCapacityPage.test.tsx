import { cleanup, render, screen } from '@testing-library/react'
import { MemoryRouter } from 'react-router-dom'
import { afterEach, describe, expect, it, vi } from 'vitest'

import { TeamCapacityPage } from './TeamCapacityPage'

const { useTeamCapacity, useWorkflowQuery } = vi.hoisted(() => ({
  useTeamCapacity: vi.fn(),
  useWorkflowQuery: vi.fn(),
}))

vi.mock('../../shared/hooks/useTeamCapacity', async (importOriginal) => {
  const actual = await importOriginal<typeof import('../../shared/hooks/useTeamCapacity')>()
  return { ...actual, useTeamCapacity }
})
vi.mock('../../shared/hooks/useWorkflowQuery', () => ({ useWorkflowQuery }))

function baseCapacity() {
  return {
    isPending: false,
    isError: false,
    entries: [],
    fullCount: 0,
    almostFullCount: 0,
    mayBeIncomplete: false,
  }
}

afterEach(() => {
  cleanup()
  vi.clearAllMocks()
})

describe('TeamCapacityPage — "max N" subtitle (GET /workflow, not a hardcoded literal)', () => {
  it('shows the real configured limit once the workflow query resolves', () => {
    useTeamCapacity.mockReturnValue(baseCapacity())
    useWorkflowQuery.mockReturnValue({ isError: false, data: { maxInProgressPerAssignee: 3 } })

    render(
      <MemoryRouter>
        <TeamCapacityPage />
      </MemoryRouter>,
    )

    expect(screen.getByText(/In progress · max/).textContent).toBe('In progress · max 3')
  })

  it('reflects a different configured limit too, never a literal 3', () => {
    useTeamCapacity.mockReturnValue(baseCapacity())
    useWorkflowQuery.mockReturnValue({ isError: false, data: { maxInProgressPerAssignee: 5 } })

    render(
      <MemoryRouter>
        <TeamCapacityPage />
      </MemoryRouter>,
    )

    expect(screen.getByText(/In progress · max/).textContent).toBe('In progress · max 5')
  })

  it('shows a loading placeholder, never "undefined" or a blank, while the workflow query is pending', () => {
    useTeamCapacity.mockReturnValue(baseCapacity())
    useWorkflowQuery.mockReturnValue({ isError: false, data: undefined })

    render(
      <MemoryRouter>
        <TeamCapacityPage />
      </MemoryRouter>,
    )

    expect(screen.getByText(/In progress · max/).textContent).toBe('In progress · max …')
  })

  it('falls back to a dash if the workflow query fails, never a crash or "undefined"', () => {
    useTeamCapacity.mockReturnValue(baseCapacity())
    useWorkflowQuery.mockReturnValue({ isError: true, data: undefined })

    render(
      <MemoryRouter>
        <TeamCapacityPage />
      </MemoryRouter>,
    )

    expect(screen.getByText(/In progress · max/).textContent).toBe('In progress · max —')
  })
})
