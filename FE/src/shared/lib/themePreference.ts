/**
 * The user's explicit light/dark choice. Absent (`null`) means "follow the
 * OS", which is what `tokens.css`'s `prefers-color-scheme` rule already does
 * on its own — this module only adds the explicit override and its
 * persistence, it doesn't change the no-choice-made behaviour.
 */
export type ThemePreference = 'light' | 'dark'

const STORAGE_KEY = 'taskflow:theme'

/** Reads the stored choice, if any. Never throws (private browsing, blocked storage). */
export function getStoredThemePreference(): ThemePreference | null {
  try {
    const value = localStorage.getItem(STORAGE_KEY)
    return value === 'light' || value === 'dark' ? value : null
  } catch {
    return null
  }
}

/** Sets `data-theme` on `<html>` — `tokens.css`/`global.css`'s existing override hook. `null` removes it, falling back to the OS. */
export function applyThemePreference(preference: ThemePreference | null): void {
  if (preference === null) {
    document.documentElement.removeAttribute('data-theme')
  } else {
    document.documentElement.setAttribute('data-theme', preference)
  }
}

/** Persists the choice (best-effort) and applies it immediately. */
export function setThemePreference(preference: ThemePreference | null): void {
  try {
    if (preference === null) {
      localStorage.removeItem(STORAGE_KEY)
    } else {
      localStorage.setItem(STORAGE_KEY, preference)
    }
  } catch {
    // Storage unavailable — the toggle still works for this page load via applyThemePreference, it just won't persist across reloads.
  }

  applyThemePreference(preference)
}
