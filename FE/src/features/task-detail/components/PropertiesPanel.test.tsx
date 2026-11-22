import { cleanup, render, screen } from '@testing-library/react'
import { afterEach, describe, expect, it, vi } from 'vitest'

import type { TaskDetail } from '../../../api/types'
import { clearSession, setSession } from '../../../shared/lib/authSession'

import { PropertiesPanel } from './PropertiesPanel'

const { useUsersQuery } = vi.hoisted(() => ({ useUsersQuery: vi.fn() }))

vi.mock('../../../shared/hooks/useUsersQuery', () => ({ useUsersQuery }))

afterEach(() => {
  cleanup()
  vi.clearAllMocks()
  clearSession()
})

function task(): TaskDetail {
  return {
    id: 1,
    title: 'Task 1',
    status: 'Todo',
    priority: 'Normal',
    assignee: null,
    dueDate: null,
    isOverdue: false,
    completedAt: null,
    version: 'v1',
    allowedTransitions: [],
    canTakeOver: false,
    canAssignOthers: false,
    description: null,
    createdAt: '2026-11-01T00:00:00Z',
    createdBy: 7,
    updatedAt: '2026-11-01T00:00:00Z',
  }
}

const noop = () => {}

describe('PropertiesPanel — "Created by" shows the roster display name (bug fix 2026-11-22)', () => {
  it('renders the display name when the creator has one', () => {
    setSession([])
    useUsersQuery.mockReturnValue({
      data: [{ id: 7, userName: 'member1', displayName: 'Lan', role: 'Member', avatarUrl: null, isActive: true }],
    })

    render(
      <PropertiesPanel
        task={task()}
        onEditPriority={noop}
        isPriorityPending={false}
        priorityError={null}
        onEditDueDate={noop}
        isDueDatePending={false}
        dueDateError={null}
        onOpenAssigneePicker={noop}
        isAssigneePending={false}
      />,
    )

    expect(screen.getByText(/by Lan/)).not.toBeNull()
  })

  it('falls back to userName when the creator has no display name', () => {
    setSession([])
    useUsersQuery.mockReturnValue({
      data: [{ id: 7, userName: 'member1', displayName: null, role: 'Member', avatarUrl: null, isActive: true }],
    })

    render(
      <PropertiesPanel
        task={task()}
        onEditPriority={noop}
        isPriorityPending={false}
        priorityError={null}
        onEditDueDate={noop}
        isDueDatePending={false}
        dueDateError={null}
        onOpenAssigneePicker={noop}
        isAssigneePending={false}
      />,
    )

    expect(screen.getByText(/by member1/)).not.toBeNull()
  })
})
