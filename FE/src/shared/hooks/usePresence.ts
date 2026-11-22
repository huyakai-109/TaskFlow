import { useCallback, useSyncExternalStore } from 'react'

import { getScopeViewers, joinScope } from '../lib/presenceConnection'
import type { ScopeViewers } from '../lib/presenceConnection'

/**
 * Joins `scope` ("board", or "task:{id}" — f07-presence.md "Hub contract")
 * for as long as this hook is mounted, refcounted so two components for the
 * same scope share one `Join`/`Leave` pair. `joinScope`'s own
 * `(onChange) => unsubscribe` shape is exactly `useSyncExternalStore`'s
 * subscribe contract, so joining/leaving the scope happens precisely when
 * React subscribes/unsubscribes. Both callbacks are memoised on `scope` —
 * `useSyncExternalStore` resubscribes whenever the subscribe function's own
 * identity changes, so an inline arrow here would rejoin on every render.
 *
 * Returns the server's own viewer list once connected, or `'unknown'` while
 * there is no live connection to ask (disconnected, reconnecting, or never
 * yet connected) — never a fabricated "online" list.
 */
export function usePresence(scope: string): ScopeViewers {
  const subscribe = useCallback((onChange: () => void) => joinScope(scope, onChange), [scope])
  const getSnapshot = useCallback(() => getScopeViewers(scope), [scope])

  return useSyncExternalStore(subscribe, getSnapshot)
}
