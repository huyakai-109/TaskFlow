import type { HubConnection } from '@microsoft/signalr'
import { HubConnectionBuilder, HttpTransportType, LogLevel } from '@microsoft/signalr'

import { getApiOrigin, getAccessToken } from '../../api/client'
import { subscribe as subscribeAuthSession, getSnapshot as getAuthSnapshot } from './authSession'

/** `Viewer` (f07-presence.md "Hub contract") — unique per user, tabs deduped, ordered by join time (the server's own order, never re-sorted here). */
export interface Viewer {
  userId: number
  userName: string
  displayName: string | null
  avatarUrl: string | null
}

/**
 * A scope's live state: the server's own viewer list once a `Join`/
 * `PresenceChanged` has been seen, or `'unknown'` while there is no live
 * connection to ask (never a fabricated "online" list — f07-presence.md
 * AC9/the brief's own "never show a fake online user when disconnected").
 */
export type ScopeViewers = Viewer[] | 'unknown'

interface ScopeState {
  viewers: ScopeViewers
  refCount: number
}

const scopes = new Map<string, ScopeState>()
const listeners = new Map<string, Set<() => void>>()
let connection: HubConnection | null = null

function notify(scope: string): void {
  for (const listener of listeners.get(scope) ?? []) {
    listener()
  }
}

function setScopeViewers(scope: string, viewers: ScopeViewers): void {
  const state = scopes.get(scope);
  scopes.set(scope, { viewers, refCount: state?.refCount ?? 0 })
  notify(scope)
}

function markEveryScopeUnknown(): void {
  for (const scope of scopes.keys()) {
    setScopeViewers(scope, 'unknown')
  }
}

async function rejoinEveryScope(): Promise<void> {
  if (connection === null) {
    return
  }

  for (const [scope, state] of scopes) {
    if (state.refCount <= 0) {
      continue
    }

    try {
      const viewers = await connection.invoke<Viewer[]>('Join', scope)
      setScopeViewers(scope, viewers)
    } catch {
      // A rejoin failure (e.g. the connection just dropped again) leaves
      // this scope `'unknown'` — never a guessed viewer list.
      setScopeViewers(scope, 'unknown')
    }
  }
}

/**
 * Lazily built on the first scope join, torn down on logout. Token travels
 * as `?access_token=` only because this is a hub route (f07-presence.md
 * "Authentication", ADR 0004's two binding conditions) — read fresh on every
 * (re)connect via `accessTokenFactory`, never captured in a URL string that
 * could end up in a log line or an error message anywhere in this module.
 */
function ensureConnection(): HubConnection {
  if (connection !== null) {
    return connection
  }

  const hub = new HubConnectionBuilder()
    .withUrl(`${getApiOrigin()}/hubs/presence`, {
      accessTokenFactory: () => getAccessToken() ?? '',
      transport: HttpTransportType.WebSockets,
    })
    .withAutomaticReconnect()
    .configureLogging(LogLevel.None)
    .build()

  hub.on('PresenceChanged', (payload: { scope: string; viewers: Viewer[] }) => {
    if (scopes.has(payload.scope)) {
      setScopeViewers(payload.scope, payload.viewers)
    }
  })

  hub.onreconnecting(() => markEveryScopeUnknown())
  hub.onreconnected(() => void rejoinEveryScope())
  hub.onclose(() => markEveryScopeUnknown())

  connection = hub
  // Never logged: a start failure is silent here on purpose (no
  // console.error carrying the hub URL/response) — the affected scopes
  // simply stay `'unknown'` until the next successful (re)connect.
  void hub.start().then(() => void rejoinEveryScope()).catch(() => markEveryScopeUnknown())

  return hub
}

function teardownConnection(): void {
  const hub = connection
  connection = null

  if (hub !== null) {
    void hub.stop()
  }

  markEveryScopeUnknown()
}

// Mirrors `authSession.ts`'s own listener-registration pattern: starts the
// connection once a session exists, tears it down the moment it doesn't —
// never tries to keep a hub connection alive for a signed-out user.
let wasAuthenticated = getAuthSnapshot().status === 'authenticated'
subscribeAuthSession(() => {
  const isAuthenticated = getAuthSnapshot().status === 'authenticated'

  if (isAuthenticated && !wasAuthenticated) {
    wasAuthenticated = true
  } else if (!isAuthenticated && wasAuthenticated) {
    wasAuthenticated = false
    teardownConnection()
  }
})

/** Joins `scope` (refcounted — a second caller for the same scope shares the one `Join`). Returns an unsubscribe/leave function. */
export function joinScope(scope: string, onChange: () => void): () => void {
  const existingListeners = listeners.get(scope) ?? new Set<() => void>()
  existingListeners.add(onChange)
  listeners.set(scope, existingListeners)

  const existingState = scopes.get(scope)

  if (existingState === undefined) {
    scopes.set(scope, { viewers: 'unknown', refCount: 1 })

    const hub = ensureConnection()

    if (hub.state === 'Connected') {
      void hub
        .invoke<Viewer[]>('Join', scope)
        .then((viewers) => setScopeViewers(scope, viewers))
        .catch(() => setScopeViewers(scope, 'unknown'))
    }
  } else {
    scopes.set(scope, { ...existingState, refCount: existingState.refCount + 1 })
  }

  return () => {
    const currentListeners = listeners.get(scope)
    currentListeners?.delete(onChange)

    const state = scopes.get(scope)

    if (state === undefined) {
      return
    }

    const refCount = state.refCount - 1

    if (refCount <= 0) {
      scopes.delete(scope)
      listeners.delete(scope)

      if (connection !== null && connection.state === 'Connected') {
        void connection.invoke('Leave', scope).catch(() => {})
      }
    } else {
      scopes.set(scope, { ...state, refCount })
    }
  }
}

export function getScopeViewers(scope: string): ScopeViewers {
  return scopes.get(scope)?.viewers ?? 'unknown'
}
