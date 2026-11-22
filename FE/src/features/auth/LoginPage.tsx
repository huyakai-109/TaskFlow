import { useState } from 'react'
import type { FormEvent } from 'react'
import { Navigate, useLocation } from 'react-router-dom'

import { ApiError } from '../../api/client'
import { describeLoginError, LOGIN_INVALID_CREDENTIAL, LOGIN_WILL_LOCK_OUT } from '../../api/errorMessages'
import { Button } from '../../shared/components/Button'
import { useAuthSession } from '../../shared/hooks/useAuthSession'
import { consumeLogoutReason } from '../../shared/lib/authSession'

import { ChangeRequiredPasswordForm } from './components/ChangeRequiredPasswordForm'
import { useLoginMutation } from './hooks/useLoginMutation'

const PASSWORD_CHANGE_REQUIRED = 'Auth.Login.PasswordChangeRequired'

// Field/label/button classes copied from `NewTaskDialog.tsx`, the closest
// existing form (docs/features/f01-login.md "UI" — no wireframe exists yet).
const fieldClass = 'flex flex-col gap-[var(--space-1)]'
const labelClass = 'text-[length:var(--font-size-sm)] font-medium text-text-muted'
const inputClass =
  'min-h-10 rounded-md border border-border bg-surface px-[var(--space-3)] text-text [font:inherit] disabled:cursor-not-allowed disabled:opacity-60 focus:outline-2 focus:outline-offset-2 focus:outline-focus'

interface LocationState {
  from?: string
}

/**
 * The login screen (`docs/features/f01-login.md` AC1–5, AC12). Layout is a
 * new design call built entirely from existing tokens and `NewTaskDialog`'s
 * form patterns — `docs/design/SPEC.md` lists login under "Not designed yet."
 */
export function LoginPage() {
  const location = useLocation()
  const { status } = useAuthSession()
  const [username, setUsername] = useState('')
  const [password, setPassword] = useState('')
  const [serverError, setServerError] = useState<string | null>(null)
  const [errorTone, setErrorTone] = useState<'danger' | 'warning'>('danger')
  // Set only for the span between `Auth.Login.PasswordChangeRequired` and
  // either cancelling or a successful change — `currentPassword` here is the
  // same value `password` held when the login attempt failed, carried across
  // just long enough for `ChangeRequiredPasswordForm`'s one request, then
  // cleared (never logged, never persisted).
  const [changeRequired, setChangeRequired] = useState<{ username: string; currentPassword: string } | null>(null)
  // Item 8: a refresh failure (token refresh rejected, e.g. the account was
  // deactivated, or any ordinary expiry) logs the user out explicitly with
  // this one generic message — never silently, and never a message that
  // distinguishes *why* (`authSession.ts`'s own doc comment on this field).
  // Read-once via the lazy initializer so a later remount never repeats it.
  const [logoutReason] = useState(() => consumeLogoutReason())

  const mutation = useLoginMutation()

  // AC1: already authenticated (e.g. back button after login) — go straight
  // to the originally requested path, replacing history so it doesn't bounce
  // back here.
  if (status === 'authenticated') {
    const from = (location.state as LocationState | null)?.from ?? '/board'
    return <Navigate to={from} replace />
  }

  function handleSubmit(event: FormEvent) {
    event.preventDefault()
    setServerError(null)

    mutation.mutate(
      { username, password },
      {
        onError: (error) => {
          if (error instanceof ApiError && error.errorCode === PASSWORD_CHANGE_REQUIRED) {
            setChangeRequired({ username, currentPassword: password })
            setPassword('')
            return
          }

          const rawError = error instanceof ApiError ? error.message : null

          setServerError(describeLoginError(rawError))
          setErrorTone(rawError === LOGIN_WILL_LOCK_OUT ? 'warning' : 'danger')

          // AC2: password cleared only on invalid-credentials — retyping the
          // same password is not the fix for either lockout variant.
          if (rawError === LOGIN_INVALID_CREDENTIAL) {
            setPassword('')
          }
        },
      },
    )
  }

  if (changeRequired !== null) {
    return (
      <div className="flex min-h-screen items-center justify-center bg-bg px-[var(--space-4)]">
        <ChangeRequiredPasswordForm
          username={changeRequired.username}
          currentPassword={changeRequired.currentPassword}
          onCancel={() => setChangeRequired(null)}
          onChanged={(newPassword) => {
            // The server issues no token from the change-required call
            // itself — log in again with the password just set, then forget
            // it immediately either way (success moves on via `authSession`;
            // a failure here is unexpected enough to just fall back to the
            // plain sign-in form rather than loop on this screen).
            setChangeRequired(null)
            mutation.mutate(
              { username: changeRequired.username, password: newPassword },
              {
                onError: () => {
                  setServerError('Your password was changed. Sign in with it below.')
                  setErrorTone('warning')
                },
              },
            )
          }}
        />
      </div>
    )
  }

  return (
    <div className="flex min-h-screen items-center justify-center bg-bg px-[var(--space-4)]">
      <form
        className="flex w-full max-w-[360px] flex-col gap-[var(--space-4)] rounded-xl bg-surface-raised p-[var(--space-5)] shadow-raised"
        onSubmit={handleSubmit}
      >
        <div className="flex flex-col gap-[var(--space-1)]">
          <h1 className="m-0 text-[length:var(--font-size-xl)] font-semibold">TaskFlow</h1>
          <p className="m-0 text-[length:var(--font-size-sm)] text-text-muted">Sign in to see the board</p>
        </div>

        {serverError === null && logoutReason !== null && (
          <p role="status" className="m-0 rounded-md border border-border bg-column p-[var(--space-3)] text-[length:var(--font-size-sm)] text-text-muted">
            {logoutReason}
          </p>
        )}

        {serverError !== null && (
          <p
            role="alert"
            className={
              errorTone === 'warning'
                ? 'm-0 rounded-md border border-warning bg-warning-surface p-[var(--space-3)] text-[length:var(--font-size-sm)] text-warning'
                : 'm-0 rounded-md border border-danger-border bg-danger-surface p-[var(--space-3)] text-[length:var(--font-size-sm)] text-danger'
            }
          >
            {serverError}
          </p>
        )}

        <div className={fieldClass}>
          <label htmlFor="login-username" className={labelClass}>
            Username
          </label>
          <input
            id="login-username"
            type="text"
            autoComplete="username"
            className={inputClass}
            value={username}
            required
            disabled={mutation.isPending}
            onChange={(event) => setUsername(event.target.value)}
          />
        </div>

        <div className={fieldClass}>
          <label htmlFor="login-password" className={labelClass}>
            Password
          </label>
          <input
            id="login-password"
            type="password"
            autoComplete="current-password"
            className={inputClass}
            value={password}
            required
            disabled={mutation.isPending}
            onChange={(event) => setPassword(event.target.value)}
          />
        </div>

        <Button type="submit" variant="primary" size="lg" loading={mutation.isPending}>
          {mutation.isPending ? 'Signing in…' : 'Sign in'}
        </Button>
      </form>
    </div>
  )
}
