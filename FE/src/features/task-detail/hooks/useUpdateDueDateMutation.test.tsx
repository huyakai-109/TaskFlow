import { QueryClient, QueryClientProvider } from '@tanstack/react-query'
import { renderHook, waitFor } from '@testing-library/react'
import type { ReactNode } from 'react'
import { afterEach, describe, expect, it, vi } from 'vitest'

import { ApiError } from '../../../api/client'
import { FORBIDDEN_SENTINEL } from '../../../api/errorMessages'
import { useUpdateDueDateMutation } from './useUpdateDueDateMutation'

const { updateTaskDueDate } = vi.hoisted(() => ({ updateTaskDueDate: vi.fn() }))
const { refreshAccessToken } = vi.hoisted(() => ({ refreshAccessToken: vi.fn() }))

vi.mock('../../../api/tasks', () => ({ updateTaskDueDate }))

vi.mock('../../../api/client', async (importOriginal) => {
  const actual = await importOriginal<typeof import('../../../api/client')>()
  return { ...actual, refreshAccessToken }
})

function createWrapper() {
  const queryClient = new QueryClient({ defaultOptions: { mutations: { retry: false } } })
  return function Wrapper({ children }: { children: ReactNode }) {
    return <QueryClientProvider client={queryClient}>{children}</QueryClientProvider>
  }
}

const variables = { taskId: 1, dueDate: '2026-12-01', version: 'v1' }

afterEach(() => {
  vi.clearAllMocks()
})

/**
 * `useUpdateDueDateMutation`'s 403 handling mirrors
 * `useUpdatePriorityMutation`'s — one bounded session refresh, never a retry
 * of the original request, and a clear code surfaced to the caller instead
 * of a swallowed 403.
 */
describe('useUpdateDueDateMutation — 403 handling', () => {
  it('on a 403, calls refreshAccessToken exactly once and never retries the original request', async () => {
    updateTaskDueDate.mockRejectedValueOnce(new ApiError(403, null, 'Forbidden'))
    refreshAccessToken.mockResolvedValueOnce(undefined)
    const onRefused = vi.fn()

    const { result } = renderHook(() => useUpdateDueDateMutation({ onRefused }), { wrapper: createWrapper() })
    result.current.mutate(variables)

    await waitFor(() => expect(onRefused).toHaveBeenCalledTimes(1))

    expect(updateTaskDueDate).toHaveBeenCalledTimes(1)
    expect(refreshAccessToken).toHaveBeenCalledTimes(1)
  })

  it('surfaces the FORBIDDEN_SENTINEL code to the caller rather than swallowing the 403', async () => {
    updateTaskDueDate.mockRejectedValueOnce(new ApiError(403, null, 'Forbidden'))
    refreshAccessToken.mockResolvedValueOnce(undefined)
    const onRefused = vi.fn()

    const { result } = renderHook(() => useUpdateDueDateMutation({ onRefused }), { wrapper: createWrapper() })
    result.current.mutate(variables)

    await waitFor(() => expect(onRefused).toHaveBeenCalledWith(FORBIDDEN_SENTINEL))
  })

  it('a second 403 on a later attempt triggers its own single refresh, never an unbounded loop', async () => {
    updateTaskDueDate.mockRejectedValue(new ApiError(403, null, 'Forbidden'))
    refreshAccessToken.mockResolvedValue(undefined)
    const onRefused = vi.fn()

    const { result } = renderHook(() => useUpdateDueDateMutation({ onRefused }), { wrapper: createWrapper() })

    result.current.mutate(variables)
    await waitFor(() => expect(onRefused).toHaveBeenCalledTimes(1))

    result.current.mutate(variables)
    await waitFor(() => expect(onRefused).toHaveBeenCalledTimes(2))

    // One refresh per failed attempt — not doubled by the second 403, not skipped either.
    expect(refreshAccessToken).toHaveBeenCalledTimes(2)
    expect(updateTaskDueDate).toHaveBeenCalledTimes(2)
  })

  it('a non-403 failure is surfaced by its own error code, with no refresh triggered', async () => {
    updateTaskDueDate.mockRejectedValueOnce(
      new ApiError(409, { success: false, errorCode: 'Task.UpdateDueDate.Conflict', error: 'Conflict', validates: null }, 'Conflict'),
    )
    const onRefused = vi.fn()

    const { result } = renderHook(() => useUpdateDueDateMutation({ onRefused }), { wrapper: createWrapper() })
    result.current.mutate(variables)

    await waitFor(() => expect(onRefused).toHaveBeenCalledWith('Task.UpdateDueDate.Conflict'))

    expect(refreshAccessToken).not.toHaveBeenCalled()
  })
})
