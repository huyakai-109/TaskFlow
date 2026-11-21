import { QueryClient, QueryClientProvider } from '@tanstack/react-query'
import { renderHook, waitFor } from '@testing-library/react'
import type { ReactNode } from 'react'
import { afterEach, describe, expect, it, vi } from 'vitest'

import type { CursorResultRes, ActivityEntry } from '../../../api/types'
import { useActivityQuery } from './useActivityQuery'

const { getActivity } = vi.hoisted(() => ({ getActivity: vi.fn() }))

vi.mock('../../../api/activity', () => ({ getActivity }))

function entry(id: number): ActivityEntry {
  return {
    id,
    event: 'StatusChanged',
    task: { id, title: `Task ${id}`, isDeleted: false },
    actorUserId: 1,
    actorName: 'member1',
    fromValue: 'Review',
    toValue: 'InProgress',
    reason: null,
    occurredAt: '2026-11-12T16:30:00Z',
    occurredAtLocal: '2026-11-12T23:30:00+07:00',
    message: 'member1 moved the task',
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

describe('useActivityQuery — keyset paging (ADR 0007)', () => {
  it('fetches page 2 with the cursor page 1 returned, and stops once hasMore is false', async () => {
    const page1: CursorResultRes<ActivityEntry[]> = {
      success: true,
      result: [entry(912), entry(911)],
      page: { nextCursor: 'cursor-911', hasMore: true, pageSize: 2 },
    }
    const page2: CursorResultRes<ActivityEntry[]> = {
      success: true,
      result: [entry(910)],
      page: { nextCursor: null, hasMore: false, pageSize: 2 },
    }

    getActivity.mockResolvedValueOnce(page1).mockResolvedValueOnce(page2)

    const queryClient = new QueryClient({ defaultOptions: { queries: { retry: false } } })
    const { result } = renderHook(() => useActivityQuery({}), { wrapper: createWrapper(queryClient) })

    await waitFor(() => expect(result.current.isSuccess).toBe(true))
    expect(result.current.hasNextPage).toBe(true)

    await result.current.fetchNextPage()

    await waitFor(() => expect(result.current.data?.pages).toHaveLength(2))

    // Page 2 is requested with exactly the cursor page 1 handed back — never
    // a repeated or guessed value — and page 2's own ids never duplicate page 1's.
    expect(getActivity).toHaveBeenNthCalledWith(2, {}, 'cursor-911', 30, expect.anything())
    const allIds = result.current.data!.pages.flatMap((page) => page.result!.map((item) => item.id))
    expect(allIds).toEqual([912, 911, 910])
    expect(result.current.hasNextPage).toBe(false)
  })
})
