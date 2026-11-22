import { useEffect, useState } from 'react'

const QUERY = '(prefers-reduced-motion: reduce)'

function matchesReducedMotion(): boolean {
  return typeof window.matchMedia === 'function' && window.matchMedia(QUERY).matches
}

/**
 * `prefers-reduced-motion` for the one place CSS alone can't cover it:
 * Recharts' own `isAnimationActive` prop (`docs/design/SPEC.md` §11.4). CSS
 * transitions already honour the media query globally (`global.css`); this
 * hook is for JS-driven chart animation specifically, reused across every
 * `features/reports` chart (second+ real use, `coding-standards.md` §2).
 */
export function useReducedMotion(): boolean {
  const [reduced, setReduced] = useState(matchesReducedMotion)

  useEffect(() => {
    if (typeof window.matchMedia !== 'function') {
      return
    }

    const media = window.matchMedia(QUERY)
    const onChange = () => setReduced(media.matches)

    media.addEventListener('change', onChange)
    return () => media.removeEventListener('change', onChange)
  }, [])

  return reduced
}
