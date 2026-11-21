import { useMutation, useQueryClient } from '@tanstack/react-query'

import { activateUser, changeUserRole, createUser, deactivateUser, resetUserPassword } from '../../../api/users'
import type { AccountRole } from '../../../api/types'

/** Every mutation here invalidates `['users']` on success — the same roster `GET /users` feeds this table, the assignee pickers and every avatar lookup. */
function useInvalidateUsers() {
  const queryClient = useQueryClient()
  return () => void queryClient.invalidateQueries({ queryKey: ['users'] })
}

export function useCreateUserMutation() {
  const invalidate = useInvalidateUsers()
  return useMutation({
    mutationFn: createUser,
    onSuccess: invalidate,
  })
}

export function useChangeUserRoleMutation() {
  const invalidate = useInvalidateUsers()
  return useMutation({
    mutationFn: ({ id, role }: { id: number; role: AccountRole }) => changeUserRole(id, role),
    onSuccess: invalidate,
  })
}

export function useDeactivateUserMutation() {
  const invalidate = useInvalidateUsers()
  return useMutation({
    mutationFn: (id: number) => deactivateUser(id),
    onSuccess: invalidate,
  })
}

export function useActivateUserMutation() {
  const invalidate = useInvalidateUsers()
  return useMutation({
    mutationFn: (id: number) => activateUser(id),
    onSuccess: invalidate,
  })
}

/** Returns the one-time temporary password to the caller (shown once in a dialog); never cached, never logged. */
export function useResetUserPasswordMutation() {
  return useMutation({
    mutationFn: (id: number) => resetUserPassword(id),
  })
}
