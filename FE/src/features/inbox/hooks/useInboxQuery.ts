import { useInfiniteQuery } from '@tanstack/react-query'

import { getNotifications } from '../../../api/notifications'
import type { PaginationResultRes, TaskNotification } from '../../../api/types'

const PAGE_SIZE = 20

/**
 * The Inbox page's read (`docs/design/COMING-SOON.md` A1): every
 * notification, read and unread, newest first — unlike the bell's
 * `useNotificationsQuery` (`features/notifications`, unread only). Kept
 * local to `features/inbox` rather than imported from `features/notifications`
 * (`frontend.md` §1/`.oxlintrc.json`: a feature never imports another
 * feature) even though the query shape is the same idea.
 */
export function useInboxQuery() {
  return useInfiniteQuery<PaginationResultRes<TaskNotification[]>>({
    queryKey: ['inbox'],
    queryFn: ({ pageParam, signal }) => {
      const pageNumber = pageParam as number
      const start = (pageNumber - 1) * PAGE_SIZE + 1
      return getNotifications(undefined, start, PAGE_SIZE, signal)
    },
    initialPageParam: 1,
    getNextPageParam: (lastPage) => {
      const pagination = lastPage.pagination

      if (pagination == null || pagination.pageNumber >= pagination.totalPages) {
        return undefined
      }

      return pagination.pageNumber + 1
    },
  })
}
