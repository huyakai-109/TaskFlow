import { useEffect, useRef } from 'react'
import type { RefObject } from 'react'

/**
 * The focus-on-open / Esc-to-close / focus-return pattern `ReopenDialog` and
 * `MoveRefusalDialog` (slice 1.6) each wrote inline. Slice 1.7 adds a third,
 * fourth and fifth dialog that need the same thing, so it is extracted here
 * (coding-standards.md: an abstraction at the second real use, not the
 * first) rather than re-copied a third time. The two 1.6 dialogs are left as
 * they are — no unrelated refactor.
 */
export function useDialogA11y(initialFocusRef: RefObject<HTMLElement | null>, onClose: () => void, isBusy = false): void {
  const previouslyFocused = useRef<Element | null>(null)

  // Latest callback/flag read from the keydown handler, so the mount effect
  // that sets up focus management can run exactly once.
  const onCloseRef = useRef(onClose)
  const isBusyRef = useRef(isBusy)

  useEffect(() => {
    onCloseRef.current = onClose
    isBusyRef.current = isBusy
  })

  useEffect(() => {
    previouslyFocused.current = document.activeElement
    initialFocusRef.current?.focus()

    function onKeyDown(event: KeyboardEvent) {
      if (event.key === 'Escape' && !isBusyRef.current) {
        onCloseRef.current()
      }
    }

    document.addEventListener('keydown', onKeyDown)

    return () => {
      document.removeEventListener('keydown', onKeyDown)
      if (previouslyFocused.current instanceof HTMLElement) {
        previouslyFocused.current.focus()
      }
    }
  }, [])
}
