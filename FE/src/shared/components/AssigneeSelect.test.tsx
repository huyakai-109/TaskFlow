import { cleanup, render, screen } from '@testing-library/react'
import { afterEach, describe, expect, it, vi } from 'vitest'

import { AssigneeSelect } from './AssigneeSelect'

const { useUsersQuery } = vi.hoisted(() => ({ useUsersQuery: vi.fn() }))

vi.mock('../hooks/useUsersQuery', () => ({ useUsersQuery }))

afterEach(() => {
  cleanup()
  vi.clearAllMocks()
})

describe('AssigneeSelect — display name over userName (bug fix 2026-11-22)', () => {
  it('renders the display name when the roster entry has one', () => {
    useUsersQuery.mockReturnValue({
      data: [{ id: 1, userName: 'member1', displayName: 'Lan', role: 'Member', avatarUrl: null, isActive: true }],
      isPending: false,
      isError: false,
    })

    render(<AssigneeSelect id="assignee" value={null} onChange={() => {}} />)

    expect(screen.getByRole('option', { name: 'Lan' })).not.toBeNull()
  })

  it('falls back to userName when the roster entry has no display name', () => {
    useUsersQuery.mockReturnValue({
      data: [{ id: 1, userName: 'member1', displayName: null, role: 'Member', avatarUrl: null, isActive: true }],
      isPending: false,
      isError: false,
    })

    render(<AssigneeSelect id="assignee" value={null} onChange={() => {}} />)

    expect(screen.getByRole('option', { name: 'member1' })).not.toBeNull()
  })
})
