import { useEffect, useState } from 'react'
import type { FormEvent } from 'react'

import { describeErrorCode } from '../../api/errorMessages'
import { Button } from '../../shared/components/Button'
import { fieldErrorMap } from '../../shared/lib/fieldErrors'
import { userLabel } from '../../shared/lib/userLabel'

import { AvatarUploadCard } from './components/AvatarUploadCard'
import { useChangeMyPasswordMutation } from './hooks/useChangeMyPasswordMutation'
import { useMeQuery } from './hooks/useMeQuery'
import { useUpdateMeMutation } from './hooks/useUpdateMeMutation'
import { ApiError } from '../../api/client'

const fieldClass = 'flex flex-col gap-[var(--space-1)]'
const labelClass = 'text-[length:var(--font-size-sm)] font-medium text-text-muted'
const inputClass =
  'min-h-10 rounded-md border border-border bg-surface px-[var(--space-3)] text-text [font:inherit] disabled:cursor-not-allowed disabled:opacity-60 focus:outline-2 focus:outline-offset-2 focus:outline-focus'
const fieldErrorClass = 'm-0 text-[length:var(--font-size-xs)] text-danger'
const sectionClass = 'flex flex-col gap-[var(--space-3)] rounded-xl border border-border bg-surface-raised p-[var(--space-5)]'

/**
 * Self-service profile (`docs/features/f08-accounts-profile.md` "Self API").
 * Display name and avatar only, plus password change since `PUT /me/password`
 * is a real, already-used endpoint for exactly this (confirmed in
 * `ProfileController.ChangePassword`, which reads the caller's id from
 * claims — never an admin-side reset). No image cropping: a client-side
 * type/size pre-check and a local preview only, no new dependency — the
 * server never trusts the client's declared type anyway (it sniffs magic
 * bytes) and does the real 2 MiB enforcement itself.
 */
export function ProfilePage() {
  const meQuery = useMeQuery()
  const updateMeMutation = useUpdateMeMutation()
  const changePasswordMutation = useChangeMyPasswordMutation()

  const [displayName, setDisplayName] = useState('')
  const [displayNameError, setDisplayNameError] = useState<string | null>(null)
  const [displayNameSaved, setDisplayNameSaved] = useState(false)

  const [currentPassword, setCurrentPassword] = useState('')
  const [newPassword, setNewPassword] = useState('')
  const [passwordError, setPasswordError] = useState<string | null>(null)
  const [passwordFieldErrors, setPasswordFieldErrors] = useState<Record<string, string>>({})
  const [passwordSaved, setPasswordSaved] = useState(false)

  useEffect(() => {
    if (meQuery.data !== undefined) {
      setDisplayName(meQuery.data.displayName ?? '')
    }
  }, [meQuery.data])

  function handleDisplayNameSubmit(event: FormEvent) {
    event.preventDefault()
    setDisplayNameError(null)
    setDisplayNameSaved(false)
    const trimmed = displayName.trim()

    updateMeMutation.mutate(trimmed.length === 0 ? null : trimmed, {
      onSuccess: () => setDisplayNameSaved(true),
      onError: (error) => {
        const code = error instanceof ApiError ? error.errorCode : null
        setDisplayNameError(describeErrorCode(code))
      },
    })
  }

  function handlePasswordSubmit(event: FormEvent) {
    event.preventDefault()
    setPasswordError(null)
    setPasswordFieldErrors({})
    setPasswordSaved(false)

    changePasswordMutation.mutate(
      { currentPassword, newPassword },
      {
        onSuccess: () => {
          setCurrentPassword('')
          setNewPassword('')
          setPasswordSaved(true)
        },
        onError: (error) => {
          setCurrentPassword('')
          setNewPassword('')

          if (error instanceof ApiError) {
            setPasswordFieldErrors(fieldErrorMap(error.validates))
            setPasswordError(describeErrorCode(error.errorCode))
            return
          }

          setPasswordError('Could not reach the server. Try again in a moment.')
        },
      },
    )
  }

  if (meQuery.isPending) {
    return (
      <p role="status" className="text-[length:var(--font-size-sm)] text-text-muted">
        Loading profile…
      </p>
    )
  }

  if (meQuery.isError) {
    return (
      <p role="alert" className="text-[length:var(--font-size-sm)] text-danger">
        Your profile could not be loaded. Try again in a moment.
      </p>
    )
  }

  const me = meQuery.data

  return (
    <section className="mx-auto flex w-full max-w-[640px] flex-col gap-[var(--space-5)]">
      <h1 className="m-0 text-[length:var(--font-size-display)] font-semibold">Profile</h1>

      <AvatarUploadCard userId={me.id} name={userLabel(me)} avatarUrl={me.avatarUrl} />

      <form className={sectionClass} onSubmit={handleDisplayNameSubmit}>
        <h2 className="m-0 text-[length:var(--font-size-lg)] font-semibold">Display name</h2>
        <div className={fieldClass}>
          <label htmlFor="profile-display-name" className={labelClass}>
            Shown on the board, in history and anywhere you're assigned
          </label>
          <input
            id="profile-display-name"
            type="text"
            maxLength={100}
            className={inputClass}
            value={displayName}
            placeholder={me.userName}
            disabled={updateMeMutation.isPending}
            onChange={(event) => {
              setDisplayName(event.target.value)
              setDisplayNameSaved(false)
            }}
          />
        </div>
        {displayNameError !== null && (
          <p role="alert" className={fieldErrorClass}>
            {displayNameError}
          </p>
        )}
        {displayNameSaved && (
          <p role="status" className="m-0 text-[length:var(--font-size-xs)] text-success">
            Saved.
          </p>
        )}
        <Button type="submit" variant="primary" className="self-start" loading={updateMeMutation.isPending}>
          {updateMeMutation.isPending ? 'Saving…' : 'Save'}
        </Button>
      </form>

      <form className={sectionClass} onSubmit={handlePasswordSubmit}>
        <h2 className="m-0 text-[length:var(--font-size-lg)] font-semibold">Password</h2>
        <div className={fieldClass}>
          <label htmlFor="profile-current-password" className={labelClass}>
            Current password
          </label>
          <input
            id="profile-current-password"
            type="password"
            autoComplete="current-password"
            className={inputClass}
            value={currentPassword}
            required
            disabled={changePasswordMutation.isPending}
            onChange={(event) => setCurrentPassword(event.target.value)}
          />
        </div>
        <div className={fieldClass}>
          <label htmlFor="profile-new-password" className={labelClass}>
            New password
          </label>
          <input
            id="profile-new-password"
            type="password"
            autoComplete="new-password"
            minLength={10}
            className={inputClass}
            value={newPassword}
            required
            disabled={changePasswordMutation.isPending}
            onChange={(event) => setNewPassword(event.target.value)}
          />
          {passwordFieldErrors.newpassword !== undefined && <p className={fieldErrorClass}>{passwordFieldErrors.newpassword}</p>}
        </div>
        {passwordError !== null && (
          <p role="alert" className={fieldErrorClass}>
            {passwordError}
          </p>
        )}
        {passwordSaved && (
          <p role="status" className="m-0 text-[length:var(--font-size-xs)] text-success">
            Password changed.
          </p>
        )}
        <Button type="submit" variant="primary" className="self-start" loading={changePasswordMutation.isPending}>
          {changePasswordMutation.isPending ? 'Changing…' : 'Change password'}
        </Button>
      </form>
    </section>
  )
}
