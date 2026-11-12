import { useEffect, useState } from 'react'

const STORAGE_KEY = 'taskflow.sidebar.collapsed'
const AUTO_COLLAPSE_QUERY = '(max-width: 1099px)'

function readStoredPreference(): boolean | null {
  try {
    const raw = window.localStorage.getItem(STORAGE_KEY)
    return raw === 'true' ? true : raw === 'false' ? false : null
  } catch {
    return null
  }
}

function matchesAutoCollapse(): boolean {
  return typeof window.matchMedia === 'function' && window.matchMedia(AUTO_COLLAPSE_QUERY).matches
}

/**
 * Collapsed/expanded choice for the desktop sidebar rail (`docs/design/SPEC.md`
 * §3 — additive, the wireframe only drew the expanded sidebar; recorded there
 * as a decision, not invented silently).
 *
 * Below 1100px the sidebar auto-collapses to the icon rail, same as a
 * narrower monitor would otherwise cramp the board — *unless* the user has
 * explicitly clicked the collapse/expand toggle at least once, in which case
 * that explicit choice (persisted in `localStorage`) always wins over the
 * viewport-width default, on every width, until they toggle again. Storage
 * access is wrapped in try/catch so a disabled or unavailable store (private
 * browsing, quota) never throws — an explicit choice just won't survive a
 * reload in that case, falling back to the width-based default instead.
 */
export function useSidebarCollapse(): [boolean, (value: boolean | ((prev: boolean) => boolean)) => void] {
  const [preference, setPreferenceState] = useState<boolean | null>(readStoredPreference)
  const [autoCollapsed, setAutoCollapsed] = useState(matchesAutoCollapse)

  useEffect(() => {
    if (typeof window.matchMedia !== 'function') {
      return
    }

    const media = window.matchMedia(AUTO_COLLAPSE_QUERY)
    const onChange = () => setAutoCollapsed(media.matches)

    media.addEventListener('change', onChange)
    return () => media.removeEventListener('change', onChange)
  }, [])

  const collapsed = preference ?? autoCollapsed

  function setCollapsed(value: boolean | ((prev: boolean) => boolean)) {
    setPreferenceState((prev) => {
      const prevEffective = prev ?? autoCollapsed
      const next = typeof value === 'function' ? (value as (p: boolean) => boolean)(prevEffective) : value

      try {
        window.localStorage.setItem(STORAGE_KEY, String(next))
      } catch {
        // storage unavailable — the choice just won't survive a reload
      }

      return next
    })
  }

  return [collapsed, setCollapsed]
}
