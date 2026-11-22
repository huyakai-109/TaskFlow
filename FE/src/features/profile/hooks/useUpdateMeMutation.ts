import { useMutation, useQueryClient } from '@tanstack/react-query'

import { ApiError } from '../../../api/client'
import { updateMe } from '../../../api/profile'
import { ME_QUERY_KEY } from './useMeQuery'

/** `PUT /me` — display name only. */
export function useUpdateMeMutation(options: { onRefused?: (code: string | null) => void } = {}) {
  const queryClient = useQueryClient()

  return useMutation({
    mutationFn: (displayName: string | null) => updateMe(displayName),
    onSuccess: (me) => {
      queryClient.setQueryData(ME_QUERY_KEY, me)
      // The sidebar footer and every board card reading this user's name
      // from the `GET /users` roster must pick up the new display name too.
      void queryClient.invalidateQueries({ queryKey: ['users'] })
    },
    onError: (error) => {
      options.onRefused?.(error instanceof ApiError ? error.errorCode : null)
    },
  })
}
