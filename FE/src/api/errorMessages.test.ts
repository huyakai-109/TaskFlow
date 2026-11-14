import { describe, expect, it } from 'vitest'

import { describeErrorCode, FORBIDDEN_SENTINEL } from './errorMessages'

describe('describeErrorCode', () => {
  it('maps each of the 5 Revision R2 error codes to a specific message, never the generic fallback', () => {
    const codes = [
      'Task.Move.ActorNotAssignee',
      'Task.Assign.SelfOnly',
      'Task.Assign.Locked',
      'Task.Create.PlanningNotPermitted',
      'Task.Create.AssigneeNotPermitted',
    ]

    for (const code of codes) {
      const message = describeErrorCode(code)
      expect(message).not.toBe('Something went wrong. Try again.')
      expect(message.length).toBeGreaterThan(0)
    }
  })

  it('maps the client-assigned FORBIDDEN_SENTINEL (stale-permission 403) to a specific message', () => {
    expect(describeErrorCode(FORBIDDEN_SENTINEL)).toBe(
      "You don't have permission to change this. Your session is refreshing — try again in a moment.",
    )
  })

  it('prefers the server-templated message for a CapacityReached code', () => {
    expect(describeErrorCode('Task.Assign.CapacityReached', 'Alex is already at the 3-task limit.')).toBe(
      'Alex is already at the 3-task limit.',
    )
  })

  it('falls back to the fixed CapacityReached copy when the server sent no message', () => {
    expect(describeErrorCode('Task.Move.CapacityReached', null)).toBe('That person is already at the in-progress limit.')
  })

  it('falls back to the generic message for an unmapped or null code', () => {
    expect(describeErrorCode('Task.Something.Unmapped')).toBe('Something went wrong. Try again.')
    expect(describeErrorCode(null)).toBe('Something went wrong. Try again.')
  })
})
