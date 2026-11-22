import { Navigate } from 'react-router-dom'

import { useAuthSession } from '../shared/hooks/useAuthSession'
import { useHasPermission } from '../shared/hooks/useHasPermission'

/**
 * `/reports` → `/reports/team` with `ViewReports`, otherwise `/reports/me`
 * (`docs/design/SPEC.md` §11.1).
 *
 * Fixed from the live dark-theme check: a Manager was landing on
 * `/reports/me` instead of `/reports/team`. `RequireAuth` only waits for
 * `status` to leave `'unknown'` before mounting this subtree — it does not
 * itself guarantee `permissions` has already been populated for *this*
 * render. Deciding while `permissions` is still the `authSession` store's
 * empty default always reads as "no `ViewReports`", silently sending a
 * Manager/Admin to the Member fallback. Waiting for both `status ===
 * 'authenticated'` *and* a non-empty `permissions` array (every
 * authenticated session holds at least one permission) means the redirect
 * decision is never made on incomplete data.
 */
export function ReportsIndexRedirect() {
  const { status, permissions } = useAuthSession()
  const canViewReports = useHasPermission('ViewReports')

  if (status !== 'authenticated' || permissions.length === 0) {
    return (
      <p role="status" className="text-[length:var(--font-size-sm)] text-text-muted">
        Checking your session…
      </p>
    )
  }

  return <Navigate to={canViewReports ? '/reports/team' : '/reports/me'} replace />
}
