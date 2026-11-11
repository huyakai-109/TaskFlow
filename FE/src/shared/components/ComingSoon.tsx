import { useEffect } from 'react'

interface ComingSoonProps {
  message: string
  onDismiss: () => void
}

/**
 * The small, non-blocking notice every "Coming soon" control shows
 * (`docs/design/COMING-SOON.md`'s category-B rule: the control stays visible,
 * disabled in effect, calls no API, changes no state — this is the only
 * reaction a click gets). `role="status"` (not `alert`): it's informational,
 * not an error, so it's announced without interrupting. Auto-hides; never
 * blocks the page behind it, unlike `MoveToast`'s `role="alert"` pattern it
 * otherwise mirrors.
 */
export function ComingSoon({ message, onDismiss }: ComingSoonProps) {
  useEffect(() => {
    const timer = window.setTimeout(onDismiss, 2500)
    return () => window.clearTimeout(timer)
  }, [onDismiss])

  return (
    <div
      role="status"
      className="fixed bottom-[var(--space-5)] left-1/2 z-[100] -translate-x-1/2 rounded-pill bg-text px-[var(--space-4)] py-[var(--space-3)] text-text-inverse shadow-raised"
    >
      {message}
    </div>
  )
}
