import { cleanup, render, screen } from '@testing-library/react'
import { MemoryRouter } from 'react-router-dom'
import { afterEach, describe, expect, it, vi } from 'vitest'

import type { Board, TaskSummary } from '../../api/types'

import { MyTasksPage } from './MyTasksPage'

const { useBoardQuery, useUsersQuery, useAuthSession, useMoveTaskMutation } = vi.hoisted(() => ({
  useBoardQuery: vi.fn(),
  useUsersQuery: vi.fn(),
  useAuthSession: vi.fn(),
  useMoveTaskMutation: vi.fn(),
}))

vi.mock('../../shared/hooks/useBoardQuery', () => ({ useBoardQuery }))
vi.mock('../../shared/hooks/useUsersQuery', () => ({ useUsersQuery }))
vi.mock('../../shared/hooks/useAuthSession', () => ({ useAuthSession }))
vi.mock('../../shared/hooks/useMoveTaskMutation', () => ({ useMoveTaskMutation }))

function inProgressTask(id: number): TaskSummary {
  return {
    id,
    title: `Task ${id}`,
    status: 'InProgress',
    priority: 'Normal',
    assignee: { id: 1, userName: 'member1' },
    dueDate: null,
    isOverdue: false,
    completedAt: null,
    version: 'v1',
    allowedTransitions: [{ status: 'Review', operation: 'Move', requiresReason: false, requiresTakeover: false }],
    canTakeOver: false,
    canAssignOthers: false,
  }
}

function emptyBoard(items: TaskSummary[]): Board {
  return {
    columns: [
      { status: 'Backlog', items: [], pagination: null },
      { status: 'Todo', items: [], pagination: null },
      { status: 'InProgress', items, pagination: null },
      { status: 'Review', items: [], pagination: null },
      { status: 'Done', items: [], pagination: null },
    ],
  }
}

afterEach(() => {
  cleanup()
  vi.clearAllMocks()
})

/**
 * Bug fix (2026-11-22): the first "In progress" row's "Submit for review"
 * used to render filled/primary (the `primary={index === 0}` flag on
 * `TaskRow`) while the identical-label rows below it stayed secondary. The
 * flag is gone — every row sharing a label must render with the same classes.
 */
describe('MyTasksPage — row actions sharing a label render identically (no first-row emphasis)', () => {
  it('renders every "Submit for review" button with the same (secondary) classes', () => {
    useAuthSession.mockReturnValue({ userId: 1 })
    useUsersQuery.mockReturnValue({ data: [{ id: 1, userName: 'member1', displayName: 'Member One', role: 'Member', avatarUrl: null, isActive: true }] })
    useMoveTaskMutation.mockReturnValue({ mutate: vi.fn() })
    useBoardQuery.mockReturnValue({
      isPending: false,
      isError: false,
      isSuccess: true,
      data: emptyBoard([inProgressTask(1), inProgressTask(2), inProgressTask(3)]),
    })

    render(
      <MemoryRouter>
        <MyTasksPage />
      </MemoryRouter>,
    )

    const buttons = screen.getAllByRole('button', { name: 'Submit for review' })
    expect(buttons).toHaveLength(3)

    const classNames = buttons.map((button) => button.className)
    expect(new Set(classNames).size).toBe(1)
    // Secondary, not the filled/primary accent look the first row used to get.
    for (const className of classNames) {
      expect(className).not.toContain('bg-accent ')
      expect(className).toContain('border-border')
    }
  })
})
