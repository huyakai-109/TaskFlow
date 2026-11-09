import { useEffect } from 'react'

interface MoveToastProps {
  message: string
  onDismiss: () => void
}

/**
 * The dark pill toast `docs/design/SPEC.md` calls for ("a short confirmation"), reused
 * here for a conflict/not-found move: the board or task view has already
 * refetched by the time this shows, so it is a notice, not a blocking dialog.
 */
export function MoveToast({ message, onDismiss }: MoveToastProps) {
  useEffect(() => {
    const timer = window.setTimeout(onDismiss, 6000)
    return () => window.clearTimeout(timer)
  }, [onDismiss])

  return (
    <div
      className="fixed bottom-[var(--space-5)] left-1/2 z-[100] flex max-w-[420px] -translate-x-1/2 items-center gap-[var(--space-3)] rounded-pill bg-text px-[var(--space-4)] py-[var(--space-3)] text-text-inverse shadow-raised"
      role="alert"
    >
      <span>{message}</span>
      <button
        type="button"
        className="inline-flex size-5 shrink-0 cursor-pointer items-center justify-center rounded-pill border-0 bg-transparent text-inherit"
        onClick={onDismiss}
        aria-label="Dismiss"
      >
        <svg aria-hidden="true" width="12" height="12" viewBox="0 0 16 16" fill="none" stroke="currentColor" strokeWidth="1.8" strokeLinecap="round">
          <path d="M3 3l10 10M13 3 3 13" />
        </svg>
      </button>
    </div>
  )
}
