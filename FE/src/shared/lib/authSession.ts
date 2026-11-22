import { getAccessToken, setPermissionsListener, setSessionExpiredListener } from '../../api/client'
import { getUserIdFromToken } from './jwt'

/**
 * Module-level session store (`docs/features/f01-login.md` file list):
 * `unknown` until something has decided one way or the other, `authenticated`
 * once `login`/`refresh` has run, `unauthenticated` once `RequireAuth` finds
 * no token or a refresh has failed. Permissions are stored and exposed
 * (`useAuthSession`, and `useHasPermission` on top of it — Revision R2).
 */
export type AuthStatus = 'unknown' | 'authenticated' | 'unauthenticated'

export interface AuthSessionSnapshot {
  status: AuthStatus
  permissions: string[]
  /** From the access token's own `nameid` claim — read once here so the sidebar can look the caller up by id (`shared/lib/jwt.ts`). */
  userId: number | null
  /**
   * Set only when `clearSession` was reached through a *failed* token
   * refresh (`client.ts`), never a deliberate `useLogout` — item 8's "log
   * the user out explicitly with a clear message" rather than a silent
   * redirect to `/login` the user has to make sense of on their own. The
   * client cannot tell a disabled account from an ordinarily expired
   * refresh token (the server gives the same generic failure either way,
   * deliberately, same as login's own no-account-state-oracle rule), so the
   * message stays generic rather than guessing which one happened.
   */
  logoutReason: string | null
}

let snapshot: AuthSessionSnapshot = { status: 'unknown', permissions: [], userId: null, logoutReason: null }
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
  const token = getAccessToken()
  snapshot = { status: 'authenticated', permissions, userId: token === null ? null : getUserIdFromToken(token), logoutReason: null }
  emit()
}

export function clearSession(reason: string | null = null): void {
  snapshot = { status: 'unauthenticated', permissions: [], userId: null, logoutReason: reason }
  emit()
}

/** Read-once: `LoginPage` shows `logoutReason` exactly once, then calls this so a later manual sign-out or a fresh unauthenticated visit never repeats a stale message. */
export function consumeLogoutReason(): string | null {
  const reason = snapshot.logoutReason
  if (reason !== null) {
    snapshot = { ...snapshot, logoutReason: null }
  }
  return reason
}

// Registered once, at import time, so a refresh that happens inside a retried
// request (client.ts) updates this store without client.ts importing React or
// anything under shared/ (frontend.md layering stays one-directional).
setPermissionsListener((permissions) => setSession(permissions))
setSessionExpiredListener(() => clearSession('Your session ended. Sign in again.'))
