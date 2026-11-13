import { ComingSoon } from '../../../shared/components/ComingSoon'
import { useComingSoon } from '../../../shared/hooks/useComingSoon'
import { useAuthSession } from '../../../shared/hooks/useAuthSession'
import { useUsersQuery } from '../../../shared/hooks/useUsersQuery'
import { avatarPaletteIndex, initialsOf } from '../../../shared/lib/avatar'

/**
 * Replaces the Board toolbar's "N at capacity" pill and `AvatarStack`/Team
 * popover (removed this task — capacity already has its own page, Team
 * capacity, so repeating it here was redundant, and the "at capacity" ring
 * styling on the avatar stack made it read as live presence, which it never
 * was: it was derived from the board's own `InProgress` column, same data as
 * the Team capacity page).
 *
 * There is no live-presence feed in this app — no socket, no poll of "who's
 * looking at the board right now". Showing a stack of "other users online"
 * here would be invented data (`frontend.md` §4: every async surface shows
 * honest data, never a fabricated one), so this control shows only the
 * signed-in viewer's own avatar, same 28px sizing and slot `AvatarStack` used,
 * so a later real presence list can drop in without a layout change. Clicking
 * it opens the same `ComingSoon` toast every other blocked control in this
 * app uses.
 */
export function ViewingNowPlaceholder() {
  const { userId } = useAuthSession()
  const usersQuery = useUsersQuery()
  const comingSoon = useComingSoon('Live presence is coming soon.')
  const me = usersQuery.data?.find((user) => user.id === userId) ?? null

  return (
    <div className="flex items-center gap-[var(--space-2)]">
      <span className="text-[length:var(--font-size-sm)] text-text-muted">Viewing now</span>
      <button
        type="button"
        title="Live presence is coming soon"
        aria-label={me !== null ? `${me.userName} (you) — live presence is coming soon` : 'Live presence is coming soon'}
        className="flex size-[28px] shrink-0 cursor-pointer items-center justify-center rounded-pill border-0 text-[11px] leading-none font-semibold"
        style={
          me !== null
            ? {
                backgroundColor: `var(--avatar-${avatarPaletteIndex(me.id)}-bg)`,
                color: `var(--avatar-${avatarPaletteIndex(me.id)}-ink)`,
                boxShadow: '0 0 0 2px var(--color-bg)',
              }
            : { backgroundColor: 'var(--color-meter-empty)', boxShadow: '0 0 0 2px var(--color-bg)' }
        }
        onClick={() => comingSoon.show()}
      >
        {me !== null ? initialsOf(me.userName) : ''}
      </button>

      {comingSoon.message !== null && <ComingSoon message={comingSoon.message} onDismiss={comingSoon.hide} />}
    </div>
  )
}
