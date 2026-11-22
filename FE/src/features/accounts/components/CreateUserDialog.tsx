import { useRef, useState } from 'react'
import type { FormEvent } from 'react'

import { ApiError } from '../../../api/client'
import { describeErrorCode } from '../../../api/errorMessages'
import type { AccountRole } from '../../../api/types'
import { Button } from '../../../shared/components/Button'
import { useDialogA11y } from '../../../shared/hooks/useDialogA11y'
import { fieldErrorMap } from '../../../shared/lib/fieldErrors'

import { useCreateUserMutation } from '../hooks/useAccountMutations'
import type { CreatedUser } from '../../../api/users'

const ROLES: AccountRole[] = ['Member', 'Manager', 'Admin']

interface CreateUserDialogProps {
  onClose: () => void
  onCreated: (created: CreatedUser) => void
}

/** `POST /users` (`f08-accounts-profile.md` "Admin API") — `userName` is immutable once created, no email collected (B9). */
export function CreateUserDialog({ onClose, onCreated }: CreateUserDialogProps) {
  const [userName, setUserName] = useState('')
  const [displayName, setDisplayName] = useState('')
  const [role, setRole] = useState<AccountRole>('Member')
  const [formError, setFormError] = useState<string | null>(null)
  const [fieldErrors, setFieldErrors] = useState<Record<string, string>>({})
  const usernameRef = useRef<HTMLInputElement>(null)
  const mutation = useCreateUserMutation()
  useDialogA11y(usernameRef, onClose, mutation.isPending)

  function handleSubmit(event: FormEvent) {
    event.preventDefault()
    setFormError(null)
    setFieldErrors({})

    mutation.mutate(
      { userName: userName.trim(), displayName: displayName.trim().length === 0 ? null : displayName.trim(), role },
      {
        onSuccess: (created) => onCreated(created),
        onError: (error) => {
          if (error instanceof ApiError) {
            setFieldErrors(fieldErrorMap(error.validates))
            setFormError(describeErrorCode(error.errorCode))
            return
          }

          setFormError('Could not reach the server. Try again in a moment.')
        },
      },
    )
  }

  return (
    <div className="fixed inset-0 z-[100] flex items-center justify-center bg-overlay p-[var(--space-4)]" onClick={() => !mutation.isPending && onClose()}>
      <form
        role="dialog"
        aria-modal="true"
        aria-labelledby="create-user-title"
        className="flex w-full max-w-[440px] flex-col gap-[var(--space-3)] rounded-xl bg-surface-raised p-[var(--space-5)] shadow-raised"
        onClick={(event) => event.stopPropagation()}
        onSubmit={handleSubmit}
      >
        <h2 id="create-user-title" className="m-0 text-[length:var(--font-size-lg)] font-semibold">
          New account
        </h2>

        {formError !== null && (
          <p role="alert" className="m-0 text-[length:var(--font-size-sm)] text-danger">
            {formError}
          </p>
        )}

        <div className="flex flex-col gap-[var(--space-1)]">
          <label htmlFor="create-user-username" className="text-[length:var(--font-size-sm)] font-medium text-text-muted">
            Username
          </label>
          <input
            id="create-user-username"
            ref={usernameRef}
            type="text"
            required
            pattern="[A-Za-z0-9._-]{3,50}"
            title="3-50 characters: letters, numbers, dot, underscore or hyphen"
            className="min-h-10 rounded-md border border-border bg-surface px-[var(--space-3)] text-text [font:inherit] disabled:opacity-60"
            value={userName}
            disabled={mutation.isPending}
            onChange={(event) => setUserName(event.target.value)}
          />
          {fieldErrors.username !== undefined && <p className="m-0 text-[length:var(--font-size-xs)] text-danger">{fieldErrors.username}</p>}
        </div>

        <div className="flex flex-col gap-[var(--space-1)]">
          <label htmlFor="create-user-display-name" className="text-[length:var(--font-size-sm)] font-medium text-text-muted">
            Display name (optional)
          </label>
          <input
            id="create-user-display-name"
            type="text"
            maxLength={100}
            className="min-h-10 rounded-md border border-border bg-surface px-[var(--space-3)] text-text [font:inherit] disabled:opacity-60"
            value={displayName}
            disabled={mutation.isPending}
            onChange={(event) => setDisplayName(event.target.value)}
          />
        </div>

        <div className="flex flex-col gap-[var(--space-1)]">
          <label htmlFor="create-user-role" className="text-[length:var(--font-size-sm)] font-medium text-text-muted">
            Role
          </label>
          <select
            id="create-user-role"
            className="min-h-10 rounded-md border border-border bg-surface px-[var(--space-3)] text-text [font:inherit] disabled:opacity-60"
            value={role}
            disabled={mutation.isPending}
            onChange={(event) => setRole(event.target.value as AccountRole)}
          >
            {ROLES.map((option) => (
              <option key={option} value={option}>
                {option}
              </option>
            ))}
          </select>
        </div>

        <div className="flex gap-[var(--space-3)]">
          <Button type="submit" variant="primary" size="lg" loading={mutation.isPending}>
            {mutation.isPending ? 'Creating…' : 'Create account'}
          </Button>
          <Button type="button" size="lg" onClick={onClose} disabled={mutation.isPending}>
            Cancel
          </Button>
        </div>
      </form>
    </div>
  )
}
