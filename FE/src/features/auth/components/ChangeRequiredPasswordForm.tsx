import { useState } from 'react'
import type { FormEvent } from 'react'

import { ApiError } from '../../../api/client'
import { describeErrorCode } from '../../../api/errorMessages'
import { fieldErrorMap } from '../../../shared/lib/fieldErrors'
import { useChangeRequiredPasswordMutation } from '../hooks/useChangeRequiredPasswordMutation'

const fieldClass = 'flex flex-col gap-[var(--space-1)]'
const labelClass = 'text-[length:var(--font-size-sm)] font-medium text-text-muted'
const inputClass =
  'min-h-10 rounded-md border border-border bg-surface px-[var(--space-3)] text-text [font:inherit] disabled:cursor-not-allowed disabled:opacity-60 focus:outline-2 focus:outline-offset-2 focus:outline-focus'
const fieldErrorClass = 'm-0 text-[length:var(--font-size-xs)] text-danger'

interface ChangeRequiredPasswordFormProps {
  username: string
  /** The password that triggered `Auth.Login.PasswordChangeRequired` — needed as `currentPassword` for the change-required call, never shown again after this screen unmounts. */
  currentPassword: string
  onCancel: () => void
  /** Called once the server accepted the new password; the parent re-runs login with it. Receiving component clears its own fields immediately afterwards — see the `onSubmit` handler below. */
  onChanged: (newPassword: string) => void
}

/**
 * `POST /auth/password/change-required` (`docs/features/f08-accounts-profile.md`
 * "Auth changes") — shown in place of the ordinary login form once
 * `Auth.Login.PasswordChangeRequired` comes back from `/auth/login`. Never
 * persists any of these three values beyond this one request: no
 * `localStorage`/`sessionStorage`, no `console.log`, and the local state
 * holding them is cleared the instant the request settles (success or
 * failure) rather than lingering in a closed-over value.
 */
export function ChangeRequiredPasswordForm({ username, currentPassword, onCancel, onChanged }: ChangeRequiredPasswordFormProps) {
  const [newPassword, setNewPassword] = useState('')
  const [confirmPassword, setConfirmPassword] = useState('')
  const [formError, setFormError] = useState<string | null>(null)
  const [fieldErrors, setFieldErrors] = useState<Record<string, string>>({})
  const mutation = useChangeRequiredPasswordMutation()

  function handleSubmit(event: FormEvent) {
    event.preventDefault()
    setFormError(null)
    setFieldErrors({})

    if (newPassword !== confirmPassword) {
      setFieldErrors({ newpassword: 'The two passwords do not match.' })
      return
    }

    mutation.mutate(
      { username, currentPassword, newPassword },
      {
        onSuccess: () => {
          const toUse = newPassword
          // Clear every credential field immediately — nothing survives this
          // callback beyond the one value the parent needs to log in with.
          setNewPassword('')
          setConfirmPassword('')
          onChanged(toUse)
        },
        onError: (error) => {
          setNewPassword('')
          setConfirmPassword('')

          if (error instanceof ApiError) {
            setFieldErrors(fieldErrorMap(error.validates))
            setFormError(describeErrorCode(error.errorCode))
            return
          }

          setFormError('Could not reach the server. Check your connection and try again.')
        },
      },
    )
  }

  return (
    <form
      className="flex w-full max-w-[360px] flex-col gap-[var(--space-4)] rounded-xl bg-surface-raised p-[var(--space-5)] shadow-raised"
      onSubmit={handleSubmit}
    >
      <div className="flex flex-col gap-[var(--space-1)]">
        <h1 className="m-0 text-[length:var(--font-size-xl)] font-semibold">Set a new password</h1>
        <p className="m-0 text-[length:var(--font-size-sm)] text-text-muted">
          Your password must be changed before you can sign in.
        </p>
      </div>

      {formError !== null && (
        <p role="alert" className="m-0 rounded-md border border-danger-border bg-danger-surface p-[var(--space-3)] text-[length:var(--font-size-sm)] text-danger">
          {formError}
        </p>
      )}

      <div className={fieldClass}>
        <label htmlFor="change-required-new-password" className={labelClass}>
          New password
        </label>
        <input
          id="change-required-new-password"
          type="password"
          autoComplete="new-password"
          className={inputClass}
          value={newPassword}
          required
          minLength={10}
          disabled={mutation.isPending}
          onChange={(event) => setNewPassword(event.target.value)}
        />
        {fieldErrors.newpassword !== undefined && <p className={fieldErrorClass}>{fieldErrors.newpassword}</p>}
      </div>

      <div className={fieldClass}>
        <label htmlFor="change-required-confirm-password" className={labelClass}>
          Confirm new password
        </label>
        <input
          id="change-required-confirm-password"
          type="password"
          autoComplete="new-password"
          className={inputClass}
          value={confirmPassword}
          required
          minLength={10}
          disabled={mutation.isPending}
          onChange={(event) => setConfirmPassword(event.target.value)}
        />
      </div>

      <button
        type="submit"
        className="min-h-11 cursor-pointer rounded-md border-0 bg-accent px-[var(--space-4)] font-semibold text-on-accent disabled:cursor-not-allowed disabled:opacity-60"
        disabled={mutation.isPending}
      >
        {mutation.isPending ? 'Changing password…' : 'Change password and sign in'}
      </button>
      <button
        type="button"
        className="min-h-9 cursor-pointer border-0 bg-transparent text-[length:var(--font-size-sm)] text-text-muted underline"
        disabled={mutation.isPending}
        onClick={onCancel}
      >
        Back to sign in
      </button>
    </form>
  )
}
