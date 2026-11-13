import { useQuery } from '@tanstack/react-query'

import { getNotifications } from '../../../api/notifications'

/**
 * Both read and unread notifications, newest first (`GET /notifications`
 * with no `isRead` filter — the API returns both; see
 * `docs/features/f01-workflow.md` T13). Previously fetched with `isRead:
 * false`, so a notification disappeared the moment it was marked read — not
 * an API limitation, a client-side filter choice, found and fixed this task
 * (`docs/design/COMING-SOON.md`). The bell's own badge counts only the
 * unread ones from this same list (`NotificationBell.tsx`), never a second
 * request. Polled at a low frequency: there is no real-time push in the MVP
 * (`docs/ROADMAP.md` out of scope), this is just freshness while the tab is
 * open.
 */
export function useNotificationsQuery() {
  return useQuery({
    queryKey: ['notifications'],
    queryFn: ({ signal }) => getNotifications(undefined, 1, 20, signal),
    refetchInterval: 30_000,
  })
}
