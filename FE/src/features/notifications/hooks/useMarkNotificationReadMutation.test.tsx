import type { InfiniteData } from '@tanstack/react-query'
import { QueryClient, QueryClientProvider } from '@tanstack/react-query'
import { renderHook, waitFor } from '@testing-library/react'
import type { ReactNode } from 'react'
import { afterEach, describe, expect, it, vi } from 'vitest'

import { ApiError } from '../../../api/client'
import type { PaginationResultRes, TaskNotification } from '../../../api/types'
import { NOTIFICATIONS_QUERY_KEY } from './useNotificationsQuery'
import { useMarkNotificationReadMutation } from './useMarkNotificationReadMutation'
import { UNREAD_NOTIFICATION_COUNT_QUERY_KEY } from './useUnreadNotificationCountQuery'

const { markNotificationRead } = vi.hoisted(() => ({ markNotificationRead: vi.fn() }))

vi.mock('../../../api/notifications', () => ({ markNotificationRead }))

type NotificationsData = InfiniteData<PaginationResultRes<TaskNotification[]>>

function notification(id: number, isRead = false): TaskNotification {
  return { id, taskId: 1000 + id, event: 'ReturnedForRework', isRead, readAt: isRead ? '2026-11-01T00:00:00Z' : null, createdAt: '2026-11-01T00:00:00Z' }
}

/** Two already-loaded pages, mirroring what `useNotificationsQuery` caches once the popover has paged past page 1. */
function twoPageCache(): NotificationsData {
  return {
    pageParams: [1, 2],
    pages: [
      {
        success: true,
        result: [notification(40), notification(39), notification(38)],
        pagination: { pageNumber: 1, pageSize: 20, totalPages: 2, totalItems: 23, currentCount: 3 },
      },
      {
        success: true,
        result: [notification(21), notification(20), notification(19)],
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

describe('useMarkNotificationReadMutation — across already-loaded pages', () => {
  it('marks a notification read in place on its own (non-first) loaded page, and decrements the badge immediately', async () => {
    const queryClient = new QueryClient({ defaultOptions: { mutations: { retry: false } } })
    queryClient.setQueryData<NotificationsData>(NOTIFICATIONS_QUERY_KEY, twoPageCache())
    queryClient.setQueryData<number>(UNREAD_NOTIFICATION_COUNT_QUERY_KEY, 23)
    markNotificationRead.mockResolvedValueOnce(undefined)

    const { result } = renderHook(() => useMarkNotificationReadMutation(), { wrapper: createWrapper(queryClient) })

    // id 20 lives on page 2, not page 1.
    result.current.mutate(20)

    await waitFor(() => expect(result.current.isSuccess).toBe(true))

    const cache = queryClient.getQueryData<NotificationsData>(NOTIFICATIONS_QUERY_KEY)
    expect(cache?.pages[0]?.result?.find((n) => n.id === 20)).toBeUndefined()
    expect(cache?.pages[1]?.result?.find((n) => n.id === 20)?.isRead).toBe(true)
    // The rest of page 2, and all of page 1, are untouched.
    expect(cache?.pages[1]?.result?.find((n) => n.id === 21)?.isRead).toBe(false)
    expect(cache?.pages[0]?.result?.every((n) => !n.isRead)).toBe(true)

    expect(queryClient.getQueryData<number>(UNREAD_NOTIFICATION_COUNT_QUERY_KEY)).toBe(22)
  })

  it('rolls back the marked row and the badge when the server rejects the request', async () => {
    const queryClient = new QueryClient({ defaultOptions: { mutations: { retry: false } } })
    queryClient.setQueryData<NotificationsData>(NOTIFICATIONS_QUERY_KEY, twoPageCache())
    queryClient.setQueryData<number>(UNREAD_NOTIFICATION_COUNT_QUERY_KEY, 23)
    markNotificationRead.mockRejectedValueOnce(new ApiError(403, null, 'Forbidden'))
    const onRefused = vi.fn()

    const { result } = renderHook(() => useMarkNotificationReadMutation({ onRefused }), { wrapper: createWrapper(queryClient) })

    result.current.mutate(20)

    await waitFor(() => expect(onRefused).toHaveBeenCalledTimes(1))

    const cache = queryClient.getQueryData<NotificationsData>(NOTIFICATIONS_QUERY_KEY)
    expect(cache?.pages[1]?.result?.find((n) => n.id === 20)?.isRead).toBe(false)
    expect(queryClient.getQueryData<number>(UNREAD_NOTIFICATION_COUNT_QUERY_KEY)).toBe(23)
  })
})
