import { useMutation } from '@tanstack/react-query'

import { changeMyPassword } from '../../../api/profile'

/** `PUT /me/password` — the signed-in user's own password change (never an admin reset). */
export function useChangeMyPasswordMutation() {
  return useMutation({
    mutationFn: ({ currentPassword, newPassword }: { currentPassword: string; newPassword: string }) =>
      changeMyPassword(currentPassword, newPassword),
  })
}
