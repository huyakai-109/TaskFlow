import { describe, expect, it } from 'vitest'

import type { AllowedTransition, TaskStatus, TaskSummary } from '../../api/types'
import { getTaskActionView } from './taskActions'

/** Builds the fields `getTaskActionView` reads, defaulting to an InProgress task with no special flags. */
function task(overrides: Partial<Pick<TaskSummary, 'allowedTransitions' | 'canTakeOver' | 'status'>>): Pick<
  TaskSummary,
  'allowedTransitions' | 'canTakeOver' | 'status'
> {
  return {
    allowedTransitions: [],
    canTakeOver: false,
    status: 'InProgress' as TaskStatus,
    ...overrides,
  }
}

const moveTo = (status: TaskStatus, requiresTakeover = false): AllowedTransition => ({
  status,
  operation: 'Move',
  requiresReason: false,
  requiresTakeover,
})

const reopenEdge: AllowedTransition = { status: 'Review', operation: 'Reopen', requiresReason: true, requiresTakeover: false }

describe('getTaskActionView', () => {
  it('Member, holder: InProgress task assigned to the caller offers Submit, no take over', () => {
    const view = getTaskActionView(task({ allowedTransitions: [moveTo('Review')], canTakeOver: false, status: 'InProgress' }))

    expect(view.moves).toEqual([moveTo('Review')])
    expect(view.showTakeOver).toBe(false)
  })

  it('Member, non-holder: InProgress task assigned to someone else offers no moves, but take over if canTakeOver', () => {
    const view = getTaskActionView(task({ allowedTransitions: [], canTakeOver: true, status: 'InProgress' }))

    expect(view.moves).toEqual([])
    expect(view.showTakeOver).toBe(true)
  })

  it('unassigned Todo task: requiresTakeover move is still a move, plus canTakeOver separately drives the Take over button', () => {
    const view = getTaskActionView(task({ allowedTransitions: [moveTo('InProgress', true)], canTakeOver: true, status: 'Todo' }))

    expect(view.moves).toEqual([moveTo('InProgress', true)])
    expect(view.showTakeOver).toBe(true)
  })

  it('Manager/Admin, non-holder, no bypass: holder-only means no Move entries even for Manager/Admin — only Take over', () => {
    const view = getTaskActionView(task({ allowedTransitions: [], canTakeOver: true, status: 'Todo' }))

    expect(view.moves).toEqual([])
    expect(view.showTakeOver).toBe(true)
  })

  it('Manager/Admin: Done task with the reopen edge available', () => {
    const view = getTaskActionView(task({ allowedTransitions: [reopenEdge], canTakeOver: false, status: 'Done' }))

    expect(view.reopen).toEqual(reopenEdge)
    expect(view.moves).toEqual([])
  })

  it('never shows Take over in Review, even if canTakeOver were somehow true (defensive mirror of the server lock)', () => {
    const view = getTaskActionView(task({ allowedTransitions: [], canTakeOver: true, status: 'Review' }))

    expect(view.showTakeOver).toBe(false)
  })

  it('never shows Take over in Done, even if canTakeOver were somehow true', () => {
    const view = getTaskActionView(task({ allowedTransitions: [], canTakeOver: true, status: 'Done' }))

    expect(view.showTakeOver).toBe(false)
  })

  it('canTakeOver false (e.g. caller already the assignee) never shows Take over, regardless of status', () => {
    const view = getTaskActionView(task({ allowedTransitions: [moveTo('Review')], canTakeOver: false, status: 'InProgress' }))

    expect(view.showTakeOver).toBe(false)
  })
})
