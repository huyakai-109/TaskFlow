import { useMutation, useQueryClient } from '@tanstack/react-query'

import { ApiError } from '../../../api/client'
import { uploadMyAvatar } from '../../../api/profile'
import { ME_QUERY_KEY } from './useMeQuery'

export function useUploadAvatarMutation(options: { onRefused?: (code: string | null) => void } = {}) {
  const queryClient = useQueryClient()

  return useMutation({
    mutationFn: (file: File) => uploadMyAvatar(file),
    onSuccess: (avatarUrl) => {
      queryClient.setQueryData<{ avatarUrl: string | null } | undefined>(ME_QUERY_KEY, (current) =>
        current === undefined ? current : { ...current, avatarUrl },
      )
      void queryClient.invalidateQueries({ queryKey: ['users'] })
    },
    onError: (error) => {
      options.onRefused?.(error instanceof ApiError ? error.errorCode : null)
    },
  })
}
