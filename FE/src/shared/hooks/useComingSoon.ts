import { useCallback, useState } from 'react'

/**
 * State for a `ComingSoon` toast (`FE/src/shared/components/ComingSoon.tsx`).
 * One instance per screen that has "Coming soon" controls — `show()` never
 * calls an API or changes any other state, per `docs/design/COMING-SOON.md`'s
 * rule for a category-B stub.
 */
export function useComingSoon(defaultMessage = 'Coming soon') {
  const [message, setMessage] = useState<string | null>(null)

  const show = useCallback((custom?: string) => setMessage(custom ?? defaultMessage), [defaultMessage])
  const hide = useCallback(() => setMessage(null), [])

  return { message, show, hide }
}
