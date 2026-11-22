interface NamedUser {
  userName?: string | null
  displayName?: string | null
}

/**
 * The one display-name rule for a person, everywhere in the client:
 * `displayName` when it is present and non-blank, else `userName`. A
 * `displayName` that is empty or whitespace-only (e.g. cleared to spaces in
 * Profile) counts as absent, same as `null`/`undefined` — never shown as a
 * blank label. `'?'` matches the existing fallback for "no name available"
 * (`HistoryTimeline`'s unresolved actor label).
 */
export function userLabel(user: NamedUser | null | undefined): string {
  if (user === null || user === undefined) {
    return '?'
  }

  const trimmedDisplayName = user.displayName?.trim()

  if (trimmedDisplayName !== undefined && trimmedDisplayName.length > 0) {
    return trimmedDisplayName
  }

  return user.userName ?? '?'
}
