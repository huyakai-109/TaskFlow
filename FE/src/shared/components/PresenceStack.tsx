import { Avatar } from './Avatar'
import { useAuthSession } from '../hooks/useAuthSession'
import { usePresence } from '../hooks/usePresence'
import { useUsersQuery } from '../hooks/useUsersQuery'

const MAX_SHOWN = 5

interface PresenceStackProps {
  scope: string
  label?: string
}

/**
 * "Currently viewing" stack (`docs/features/f07-presence.md`) — replaces
 * `ViewingNowPlaceholder` on the board toolbar, and the same component on
 * task detail. Shows only name and avatar (A6 sign-off, Viewer carries
 * nothing else). While there is no live connection (`usePresence` returns
 * `'unknown'` — disconnected, reconnecting, or not yet connected), this
 * never invents an "online" list: it falls back to the signed-in viewer's
 * own avatar only, the same honest fallback `ViewingNowPlaceholder` used
 * before a real presence feed existed.
 */
export function PresenceStack({ scope, label = 'Viewing now' }: PresenceStackProps) {
  const viewers = usePresence(scope)
  const { userId } = useAuthSession()
  const usersQuery = useUsersQuery()
  const me = usersQuery.data?.find((user) => user.id === userId) ?? null

  if (viewers === 'unknown') {
    return (
      <div className="flex items-center gap-[var(--space-2)]">
        <span className="text-[length:var(--font-size-sm)] text-text-muted">{label}</span>
        {me !== null && (
          <span title={`${me.displayName ?? me.userName} (you)`}>
            <Avatar userId={me.id} name={me.displayName ?? me.userName} avatarUrl={me.avatarUrl} size={28} />
          </span>
        )}
      </div>
    )
  }

  const shown = viewers.slice(0, MAX_SHOWN)
  const overflow = viewers.length - shown.length

  return (
    <div className="flex items-center gap-[var(--space-2)]">
      <span className="text-[length:var(--font-size-sm)] text-text-muted">{label}</span>
      <div className="flex items-center" aria-label={`${viewers.length} viewing: ${viewers.map((viewer) => viewer.displayName ?? viewer.userName).join(', ')}`}>
        {shown.map((viewer, index) => (
          <span
            key={viewer.userId}
            title={viewer.userId === userId ? `${viewer.displayName ?? viewer.userName} (you)` : viewer.displayName ?? viewer.userName}
            style={{ marginLeft: index === 0 ? 0 : -5, boxShadow: '0 0 0 2px var(--color-bg)', borderRadius: '50%' }}
          >
            <Avatar userId={viewer.userId} name={viewer.displayName ?? viewer.userName} avatarUrl={viewer.avatarUrl} size={28} />
          </span>
        ))}
        {overflow > 0 && (
          <span
            className="flex size-[28px] items-center justify-center rounded-pill bg-column text-[11px] font-semibold text-text-muted"
            style={{ marginLeft: -5, boxShadow: '0 0 0 2px var(--color-bg)' }}
          >
            +{overflow}
          </span>
        )}
      </div>
    </div>
  )
}
