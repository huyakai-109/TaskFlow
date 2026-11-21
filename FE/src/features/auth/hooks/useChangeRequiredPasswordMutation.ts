import { useMutation } from '@tanstack/react-query'

import type { ChangeRequiredPasswordInput } from '../../../api/auth'
import { changeRequiredPassword } from '../../../api/auth'

/** `POST /auth/password/change-required` — issues no token; the caller logs in again afterwards (`LoginPage`). */
export function useChangeRequiredPasswordMutation() {
  return useMutation({
    mutationFn: (input: ChangeRequiredPasswordInput) => changeRequiredPassword(input),
  })
}
