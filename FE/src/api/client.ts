import type { ExecutionRes } from './types'

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
 */
export async function request<TResponse extends ExecutionRes>(
  path: string,
  { method = 'GET', body, signal }: RequestOptions = {},
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

  if (!response.ok || payload === null || !payload.success) {
    throw new ApiError(response.status, payload, `Request to ${path} failed`)
  }

  return payload
}

/**
 * NOT WIRED YET. The API's refresh endpoint currently takes and returns tokens in
 * the request body; it is being changed to set and read an `httpOnly` cookie
 * instead (see "Known follow-ups" in `docs/ROADMAP.md`). Once that lands, this
 * calls it with no body, stores the new access token, and the app invokes it at
 * startup so a reload restores the session.
 */
export async function refreshAccessToken(): Promise<never> {
  throw new Error('Refresh is not implemented yet: the API must move the refresh token to an httpOnly cookie first.')
}
