import { useMutation } from '@tanstack/react-query'

import type { LoginInput } from '../../../api/auth'
import { login } from '../../../api/auth'
import { setSession } from '../../../shared/lib/authSession'

/** `POST /auth/login`, mirrors `useCreateTaskMutation`'s shape. */
export function useLoginMutation() {
  return useMutation({
    mutationFn: (input: LoginInput) => login(input),
    onSuccess: ({ permissions }) => {
      setSession(permissions)
    },
  })
}
