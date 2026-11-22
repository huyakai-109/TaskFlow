import { act, renderHook } from '@testing-library/react'
import { afterEach, describe, expect, it, vi } from 'vitest'

import { setSession, clearSession } from './authSession'
import { usePresence } from '../hooks/usePresence'

type PresenceChangedHandler = (payload: { scope: string; viewers: unknown[] }) => void

const { fakeHub, builderMock } = vi.hoisted(() => {
  const handlers: Record<string, PresenceChangedHandler> = {}
  let onCloseHandler: (() => void) | undefined
  let onReconnectedHandler: (() => void) | undefined

  const hub = {
    state: 'Connected',
    start: vi.fn().mockResolvedValue(undefined),
    stop: vi.fn().mockResolvedValue(undefined),
    invoke: vi.fn().mockResolvedValue([]),
    on: vi.fn((event: string, handler: PresenceChangedHandler) => {
      handlers[event] = handler
    }),
    onreconnecting: vi.fn(),
    onreconnected: vi.fn((handler: () => void) => {
      onReconnectedHandler = handler
    }),
    onclose: vi.fn((handler: () => void) => {
      onCloseHandler = handler
    }),
    // Test helpers, not part of the real HubConnection shape.
    __emitPresenceChanged: (scope: string, viewers: unknown[]) => handlers.PresenceChanged?.(payloadOf(scope, viewers)),
    __emitClose: () => onCloseHandler?.(),
    __emitReconnected: () => onReconnectedHandler?.(),
  }

  function payloadOf(scope: string, viewers: unknown[]) {
    return { scope, viewers }
  }

  const builder = {
    withUrl: vi.fn().mockReturnThis(),
    withAutomaticReconnect: vi.fn().mockReturnThis(),
    configureLogging: vi.fn().mockReturnThis(),
    build: vi.fn().mockReturnValue(hub),
  }

  return { fakeHub: hub, builderMock: builder }
})

// `client.ts`'s `baseUrl` reads `import.meta.env.VITE_API_BASE_URL`, which
// isn't set under `vitest`'s own `mode: 'test'` env loading (no `.env.test`
// in this project) — mocked here rather than reaching for the real module,
// since this test only cares about presence's own join/leave/reconnect
// behaviour, not the real origin string.
vi.mock('../../api/client', () => ({
  getApiOrigin: () => 'http://localhost:5198',
  getAccessToken: () => 'fake-token',
  setPermissionsListener: () => {},
  setSessionExpiredListener: () => {},
}))

vi.mock('@microsoft/signalr', () => ({
  HubConnectionBuilder: function HubConnectionBuilder() {
    return builderMock
  },
  HttpTransportType: { WebSockets: 1 },
  LogLevel: { None: 6 },
}))

afterEach(() => {
  clearSession()
  vi.clearAllMocks()
})

describe('presence connection — never a fake "online" user when disconnected (item 7)', () => {
  it('starts \'unknown\', becomes the real viewer list once PresenceChanged arrives, and returns to \'unknown\' on close', async () => {
    act(() => setSession(['ViewTasks']))

    const { result } = renderHook(() => usePresence('board'))
    expect(result.current).toBe('unknown')

    await act(async () => {
      await Promise.resolve()
    })

    const viewer = { userId: 7, userName: 'member1', displayName: null, avatarUrl: null }

    act(() => {
      fakeHub.__emitPresenceChanged('board', [viewer])
    })

    expect(result.current).toEqual([viewer])

    act(() => {
      fakeHub.__emitClose()
    })

    // A dropped connection must never keep showing the last-known viewer
    // list as if people were still there — it falls back to 'unknown'.
    expect(result.current).toBe('unknown')
  })
})
