import type { InfiniteData } from '@tanstack/react-query'
import { QueryClient, QueryClientProvider } from '@tanstack/react-query'
import { renderHook, waitFor } from '@testing-library/react'
import type { ReactNode } from 'react'
import { afterEach, describe, expect, it, vi } from 'vitest'

import { ApiError } from '../../../api/client'
import type { PaginationResultRes, TaskNotification } from '../../../api/types'
import { useMarkAllNotificationsReadMutation } from './useMarkAllNotificationsReadMutation'
import { NOTIFICATIONS_QUERY_KEY } from './useNotificationsQuery'
import { UNREAD_NOTIFICATION_COUNT_QUERY_KEY } from './useUnreadNotificationCountQuery'

const { markNotificationRead } = vi.hoisted(() => ({ markNotificationRead: vi.fn() }))

vi.mock('../../../api/notifications', () => ({ markNotificationRead }))

type NotificationsData = InfiniteData<PaginationResultRes<TaskNotification[]>>

function notification(id: number, isRead = false): TaskNotification {
  return { id, taskId: 1000 + id, event: 'ReturnedForRework', isRead, readAt: isRead ? '2026-11-01T00:00:00Z' : null, createdAt: '2026-11-01T00:00:00Z' }
}

/** Two loaded pages with a mix of read/unread rows on both, and more unread rows (ids 1-17) never loaded at all — the server's true total (23) stays above what these two pages account for. */
function twoPageCache(): NotificationsData {
  return {
    pageParams: [1, 2],
    pages: [
      {
        success: true,
        result: [notification(40), notification(39, true), notification(38)],
        pagination: { pageNumber: 1, pageSize: 20, totalPages: 2, totalItems: 23, currentCount: 3 },
      },
      {
        success: true,
        result: [notification(21), notification(20, true), notification(19)],
        pagination: { pageNumber: 2, pageSize: 20, totalPages: 2, totalItems: 23, currentCount: 3 },
      },
    ],
  }
}

function createWrapper(queryClient: QueryClient) {
  return function Wrapper({ children }: { children: ReactNode }) {
    return <QueryClientProvider client={queryClient}>{children}</QueryClientProvider>
  }
}

afterEach(() => {
  vi.clearAllMocks()
})

describe('useMarkAllNotificationsReadMutation — across already-loaded pages', () => {
  it('marks every loaded unread row on every page read, leaving the server-true remainder on the badge', async () => {
    const queryClient = new QueryClient({ defaultOptions: { mutations: { retry: false } } })
    queryClient.setQueryData<NotificationsData>(NOTIFICATIONS_QUERY_KEY, twoPageCache())
    // 23 unread server-side in total; only the 4 loaded-unread ids (40, 38, 21, 19) are passed in here —
    // `NotificationBell` never claims the 19 still-unloaded unread rows (ids outside these two pages) as read.
    queryClient.setQueryData<number>(UNREAD_NOTIFICATION_COUNT_QUERY_KEY, 23)
    markNotificationRead.mockResolvedValue(undefined)

    const { result } = renderHook(() => useMarkAllNotificationsReadMutation(), { wrapper: createWrapper(queryClient) })

    result.current.mutate([40, 38, 21, 19])

    await waitFor(() => expect(result.current.isSuccess).toBe(true))

    expect(markNotificationRead).toHaveBeenCalledTimes(4)
    expect(markNotificationRead).toHaveBeenCalledWith(40)
    expect(markNotificationRead).toHaveBeenCalledWith(19)

    const cache = queryClient.getQueryData<NotificationsData>(NOTIFICATIONS_QUERY_KEY)
    expect(cache?.pages[0]?.result?.every((n) => n.isRead)).toBe(true)
    expect(cache?.pages[1]?.result?.every((n) => n.isRead)).toBe(true)

    // Honest remainder: 23 total unread minus the 4 just marked, never claimed down to 0.
    expect(queryClient.getQueryData<number>(UNREAD_NOTIFICATION_COUNT_QUERY_KEY)).toBe(19)
  })

  it('rolls back every optimistically-marked row across both pages, and the badge, when the batch fails', async () => {
    const queryClient = new QueryClient({ defaultOptions: { mutations: { retry: false } } })
    queryClient.setQueryData<NotificationsData>(NOTIFICATIONS_QUERY_KEY, twoPageCache())
    queryClient.setQueryData<number>(UNREAD_NOTIFICATION_COUNT_QUERY_KEY, 23)
    markNotificationRead.mockRejectedValue(new ApiError(500, null, 'Server error'))
    const onRefused = vi.fn()

    const { result } = renderHook(() => useMarkAllNotificationsReadMutation({ onRefused }), { wrapper: createWrapper(queryClient) })

    result.current.mutate([40, 38, 21, 19])

    await waitFor(() => expect(onRefused).toHaveBeenCalledTimes(1))

    const cache = queryClient.getQueryData<NotificationsData>(NOTIFICATIONS_QUERY_KEY)
    // Back to their original read state on both pages -- including the rows that were already read before the batch.
    expect(cache?.pages[0]?.result?.map((n) => n.isRead)).toEqual([false, true, false])
    expect(cache?.pages[1]?.result?.map((n) => n.isRead)).toEqual([false, true, false])
    expect(queryClient.getQueryData<number>(UNREAD_NOTIFICATION_COUNT_QUERY_KEY)).toBe(23)
  })
})
