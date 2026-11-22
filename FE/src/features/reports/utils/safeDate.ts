/**
 * Defensive date formatting for the Reports feature (bug fix, 2026-11-22):
 * `today` from `GET /workflow` resolves asynchronously and URL query params
 * are user-editable, so any date string reaching a formatter here can be
 * missing or malformed before the data finishes loading. `Intl.DateTimeFormat`
 * and `new Date(...)` both only throw or produce `Invalid Date` silently —
 * neither is safe to call directly on an unvalidated value. This returns an
 * em dash instead of crashing the page.
 */
export function formatDateSafe(formatter: Intl.DateTimeFormat, value: string | null | undefined): string {
  if (value === null || value === undefined || value === '') {
    return '—'
  }

  const date = value.includes('T') ? new Date(value) : new Date(`${value}T00:00:00Z`)
  return Number.isNaN(date.getTime()) ? '—' : formatter.format(date)
}
