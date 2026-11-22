import { Avatar } from './Avatar'
import { useAuthSession } from '../hooks/useAuthSession'
import { usePresence } from '../hooks/usePresence'
import { useUsersQuery } from '../hooks/useUsersQuery'
import { userLabel } from '../lib/userLabel'

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
          <span title={`${userLabel(me)} (you)`}>
            <Avatar userId={me.id} name={userLabel(me)} avatarUrl={me.avatarUrl} size={28} />
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
      <div className="flex items-center" aria-label={`${viewers.length} viewing: ${viewers.map((viewer) => userLabel(viewer)).join(', ')}`}>
        {shown.map((viewer, index) => (
          <span
            key={viewer.userId}
            title={viewer.userId === userId ? `${userLabel(viewer)} (you)` : userLabel(viewer)}
            style={{ marginLeft: index === 0 ? 0 : -5, boxShadow: '0 0 0 2px var(--color-bg)', borderRadius: '50%' }}
          >
            <Avatar userId={viewer.userId} name={userLabel(viewer)} avatarUrl={viewer.avatarUrl} size={28} />
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
