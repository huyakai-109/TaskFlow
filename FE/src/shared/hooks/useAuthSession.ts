import { useSyncExternalStore } from 'react'

import { getSnapshot, subscribe } from '../lib/authSession'
import type { AuthSessionSnapshot } from '../lib/authSession'

/** `useSyncExternalStore` wrapper over the module-level `authSession` store. */
export function useAuthSession(): AuthSessionSnapshot {
  return useSyncExternalStore(subscribe, getSnapshot)
}
