import { useQuery, useQueryClient } from '@tanstack/react-query'

import { getMe } from '../../../api/profile'

export const ME_QUERY_KEY = ['me'] as const

/** `GET /me` (f08-accounts-profile.md "Self API") — the signed-in user's own profile (display name, role, avatar). */
export function useMeQuery() {
  return useQuery({
    queryKey: ME_QUERY_KEY,
    queryFn: ({ signal }) => getMe(signal),
  })
}

/** For a mutation that already has the server's fresh `Me` to write straight into the cache, instead of waiting on a refetch. */
export function useSetMeCache() {
  const queryClient = useQueryClient()
  return (me: Awaited<ReturnType<typeof getMe>>) => queryClient.setQueryData(ME_QUERY_KEY, me)
}
