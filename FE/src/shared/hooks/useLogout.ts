import { useQueryClient } from '@tanstack/react-query'
import { useNavigate } from 'react-router-dom'

import { setAccessToken } from '../../api/client'
import { clearSession } from '../lib/authSession'

/**
 * Client-only logout (R5, Q3): no `POST /auth/logout` exists, so this forgets
 * the in-memory token and the client's own notion of the session but cannot
 * revoke the `UserToken`/refresh cookie server-side. Also clears the query
 * cache so a different user logging in next doesn't see a flash of stale
 * board data (AC11).
 */
export function useLogout(): () => void {
  const navigate = useNavigate()
  const queryClient = useQueryClient()

  return () => {
    setAccessToken(null)
    clearSession()
    queryClient.clear()
    navigate('/login', { replace: true })
  }
}
