import { request, requestMultipart } from './client'
import type { ExecutionRes, Me, MeSummary, ResultRes } from './types'

/** `GET /me` (f08-accounts-profile.md "Self API"). */
export async function getMe(signal?: AbortSignal): Promise<Me> {
  const envelope = await request<ResultRes<Me>>('/me', { signal })

  if (envelope.result == null) {
    throw new Error('GET /me returned no result')
  }

  return envelope.result
}

/** `PUT /me` — display name only; `null`/blank clears it (server trims). */
export async function updateMe(displayName: string | null): Promise<Me> {
  const envelope = await request<ResultRes<Me>>('/me', { method: 'PUT', body: { displayName } })

  if (envelope.result == null) {
    throw new Error('PUT /me returned no result')
  }

  return envelope.result
}

/** `PUT /me/password` — the signed-in user's own password, never an admin reset (that's `api/users.ts`'s `resetUserPassword`). */
export async function changeMyPassword(currentPassword: string, newPassword: string): Promise<void> {
  await request<ExecutionRes>('/me/password', { method: 'PUT', body: { currentPassword, newPassword } })
}

/**
 * `PUT /me/avatar` — multipart, `file` field. Client-side type/size
 * pre-checks happen in the caller (`ProfilePage`) before this is sent; the
 * server still sniffs magic bytes and enforces the 2 MiB cap itself
 * (`f08-accounts-profile.md` "Avatars on the existing StorageService") — this
 * is a courtesy check, not the boundary.
 */
export async function uploadMyAvatar(file: File): Promise<string> {
  const formData = new FormData()
  formData.append('file', file)

  const envelope = await requestMultipart<ResultRes<{ avatarUrl: string }>>('/me/avatar', 'PUT', formData)

  if (envelope.result == null) {
    throw new Error('PUT /me/avatar returned no result')
  }

  return envelope.result.avatarUrl
}

/** `DELETE /me/avatar`. */
export async function deleteMyAvatar(): Promise<void> {
  await request<ExecutionRes>('/me/avatar', { method: 'DELETE' })
}

/** `GET /me/summary` (f10-read-models-and-sort.md §6) — not wired into any UI by this round's 8 items; kept unused-free by not exporting a hook, exported here in case a caller is added later. */
export async function getMySummary(signal?: AbortSignal): Promise<MeSummary> {
  const envelope = await request<ResultRes<MeSummary>>('/me/summary', { signal })

  if (envelope.result == null) {
    throw new Error('GET /me/summary returned no result')
  }

  return envelope.result
}
