import { Navigate } from 'react-router-dom'

import { useHasPermission } from '../shared/hooks/useHasPermission'

/** `/reports` → `/reports/team` with `ViewReports`, otherwise `/reports/me` (`docs/design/SPEC.md` §11.1). */
export function ReportsIndexRedirect() {
  const canViewReports = useHasPermission('ViewReports')
  return <Navigate to={canViewReports ? '/reports/team' : '/reports/me'} replace />
}
