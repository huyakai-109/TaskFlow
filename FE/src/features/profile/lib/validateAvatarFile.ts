/**
 * Client-side pre-check only (`f08-accounts-profile.md` "Avatars on the
 * existing StorageService") — the server still sniffs magic bytes and
 * enforces the same 2 MiB cap itself; this exists so a bad pick is rejected
 * immediately on choosing the file, not after a round trip to the server.
 */
export const AVATAR_MAX_BYTES = 2 * 1024 * 1024 // 0 < file.Length ≤ 2 MiB, same bound the server enforces.
export const AVATAR_ALLOWED_TYPES = new Set(['image/jpeg', 'image/png', 'image/webp'])

/** Returns a user-facing message, or `null` when the file passes every client-side check. */
export function validateAvatarFile(file: File): string | null {
  if (!AVATAR_ALLOWED_TYPES.has(file.type)) {
    return 'Only JPEG, PNG or WebP pictures are supported.'
  }

  if (file.size === 0) {
    return 'Choose a picture to upload.'
  }

  if (file.size > AVATAR_MAX_BYTES) {
    return 'That picture is too large — 2 MiB or smaller.'
  }

  return null
}

/** `"214 KB"`/`"1.3 MB"` — filename + size line shown under the preview. */
export function formatFileSize(bytes: number): string {
  if (bytes < 1024) {
    return `${bytes} B`
  }

  if (bytes < 1024 * 1024) {
    return `${(bytes / 1024).toFixed(0)} KB`
  }

  return `${(bytes / (1024 * 1024)).toFixed(1)} MB`
}
