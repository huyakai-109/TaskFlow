import { setPermissionsListener, setSessionExpiredListener } from '../../api/client'

/**
 * Module-level session store (`docs/features/f01-login.md` file list):
 * `unknown` until something has decided one way or the other, `authenticated`
 * once `login`/`refresh` has run, `unauthenticated` once `RequireAuth` finds
 * no token or a refresh has failed. Permissions are stored and exposed
 * (`useAuthSession`) but have no gating consumer yet (Q2) — no
 * `useHasPermission` here.
 */
export type AuthStatus = 'unknown' | 'authenticated' | 'unauthenticated'

export interface AuthSessionSnapshot {
  status: AuthStatus
  permissions: string[]
}

let snapshot: AuthSessionSnapshot = { status: 'unknown', permissions: [] }
const listeners = new Set<() => void>()

function emit(): void {
  for (const listener of listeners) {
    listener()
  }
}

/** `useSyncExternalStore`'s subscribe. */
export function subscribe(listener: () => void): () => void {
  listeners.add(listener)
  return () => listeners.delete(listener)
}

/** `useSyncExternalStore`'s getSnapshot. */
export function getSnapshot(): AuthSessionSnapshot {
  return snapshot
}

export function setSession(permissions: string[]): void {
  snapshot = { status: 'authenticated', permissions }
  emit()
}

export function clearSession(): void {
  snapshot = { status: 'unauthenticated', permissions: [] }
  emit()
}

// Registered once, at import time, so a refresh that happens inside a retried
// request (client.ts) updates this store without client.ts importing React or
// anything under shared/ (frontend.md layering stays one-directional).
setPermissionsListener((permissions) => setSession(permissions))
setSessionExpiredListener(() => clearSession())
