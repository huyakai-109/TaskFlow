import { useRef } from 'react'

import { Button } from '../../../shared/components/Button'
import { useDialogA11y } from '../../../shared/hooks/useDialogA11y'

interface TemporaryPasswordDialogProps {
  userName: string
  temporaryPassword: string
  onClose: () => void
}

/**
 * Shown exactly once, right after `POST /users` or `PUT
 * /users/{id}/password-reset` (`f08-accounts-profile.md` "Admin API" — both
 * responses are `Cache-Control: no-store` server-side). The password lives
 * only in this dialog's own props/closure — never written to
 * localStorage/sessionStorage, never logged, and gone the moment this
 * unmounts (the parent holds no state for it beyond what opened the dialog).
 */
export function TemporaryPasswordDialog({ userName, temporaryPassword, onClose }: TemporaryPasswordDialogProps) {
  const closeButtonRef = useRef<HTMLButtonElement>(null)
  useDialogA11y(closeButtonRef, onClose)

  return (
    <div className="fixed inset-0 z-[100] flex items-center justify-center bg-overlay p-[var(--space-4)]" onClick={onClose}>
      <div
        role="dialog"
        aria-modal="true"
        aria-labelledby="temp-password-title"
        className="flex w-full max-w-[440px] flex-col gap-[var(--space-3)] rounded-xl bg-surface-raised p-[var(--space-5)] shadow-raised"
        onClick={(event) => event.stopPropagation()}
      >
        <h2 id="temp-password-title" className="m-0 text-[length:var(--font-size-lg)] font-semibold">
          Temporary password for {userName}
        </h2>
        <p className="m-0 text-[length:var(--font-size-sm)] text-text-muted">
          Shown once — it is not stored anywhere else. {userName} must change it on first sign-in.
        </p>
        <code className="rounded-md border border-border bg-column p-[var(--space-3)] text-[length:var(--font-size-md)] font-semibold select-all">
          {temporaryPassword}
        </code>
        <Button ref={closeButtonRef} type="button" variant="primary" size="lg" className="self-start" onClick={onClose}>
          Done
        </Button>
      </div>
    </div>
  )
}
