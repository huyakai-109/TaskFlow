import { useEffect, useState } from 'react'

import { avatarPaletteIndex, initialsOf } from '../lib/avatar'
import { useAvatarUrl } from '../hooks/useAvatarUrl'

interface AvatarProps {
  userId: number
  /** `displayName ?? userName` is the caller's job — initials fall back to whichever name was passed (f08-accounts-profile.md "GET /users": "Initials fallback = first two characters of `displayName ?? userName`"). */
  name: string
  avatarUrl: string | null
  /** Pixel size — every existing call site used a different one (16/20/26/28), so this stays a prop rather than a fixed size. */
  size: number
  className?: string
}

/**
 * One shared avatar: a real picture when `avatarUrl` is set and loads, the
 * existing initials-on-a-palette-colour look otherwise (loading, no avatar,
 * or a failed fetch) — never a broken-image icon. Threaded in wherever a
 * user appears (`docs/features/f08-accounts-profile.md` "GET /users": board
 * card, presence stack, sidebar footer, assignee pickers, history/activity
 * actor, team capacity, notification actor).
 */
export function Avatar({ userId, name, avatarUrl, size, className }: AvatarProps) {
  const blobUrl = useAvatarUrl(avatarUrl)
  // `useAvatarUrl` turns whatever bytes the server returned into an object
  // URL without checking they decode as an image (an empty body, a 404 HTML
  // page served as 200 by a proxy, or a non-image content type all pass
  // through it). The bug this fixes: such a URL still resolves to a loaded
  // `<img>` element — a broken image doesn't decode to a transparent box,
  // it paints as a solid colour (observed: solid black) inside the circular,
  // `object-fit: cover` frame, never the initials fallback. `onError` is the
  // one signal the `<img>` element itself gives for "this didn't decode",
  // so it is handled here rather than re-validating bytes in `useAvatarUrl`.
  const [imageFailed, setImageFailed] = useState(false)

  useEffect(() => {
    setImageFailed(false)
  }, [blobUrl])

  if (blobUrl !== null && !imageFailed) {
    return (
      <img
        src={blobUrl}
        alt=""
        aria-hidden="true"
        className={className}
        style={{ width: size, height: size, borderRadius: '50%', objectFit: 'cover', flexShrink: 0 }}
        onError={() => setImageFailed(true)}
      />
    )
  }

  return (
    <span
      aria-hidden="true"
      className={className}
      style={{
        width: size,
        height: size,
        borderRadius: '50%',
        display: 'inline-flex',
        alignItems: 'center',
        justifyContent: 'center',
        flexShrink: 0,
        fontSize: Math.max(9, Math.round(size * 0.42)),
        lineHeight: 1,
        fontWeight: 600,
        backgroundColor: `var(--avatar-${avatarPaletteIndex(userId)}-bg)`,
        color: `var(--avatar-${avatarPaletteIndex(userId)}-ink)`,
      }}
    >
      {initialsOf(name)}
    </span>
  )
}
