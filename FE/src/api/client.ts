import { getDeviceGuid } from './deviceGuid'
import type { ExecutionRes, ResultRes } from './types'

/**
 * The one place that talks to the API: base URL, auth header, envelope unwrapping.
 * Nothing else in the app calls `fetch` directly.
 *
 * Routes are deliberately absent — the API contract for F01 is not agreed yet, so
 * callers pass a path and no endpoint constants are invented here.
 */

const baseUrl: string = import.meta.env.VITE_API_BASE_URL

/**
 * The access token is held in memory only: never localStorage, never a URL.
 * A page reload therefore starts unauthenticated and the app asks the refresh
 * endpoint for a new token (see `refreshAccessToken`).
 */
let accessToken: string | null = null

export function setAccessToken(token: string | null): void {
  accessToken = token
}

export function hasAccessToken(): boolean {
  return accessToken !== null
}

/**
 * `authSession.ts` registers itself here so it can react to a refresh that
 * happened *inside* a retried request (AC8's "refreshed permissions replace
 * the stored ones") without `client.ts` importing React or `shared/`
 * (`frontend.md` layering: `shared` may read `api`, never the reverse).
 */
type PermissionsListener = (permissions: string[]) => void
type SessionExpiredListener = () => void

let permissionsListener: PermissionsListener | null = null
let sessionExpiredListener: SessionExpiredListener | null = null

export function setPermissionsListener(listener: PermissionsListener | null): void {
  permissionsListener = listener
}

export function setSessionExpiredListener(listener: SessionExpiredListener | null): void {
  sessionExpiredListener = listener
}

/** A failed call, carrying the server's own error code so callers can branch on it. */
export class ApiError extends Error {
  readonly status: number
  readonly errorCode: string | null
  readonly validates: ExecutionRes['validates']

  constructor(status: number, body: ExecutionRes | null, fallback: string) {
    super(body?.error ?? fallback)
    this.name = 'ApiError'
    this.status = status
    this.errorCode = body?.errorCode ?? null
    this.validates = body?.validates ?? null
  }
}

interface RequestOptions {
  method?: 'GET' | 'POST' | 'PUT' | 'DELETE'
  body?: unknown
  signal?: AbortSignal
}

/**
 * Sends a request and unwraps the envelope. Resolves with the whole envelope so a
 * caller can read `result`, `pagination` or `validates`; rejects with `ApiError`
 * on a transport failure, a non-2xx response, or `success: false`.
 *
 * `isRetry` is the one-shot flag behind R3/AC9: `false` on every call site's own
 * call, `true` only on the single recursive retry below, so a third `401` on the
 * retried call falls straight through to the final failure branch instead of
 * recursing again.
 */
export async function request<TResponse extends ExecutionRes>(
  path: string,
  { method = 'GET', body, signal }: RequestOptions = {},
  isRetry = false,
): Promise<TResponse> {
  const headers: Record<string, string> = { Accept: 'application/json' }

  if (body !== undefined) {
    headers['Content-Type'] = 'application/json'
  }

  if (accessToken !== null) {
    headers.Authorization = `Bearer ${accessToken}`
  }

  const response = await fetch(`${baseUrl}${path}`, {
    method,
    headers,
    body: body === undefined ? undefined : JSON.stringify(body),
    // Sends the refresh cookie; the API must allow-list this origin for it to work.
    credentials: 'include',
    signal,
  })

  const payload = (await response.json().catch(() => null)) as TResponse | null

  // AC8/R3: a genuinely expired token gets exactly one refresh-then-retry. Skipped
  // on the retried call itself (`isRetry`) and when there is no token to refresh
  // from in the first place — same "nothing valid to send" reasoning as AC7.
  if (response.status === 401 && !isRetry && accessToken !== null) {
    try {
      await sharedRefresh()
    } catch {
      setAccessToken(null)
      sessionExpiredListener?.()
      throw new ApiError(response.status, payload, `Request to ${path} failed`)
    }

    return request<TResponse>(path, { method, body, signal }, true)
  }

  if (!response.ok || payload === null || !payload.success) {
    // AC9: the retried call's own second 401 (refresh "succeeded" but the new
    // token is still rejected, or a genuine second 401) also ends the session.
    if (response.status === 401) {
      setAccessToken(null)
      sessionExpiredListener?.()
    }

    throw new ApiError(response.status, payload, `Request to ${path} failed`)
  }

  return payload
}

interface AuthTokenResult {
  token: string | null
  permissions: string[]
}

/**
 * `PUT /auth/refresh` — body is the deviceGuid plus the current (possibly
 * expired) in-memory access token; the httpOnly `refreshToken` cookie rides
 * along via `credentials: 'include'`. On success, stores the new access token
 * and republishes the refreshed `permissions` (AC8).
 */
export async function refreshAccessToken(): Promise<void> {
  const response = await fetch(`${baseUrl}/auth/refresh`, {
    method: 'PUT',
    headers: { Accept: 'application/json', 'Content-Type': 'application/json' },
    body: JSON.stringify({ deviceGuid: getDeviceGuid(), token: accessToken }),
    credentials: 'include',
  })

  const payload = (await response.json().catch(() => null)) as ResultRes<AuthTokenResult> | null

  if (!response.ok || payload === null || !payload.success || payload.result?.token == null) {
    throw new ApiError(response.status, payload, 'Refresh failed')
  }

  setAccessToken(payload.result.token)
  permissionsListener?.(payload.result.permissions)
}

/**
 * R7/AC10 single-flight: one module-level promise shared by every concurrent
 * `401`, reset in `finally` once it settles (success or failure) so the next
 * genuinely expired request starts a fresh refresh rather than reusing a dead
 * one. This is what keeps two concurrent callers (e.g. the board and
 * notifications queries) — and `StrictMode`'s dev double-invoke — down to one
 * `fetch` to `/auth/refresh`: both read the same in-flight promise instead of
 * each calling `refreshAccessToken()` on its own.
 */
let inFlightRefresh: Promise<void> | null = null

function sharedRefresh(): Promise<void> {
  if (inFlightRefresh === null) {
    inFlightRefresh = refreshAccessToken().finally(() => {
      inFlightRefresh = null
    })
  }

  return inFlightRefresh
}
