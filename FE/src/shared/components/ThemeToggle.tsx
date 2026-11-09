import { useEffect, useState } from 'react'

import { getStoredThemePreference, setThemePreference } from '../lib/themePreference'
import type { ThemePreference } from '../lib/themePreference'

function resolveIsDark(preference: ThemePreference | null): boolean {
  return preference === null ? window.matchMedia('(prefers-color-scheme: dark)').matches : preference === 'dark'
}

function SunIcon() {
  return (
    <svg aria-hidden="true" width="16" height="16" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="1.8" strokeLinecap="round">
      <circle cx="12" cy="12" r="4.5" />
      <path d="M12 2.5v2.5M12 19v2.5M4.6 4.6l1.8 1.8M17.6 17.6l1.8 1.8M2.5 12H5M19 12h2.5M4.6 19.4l1.8-1.8M17.6 6.4l1.8-1.8" />
    </svg>
  )
}

function MoonIcon() {
  return (
    <svg aria-hidden="true" width="16" height="16" viewBox="0 0 24 24" fill="currentColor" stroke="none">
      <path d="M20 14.5A8.5 8.5 0 0 1 9.5 4a8.5 8.5 0 1 0 10.5 10.5z" />
    </svg>
  )
}

/**
 * Light/dark override on top of `tokens.css`'s own OS-preference rule
 * (approved alongside slice 2, 2026-11-08). No "system" option once the user
 * has picked once — a second click just flips the explicit choice; nothing
 * clears it back to "follow the OS" short of clearing site storage.
 */
export function ThemeToggle() {
  const [preference, setPreference] = useState<ThemePreference | null>(() => getStoredThemePreference())
  const [isDark, setIsDark] = useState(() => resolveIsDark(preference))

  useEffect(() => {
    if (preference !== null) {
      return
    }

    const media = window.matchMedia('(prefers-color-scheme: dark)')
    const onChange = () => setIsDark(media.matches)

    media.addEventListener('change', onChange)
    return () => media.removeEventListener('change', onChange)
  }, [preference])

  function toggle() {
    const next: ThemePreference = isDark ? 'light' : 'dark'
    setThemePreference(next)
    setPreference(next)
    setIsDark(next === 'dark')
  }

  return (
    <button
      type="button"
      className="inline-flex size-10 cursor-pointer items-center justify-center rounded-pill border border-border bg-surface text-text hover:bg-column"
      aria-label={isDark ? 'Switch to light mode' : 'Switch to dark mode'}
      onClick={toggle}
    >
      {isDark ? <SunIcon /> : <MoonIcon />}
    </button>
  )
}
