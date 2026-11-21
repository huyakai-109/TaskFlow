import { request } from './client'
import type { AccountRole, ResultRes, UserAdmin, UserListItem } from './types'

/**
 * `GET api/v1/users` (`docs/features/f02-capacity.md` T4, slice 3.3) — the
 * assignee picker's roster: everyone, not only people already visible on the
 * board (`useKnownAssignees`'s stopgap, slice 1.7, retired by this slice).
 */
export async function getUsers(signal?: AbortSignal): Promise<UserListItem[]> {
  const envelope = await request<ResultRes<UserListItem[]>>('/users', { signal })
  return envelope.result ?? []
}

/**
 * The account-administration surface (`docs/features/f08-accounts-profile.md`
 * "Admin API", `UsersController`, policy `ManageUsers` — every one of these
 * 403s server-side for anyone but Admin, same as the nav/route gating on the
 * client being presentation only, never the boundary).
 */
export interface CreateUserInput {
  userName: string
  displayName: string | null
  role: AccountRole
}

export interface CreatedUser {
  user: UserAdmin
  temporaryPassword: string
}

export async function getUserDetail(id: number, signal?: AbortSignal): Promise<UserAdmin> {
  const envelope = await request<ResultRes<UserAdmin>>(`/users/${id}`, { signal })

  if (envelope.result == null) {
    throw new Error(`GET /users/${id} returned no result`)
  }

  return envelope.result
}

export async function createUser(input: CreateUserInput): Promise<CreatedUser> {
  const envelope = await request<ResultRes<CreatedUser>>('/users', { method: 'POST', body: input })

  if (envelope.result == null) {
    throw new Error('POST /users returned no result')
  }

  return envelope.result
}

export async function changeUserRole(id: number, role: AccountRole): Promise<UserAdmin> {
  const envelope = await request<ResultRes<UserAdmin>>(`/users/${id}/role`, { method: 'PUT', body: { role } })

  if (envelope.result == null) {
    throw new Error(`PUT /users/${id}/role returned no result`)
  }

  return envelope.result
}

export async function deactivateUser(id: number): Promise<UserAdmin> {
  const envelope = await request<ResultRes<UserAdmin>>(`/users/${id}/deactivate`, { method: 'PUT' })

  if (envelope.result == null) {
    throw new Error(`PUT /users/${id}/deactivate returned no result`)
  }

  return envelope.result
}

export async function activateUser(id: number): Promise<UserAdmin> {
  const envelope = await request<ResultRes<UserAdmin>>(`/users/${id}/activate`, { method: 'PUT' })

  if (envelope.result == null) {
    throw new Error(`PUT /users/${id}/activate returned no result`)
  }

  return envelope.result
}

/** Returns the one-time temporary password — never stored beyond the dialog's own local state, never logged. */
export async function resetUserPassword(id: number): Promise<string> {
  const envelope = await request<ResultRes<{ temporaryPassword: string }>>(`/users/${id}/password-reset`, { method: 'PUT' })

  if (envelope.result == null) {
    throw new Error(`PUT /users/${id}/password-reset returned no result`)
  }

  return envelope.result.temporaryPassword
}
