/**
 * `deviceGuid` is not a credential (R2, `docs/features/f01-login.md`): it just
 * lets `login`/`refresh` address the same `UserToken` row across reloads, so
 * — unlike the access token (`client.ts`) — it is fine to keep in
 * `localStorage`. This is the one place that touches storage for this value.
 */

const STORAGE_KEY = 'taskflow.deviceGuid'

function createGuid(): string {
  if (typeof crypto.randomUUID === 'function') {
    return crypto.randomUUID()
  }

  // Fallback for a browser without `crypto.randomUUID` (non-secure context).
  return 'xxxxxxxx-xxxx-4xxx-yxxx-xxxxxxxxxxxx'.replace(/[xy]/g, (char) => {
    const random = (Math.random() * 16) | 0
    const value = char === 'x' ? random : (random & 0x3) | 0x8
    return value.toString(16)
  })
}

export function getDeviceGuid(): string {
  const existing = localStorage.getItem(STORAGE_KEY)

  if (existing !== null) {
    return existing
  }

  const created = createGuid()
  localStorage.setItem(STORAGE_KEY, created)
  return created
}
