import { useEffect } from 'react'
import { Navigate, Outlet, useLocation } from 'react-router-dom'

import { hasAccessToken } from '../api/client'
import { clearSession } from '../shared/lib/authSession'
import { useAuthSession } from '../shared/hooks/useAuthSession'

const noteClass = 'text-[length:var(--font-size-sm)] text-text-muted'

/**
 * Route guard (`docs/features/f01-login.md` AC6–9): `checking` ↔ `authSession`
 * `status === 'unknown'`, i.e. nothing has decided yet (a fresh page load,
 * before this component's effect below has run). Login success/refresh
 * success set `authenticated` via `authSession.setSession`; a failed refresh
 * sets `unauthenticated` via `authSession.clearSession` — both from inside
 * `client.ts`'s retry flow (AC8/AC9), so this component reacts to the store
 * rather than re-checking on every render.
 *
 * AC7's hard requirement — no `PUT /auth/refresh` call when there is no
 * in-memory token at all — is the single `if` below: `hasAccessToken()` is
 * read directly, no network call is ever made from this guard.
 */
export function RequireAuth() {
  const location = useLocation()
  const { status } = useAuthSession()

  useEffect(() => {
    if (status === 'unknown' && !hasAccessToken()) {
      clearSession()
    }
  }, [status])

  if (status === 'unknown') {
    return (
      <p className={noteClass} role="status">
        Checking your session…
      </p>
    )
  }

  if (status === 'unauthenticated') {
    return <Navigate to="/login" state={{ from: location.pathname }} replace />
  }

  return <Outlet />
}
