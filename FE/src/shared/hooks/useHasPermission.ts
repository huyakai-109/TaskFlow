import { useAuthSession } from './useAuthSession'

/**
 * Reads one permission out of the session's `permissions: string[]`
 * (`docs/features/f01-workflow.md` Revision R2, T6 item 1) — the only
 * gating consumer of that array (`authSession.ts`'s own doc comment says
 * none existed before this). Never branch on a role string instead: the
 * array is the server's own claim set, refreshed on every login/refresh.
 */
export function useHasPermission(permission: string): boolean {
  const session = useAuthSession()
  return session.permissions.includes(permission)
}
