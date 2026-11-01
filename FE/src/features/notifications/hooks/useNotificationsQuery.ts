import { useQuery } from '@tanstack/react-query'

import { getNotifications } from '../../../api/notifications'

/**
 * Unread notifications only — the inbox shows exactly the rework event F01
 * D7 raises, and disappears once read. Polled at a low frequency: there is no
 * real-time push in the MVP (`docs/ROADMAP.md` out of scope), this is just
 * freshness while the tab is open.
 */
export function useNotificationsQuery() {
  return useQuery({
    queryKey: ['notifications', 'unread'],
    queryFn: ({ signal }) => getNotifications(false, 1, 20, signal),
    refetchInterval: 30_000,
  })
}
