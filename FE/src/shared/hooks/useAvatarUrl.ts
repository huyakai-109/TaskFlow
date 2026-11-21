import { useEffect, useState } from 'react'

import { fetchAuthenticatedBlob } from '../../api/client'

interface CacheEntry {
  blobUrl: string
  refCount: number
}

/**
 * Avatars are served through an authenticated endpoint (`GET
 * /users/{id}/avatar?v=`, `f08-accounts-profile.md`), never a public/presigned
 * URL — a bare `<img src>` can't carry the bearer token, so this fetches the
 * bytes itself and turns them into an object URL. Cached by the server's own
 * `avatarUrl` string (`fetchAuthenticatedBlob` resolves it against the API
 * origin — that string already carries the version as `?v=`, so a changed
 * avatar is a different cache key, never a stale hit) and refcounted: the
 * blob is only revoked once the last consumer of that exact key has
 * unmounted, so two components showing the same user's avatar at once (a
 * board card and the properties panel, say) share one fetch and one blob URL
 * instead of each managing its own.
 */
const cache = new Map<string, CacheEntry>()
const inFlight = new Map<string, Promise<string>>()

async function acquireBlobUrl(avatarUrl: string): Promise<string> {
  const existing = cache.get(avatarUrl)

  if (existing !== undefined) {
    existing.refCount += 1
    return existing.blobUrl
  }

  const pending = inFlight.get(avatarUrl)

  if (pending !== undefined) {
    const blobUrl = await pending
    const entry = cache.get(avatarUrl)
    if (entry !== undefined) {
      entry.refCount += 1
    }
    return blobUrl
  }

  const promise = (async () => {
    const response = await fetchAuthenticatedBlob(avatarUrl)
    const blob = await response.blob()
    const blobUrl = URL.createObjectURL(blob)
    cache.set(avatarUrl, { blobUrl, refCount: 1 })
    return blobUrl
  })()

  inFlight.set(avatarUrl, promise)

  try {
    return await promise
  } finally {
    inFlight.delete(avatarUrl)
  }
}

function releaseBlobUrl(avatarUrl: string): void {
  const entry = cache.get(avatarUrl)

  if (entry === undefined) {
    return
  }

  entry.refCount -= 1

  if (entry.refCount <= 0) {
    URL.revokeObjectURL(entry.blobUrl)
    cache.delete(avatarUrl)
  }
}

/**
 * `avatarUrl` is the server-provided path (`UserListItem.avatarUrl`/
 * `Me.avatarUrl`/a presence `Viewer.avatarUrl`), or `null` for no avatar.
 * Returns `null` while loading, on fetch failure, or when there is no
 * avatar — callers fall back to the existing initials/palette look
 * unchanged. Every successful acquire is matched by exactly one release,
 * whether the component unmounts before or after the fetch resolves — a
 * `cancelled` flag alone is not enough here, since a resolution that arrives
 * after unmount must still release the reference it is about to receive.
 */
export function useAvatarUrl(avatarUrl: string | null): string | null {
  const [blobUrl, setBlobUrl] = useState<string | null>(null)

  useEffect(() => {
    if (avatarUrl === null) {
      setBlobUrl(null)
      return
    }

    let cancelled = false
    let acquired = false

    void acquireBlobUrl(avatarUrl)
      .then((url) => {
        acquired = true

        if (cancelled) {
          releaseBlobUrl(avatarUrl)
          return
        }

        setBlobUrl(url)
      })
      .catch(() => {
        if (!cancelled) {
          setBlobUrl(null)
        }
      })

    return () => {
      cancelled = true

      if (acquired) {
        releaseBlobUrl(avatarUrl)
      }
    }
  }, [avatarUrl])

  return blobUrl
}
