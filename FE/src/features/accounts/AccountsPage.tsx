import { useState } from 'react'

import { describeError } from '../../api/errorMessages'
import type { AccountRole, UserListItem } from '../../api/types'
import type { CreatedUser } from '../../api/users'
import { Avatar } from '../../shared/components/Avatar'
import { Button } from '../../shared/components/Button'
import { useAuthSession } from '../../shared/hooks/useAuthSession'
import { useUsersQuery } from '../../shared/hooks/useUsersQuery'
import { MoveToast } from '../../shared/components/MoveToast'
import { userLabel } from '../../shared/lib/userLabel'

import { CreateUserDialog } from './components/CreateUserDialog'
import { TemporaryPasswordDialog } from './components/TemporaryPasswordDialog'
import {
  useActivateUserMutation,
  useChangeUserRoleMutation,
  useDeactivateUserMutation,
  useResetUserPasswordMutation,
} from './hooks/useAccountMutations'

const ROLES: AccountRole[] = ['Member', 'Manager', 'Admin']

/**
 * Admin-only account administration (`docs/features/f08-accounts-profile.md`
 * "Admin API"). Reachable only when the token carries `ManageUsers`
 * (`Sidebar`'s nav item and `router.tsx`'s route guard both gate on the same
 * permission, never a role-string check) — every action still 403s
 * server-side regardless, the client gating is presentation only
 * (`frontend.md` §9).
 */
export function AccountsPage() {
  const usersQuery = useUsersQuery()
  const { userId } = useAuthSession()
  const [createOpen, setCreateOpen] = useState(false)
  const [revealed, setRevealed] = useState<{ userName: string; temporaryPassword: string } | null>(null)
  const [toastMessage, setToastMessage] = useState<string | null>(null)
  const [pendingRowId, setPendingRowId] = useState<number | null>(null)

  const changeRoleMutation = useChangeUserRoleMutation()
  const deactivateMutation = useDeactivateUserMutation()
  const activateMutation = useActivateUserMutation()
  const resetPasswordMutation = useResetUserPasswordMutation()

  function handleRoleChange(user: UserListItem, role: AccountRole) {
    setPendingRowId(user.id)
    changeRoleMutation.mutate(
      { id: user.id, role },
      {
        onSettled: () => setPendingRowId(null),
        onError: (error) => setToastMessage(describeError(error)),
      },
    )
  }

  function handleToggleActive(user: UserListItem) {
    setPendingRowId(user.id)
    const mutation = user.isActive ? deactivateMutation : activateMutation

    mutation.mutate(user.id, {
      onSettled: () => setPendingRowId(null),
      onError: (error) => setToastMessage(describeError(error)),
    })
  }

  function handleResetPassword(user: UserListItem) {
    setPendingRowId(user.id)
    resetPasswordMutation.mutate(user.id, {
      onSuccess: (temporaryPassword) => {
        setPendingRowId(null)
        setRevealed({ userName: userLabel(user), temporaryPassword })
      },
      onError: (error) => {
        setPendingRowId(null)
        setToastMessage(describeError(error))
      },
    })
  }

  return (
    <section className="flex flex-col gap-[var(--space-4)]">
      <div className="flex items-center justify-between gap-[var(--space-3)]">
        <h1 className="m-0 text-[length:var(--font-size-display)] font-semibold">Accounts</h1>
        <Button type="button" variant="primary" onClick={() => setCreateOpen(true)}>
          New account
        </Button>
      </div>

      {usersQuery.isPending && (
        <p role="status" className="text-[length:var(--font-size-sm)] text-text-muted">
          Loading accounts…
        </p>
      )}

      {usersQuery.isError && (
        <div role="alert" className="flex flex-col items-start gap-[var(--space-3)] rounded-md border border-danger-border bg-danger-surface p-[var(--space-4)] text-danger">
          <p className="m-0">{describeError(usersQuery.error)}</p>
          <Button type="button" size="sm" onClick={() => usersQuery.refetch()}>
            Try again
          </Button>
        </div>
      )}

      {usersQuery.isSuccess && usersQuery.data.length === 0 && (
        <p className="text-[length:var(--font-size-sm)] text-text-muted">No accounts yet.</p>
      )}

      {usersQuery.isSuccess && usersQuery.data.length > 0 && (
        <table className="w-full table-fixed border-collapse text-[length:var(--font-size-sm)]">
          <thead>
            <tr className="border-b border-border text-left text-text-muted">
              <th className="w-[36%] py-[var(--space-2)] font-medium">Account</th>
              <th className="w-[16%] py-[var(--space-2)] font-medium">Role</th>
              <th className="w-[14%] py-[var(--space-2)] font-medium">Status</th>
              <th className="w-[34%] py-[var(--space-2)] font-medium">Actions</th>
            </tr>
          </thead>
          <tbody>
            {usersQuery.data.map((user) => {
              const isSelf = user.id === userId
              const isRowPending = pendingRowId === user.id

              return (
                <tr key={user.id} className="border-b border-border align-middle">
                  <td className="py-[var(--space-2)]">
                    <span className="flex items-center gap-[var(--space-2)]">
                      <Avatar userId={user.id} name={userLabel(user)} avatarUrl={user.avatarUrl} size={24} />
                      <span className="flex flex-col">
                        <span className="font-medium text-text">{userLabel(user)}</span>
                        <span className="text-[length:var(--font-size-xs)] text-text-muted">@{user.userName}</span>
                      </span>
                    </span>
                  </td>
                  <td className="py-[var(--space-2)]">
                    <select
                      aria-label={`Role for ${user.userName}`}
                      className="min-h-9 rounded-md border border-border bg-surface px-[var(--space-2)] text-text [font:inherit] disabled:opacity-60"
                      value={user.role}
                      disabled={isRowPending}
                      onChange={(event) => handleRoleChange(user, event.target.value as AccountRole)}
                    >
                      {ROLES.map((role) => (
                        <option key={role} value={role}>
                          {role}
                        </option>
                      ))}
                    </select>
                  </td>
                  <td className="py-[var(--space-2)]">
                    <span className={user.isActive ? 'text-text' : 'text-danger'}>{user.isActive ? 'Active' : 'Deactivated'}</span>
                  </td>
                  <td className="py-[var(--space-2)]">
                    <span className="flex flex-wrap gap-[var(--space-2)]">
                      <Button
                        type="button"
                        size="sm"
                        disabled={isRowPending || isSelf}
                        title={isSelf ? 'You cannot deactivate your own account' : undefined}
                        onClick={() => handleToggleActive(user)}
                      >
                        {user.isActive ? 'Deactivate' : 'Reactivate'}
                      </Button>
                      <Button type="button" size="sm" disabled={isRowPending} onClick={() => handleResetPassword(user)}>
                        Reset password
                      </Button>
                    </span>
                  </td>
                </tr>
              )
            })}
          </tbody>
        </table>
      )}

      {createOpen && (
        <CreateUserDialog
          onClose={() => setCreateOpen(false)}
          onCreated={(created: CreatedUser) => {
            setCreateOpen(false)
            setRevealed({ userName: userLabel(created.user), temporaryPassword: created.temporaryPassword })
          }}
        />
      )}

      {revealed !== null && (
        <TemporaryPasswordDialog userName={revealed.userName} temporaryPassword={revealed.temporaryPassword} onClose={() => setRevealed(null)} />
      )}

      {toastMessage !== null && <MoveToast message={toastMessage} onDismiss={() => setToastMessage(null)} />}
    </section>
  )
}
