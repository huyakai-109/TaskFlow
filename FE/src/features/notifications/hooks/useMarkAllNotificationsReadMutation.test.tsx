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

const { markAllNotificationsRead } = vi.hoisted(() => ({ markAllNotificationsRead: vi.fn() }))

vi.mock('../../../api/notifications', () => ({ markAllNotificationsRead }))

type NotificationsData = InfiniteData<PaginationResultRes<TaskNotification[]>>

function notification(id: number, isRead = false): TaskNotification {
  return {
    id,
    taskId: 1000 + id,
    event: 'ReturnedForRework',
    isRead,
    readAt: isRead ? '2026-11-01T00:00:00Z' : null,
    createdAt: '2026-11-01T00:00:00Z',
    task: { id: 1000 + id, title: `Task ${id}` },
    actor: null,
  }
}

/** Two loaded pages with a mix of read/unread rows, plus server-side unread rows (ids 1-17) never loaded at all — `upToId` (the newest loaded id, 40) must cover all of them anyway (F06's "newest the user has seen" contract), not just what's in the cache. */
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

describe('useMarkAllNotificationsReadMutation — one bulk request', () => {
  it('calls the bulk endpoint exactly once with the newest loaded id, never one request per row', async () => {
    const queryClient = new QueryClient({ defaultOptions: { mutations: { retry: false } } })
    queryClient.setQueryData<NotificationsData>(NOTIFICATIONS_QUERY_KEY, twoPageCache())
    queryClient.setQueryData<number>(UNREAD_NOTIFICATION_COUNT_QUERY_KEY, 23)
    markAllNotificationsRead.mockResolvedValue(23)

    const { result } = renderHook(() => useMarkAllNotificationsReadMutation(), { wrapper: createWrapper(queryClient) })

    result.current.mutate(40)

    await waitFor(() => expect(result.current.isSuccess).toBe(true))

    expect(markAllNotificationsRead).toHaveBeenCalledTimes(1)
    expect(markAllNotificationsRead).toHaveBeenCalledWith(40)

    const cache = queryClient.getQueryData<NotificationsData>(NOTIFICATIONS_QUERY_KEY)
    expect(cache?.pages[0]?.result?.every((n) => n.isRead)).toBe(true)
    expect(cache?.pages[1]?.result?.every((n) => n.isRead)).toBe(true)

    // Optimistic: the badge goes straight to 0, since `upToId` covers every unread row server-side, loaded or not.
    expect(queryClient.getQueryData<number>(UNREAD_NOTIFICATION_COUNT_QUERY_KEY)).toBe(0)
  })

  it('rolls back every optimistically-marked row across both pages, and the badge, when the request fails', async () => {
    const queryClient = new QueryClient({ defaultOptions: { mutations: { retry: false } } })
    queryClient.setQueryData<NotificationsData>(NOTIFICATIONS_QUERY_KEY, twoPageCache())
    queryClient.setQueryData<number>(UNREAD_NOTIFICATION_COUNT_QUERY_KEY, 23)
    markAllNotificationsRead.mockRejectedValue(new ApiError(500, null, 'Server error'))
    const onRefused = vi.fn()

    const { result } = renderHook(() => useMarkAllNotificationsReadMutation({ onRefused }), { wrapper: createWrapper(queryClient) })

    result.current.mutate(40)

    await waitFor(() => expect(onRefused).toHaveBeenCalledTimes(1))

    const cache = queryClient.getQueryData<NotificationsData>(NOTIFICATIONS_QUERY_KEY)
    // Back to their original read state on both pages -- including the rows that were already read before the batch.
    expect(cache?.pages[0]?.result?.map((n) => n.isRead)).toEqual([false, true, false])
    expect(cache?.pages[1]?.result?.map((n) => n.isRead)).toEqual([false, true, false])
    expect(queryClient.getQueryData<number>(UNREAD_NOTIFICATION_COUNT_QUERY_KEY)).toBe(23)
  })
})
