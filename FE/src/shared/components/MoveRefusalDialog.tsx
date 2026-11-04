import { useEffect, useRef } from 'react'

import { describeErrorCode } from '../../api/errorMessages'
import { DEFAULT_REFUSAL_TITLE, MOVE_REFUSAL_TITLES } from '../lib/moveRefusalTitles'

interface MoveRefusalDialogProps {
  code: string | null
  taskTitle: string
  onClose: () => void
}

/**
 * The blocked-move dialog (`docs/ui/screens/BlockedActions.dc.html`): names
 * the task and the rule, then offers the one way out — here, just
 * acknowledging it and trying a legal move instead, since the dialog closes
 * onto a board/detail view that already shows the legal moves as buttons.
 * `role="alertdialog"` moves focus in on open and returns it on close
 * (`frontend.md` §8).
 */
export function MoveRefusalDialog({ code, taskTitle, onClose }: MoveRefusalDialogProps) {
  const closeButtonRef = useRef<HTMLButtonElement>(null)
  const previouslyFocused = useRef<Element | null>(null)

  useEffect(() => {
    previouslyFocused.current = document.activeElement
    closeButtonRef.current?.focus()

    function onKeyDown(event: KeyboardEvent) {
      if (event.key === 'Escape') {
        onClose()
      }
    }

    document.addEventListener('keydown', onKeyDown)

    return () => {
      document.removeEventListener('keydown', onKeyDown)
      if (previouslyFocused.current instanceof HTMLElement) {
        previouslyFocused.current.focus()
      }
    }
  }, [onClose])

  const title = (code !== null && MOVE_REFUSAL_TITLES[code]) || DEFAULT_REFUSAL_TITLE

  return (
    <div
      className="fixed inset-0 z-[100] flex items-center justify-center bg-[rgb(16_16_24_/_40%)] p-[var(--space-4)]"
      onClick={onClose}
    >
      <div
        role="alertdialog"
        aria-modal="true"
        aria-labelledby="move-refusal-title"
        aria-describedby="move-refusal-description"
        className="flex max-w-[420px] flex-col gap-[var(--space-3)] rounded-xl bg-surface-raised p-[var(--space-5)] shadow-raised"
        onClick={(event) => event.stopPropagation()}
      >
        <h2 id="move-refusal-title" className="text-[length:var(--font-size-lg)] font-semibold">
          {title}
        </h2>
        <p id="move-refusal-description" className="text-text-muted [line-height:var(--line-height)]">
          &ldquo;{taskTitle}&rdquo; — {describeErrorCode(code)}
        </p>
        <button
          type="button"
          ref={closeButtonRef}
          className="min-h-10 cursor-pointer self-start rounded-md border-0 bg-accent px-[var(--space-4)] font-semibold text-on-accent hover:bg-accent-hover"
          onClick={onClose}
        >
          Close
        </button>
      </div>
    </div>
  )
}
