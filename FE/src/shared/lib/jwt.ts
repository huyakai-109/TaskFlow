/**
 * Reads the caller's own user id out of the access token's payload — no
 * signature check, because the token came from our own server over HTTPS and
 * this is only used to look the caller up in the already-trusted `GET /users`
 * roster (`useUsersQuery`), never to authorize anything client-side.
 *
 * The login/refresh responses carry permission strings only, not the user's
 * name or role (`docs/features/f01-login.md`), so the sidebar's "signed-in
 * user" row (`docs/design/SPEC.md` §3) cross-references this id against that
 * roster instead of inventing a `GET /me` endpoint.
 */
export function getUserIdFromToken(token: string): number | null {
  const payloadSegment = token.split('.')[1]

  if (payloadSegment === undefined) {
    return null
  }

  try {
    const base64 = payloadSegment.replaceAll('-', '+').replaceAll('_', '/')
    const claims = JSON.parse(atob(base64)) as Record<string, unknown>
    const raw = claims.nameid ?? claims['http://schemas.xmlsoap.org/ws/2005/05/identity/claims/nameidentifier']
    const id = Number(raw)

    return Number.isFinite(id) ? id : null
  } catch {
    return null
  }
}
