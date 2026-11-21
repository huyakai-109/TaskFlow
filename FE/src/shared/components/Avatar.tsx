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

  if (blobUrl !== null) {
    return (
      <img
        src={blobUrl}
        alt=""
        aria-hidden="true"
        className={className}
        style={{ width: size, height: size, borderRadius: '50%', objectFit: 'cover', flexShrink: 0 }}
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
