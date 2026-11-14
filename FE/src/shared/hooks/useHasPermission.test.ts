import { act, renderHook } from '@testing-library/react'
import { afterEach, describe, expect, it } from 'vitest'

import { clearSession, setSession } from '../lib/authSession'
import { useHasPermission } from './useHasPermission'

afterEach(() => {
  clearSession()
})

describe('useHasPermission', () => {
  it('returns false when the session has no permissions yet', () => {
    const { result } = renderHook(() => useHasPermission('PlanTasks'))

    expect(result.current).toBe(false)
  })

  it('returns false for a permission the session does not carry (Member)', () => {
    act(() => setSession(['ViewTasks', 'CreateTasks', 'UpdateTasks', 'MoveTasks', 'AssignTasks']))

    const { result } = renderHook(() => useHasPermission('PlanTasks'))

    expect(result.current).toBe(false)
  })

  it('returns true for a permission the session carries (Manager/Admin)', () => {
    act(() => setSession(['ViewTasks', 'CreateTasks', 'UpdateTasks', 'MoveTasks', 'AssignTasks', 'PlanTasks', 'AssignAnyone']))

    const { result } = renderHook(() => useHasPermission('PlanTasks'))

    expect(result.current).toBe(true)
  })

  it('re-renders with the refreshed value once the session changes (stale-permission recovery)', () => {
    const { result } = renderHook(() => useHasPermission('PlanTasks'))
    expect(result.current).toBe(false)

    act(() => setSession(['PlanTasks']))

    expect(result.current).toBe(true)
  })
})
