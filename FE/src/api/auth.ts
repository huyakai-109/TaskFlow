import { getDeviceGuid } from './deviceGuid'
import { request, setAccessToken } from './client'
import type { ResultRes } from './types'

/** Body of `POST /auth/login` (`LoginReq`), minus `deviceGuid` — assembled here, not by the caller. */
export interface LoginInput {
  username: string
  password: string
}

export interface LoginResult {
  permissions: string[]
}

/**
 * `POST /auth/login` (`docs/features/f01-login.md` "Contract used"). Sets the
 * access token in memory on success (R1) and returns the server's
 * `permissions[]` for `authSession` to store. A failed login never reaches
 * here as a normal return — `request()` throws `ApiError` on `success: false`,
 * carrying the raw `error` string the login screen maps through
 * `describeLoginError`.
 */
export async function login(input: LoginInput): Promise<LoginResult> {
  const envelope = await request<ResultRes<{ token: string | null; permissions: string[] }>>('/auth/login', {
    method: 'POST',
    body: { ...input, deviceGuid: getDeviceGuid() },
  })

  if (envelope.result?.token == null) {
    throw new Error('Login returned no token')
  }

  setAccessToken(envelope.result.token)

  return { permissions: envelope.result.permissions }
}

/** Body of `POST /auth/password/change-required` (`ChangeRequiredPasswordReq`). */
export interface ChangeRequiredPasswordInput {
  username: string
  currentPassword: string
  newPassword: string
}

/**
 * `POST /auth/password/change-required` (`docs/features/f08-accounts-profile.md`
 * "Auth changes") — `[AllowAnonymous]`, lockout-protected. Succeeds only when
 * the password is right **and** the account's `MustChangePassword` flag is
 * set; clears the flag. Issues no token itself — the caller logs in again
 * with the new password afterwards, same two-step flow the server specifies.
 * Never logs or stores any of the three fields beyond this one call.
 */
export async function changeRequiredPassword(input: ChangeRequiredPasswordInput): Promise<void> {
  await request('/auth/password/change-required', {
    method: 'POST',
    body: { userName: input.username, currentPassword: input.currentPassword, newPassword: input.newPassword },
  })
}
