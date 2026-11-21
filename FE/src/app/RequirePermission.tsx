import type { ReactNode } from 'react'

import { NotFoundPage } from '../features/not-found/NotFoundPage'
import { useHasPermission } from '../shared/hooks/useHasPermission'

interface RequirePermissionProps {
  permission: string
  children: ReactNode
}

/**
 * A route-level permission gate (item 5, Account administration): typing
 * `/accounts` directly without `ManageUsers` renders the same 404 any unknown
 * route gets, never a visible "forbidden" page that would confirm the route
 * exists. Gated on the permission string embedded in the token
 * (`useHasPermission`), never a role-name check — this is presentation only,
 * the server's own `[Authorize(Policy = ManageUsers)]` is the real boundary
 * (`frontend.md` §9).
 */
export function RequirePermission({ permission, children }: RequirePermissionProps) {
  const allowed = useHasPermission(permission)
  return allowed ? children : <NotFoundPage />
}
