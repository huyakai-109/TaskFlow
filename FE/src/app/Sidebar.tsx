import clsx from 'clsx'
import { NavLink } from 'react-router-dom'

import { useAuthSession } from '../shared/hooks/useAuthSession'
import { useOverdueTasksQuery } from '../shared/hooks/useOverdueTasksQuery'
import { useUsersQuery } from '../shared/hooks/useUsersQuery'
import { useLogout } from '../shared/hooks/useLogout'
import { avatarPaletteIndex, initialsOf } from '../shared/lib/avatar'

function BoardIcon() {
  return (
    <svg aria-hidden="true" width="16" height="16" viewBox="0 0 16 16" fill="none" stroke="currentColor" strokeWidth="1.6" strokeLinecap="round" strokeLinejoin="round">
      <rect x="1.5" y="2.5" width="13" height="11" rx="1.5" />
      <path d="M6 2.5v11M10.5 2.5v11" />
    </svg>
  )
}

const navLinkClass = ({ isActive }: { isActive: boolean }) =>
  clsx(
    'flex min-h-[34px] items-center gap-[var(--space-2)] rounded-control px-[var(--space-2)] text-[length:var(--font-size-base)] no-underline',
    isActive ? 'bg-accent-soft font-medium text-accent-ink' : 'text-text-muted hover:bg-column hover:text-text',
  )

/**
 * `docs/design/SPEC.md` §3. Scoped down per Gate A (approved 2026-11-08,
 * SPEC.md §10): no "Team Alpha" workspace label (CLAUDE.md — no team entity),
 * no Inbox/Team capacity nav items or search box (not built, hidden rather
 * than dead-linked), no "My tasks"/"Due this week" yet (My tasks ships in the
 * last slice of this redesign; "Due this week" isn't in scope at all).
 */
export function Sidebar() {
  const logout = useLogout()
  const { userId } = useAuthSession()
  const overdueTasksQuery = useOverdueTasksQuery()
  const usersQuery = useUsersQuery()

  const overdueCount = overdueTasksQuery.data?.pages.at(-1)?.pagination?.totalItems ?? 0
  const currentUser = usersQuery.data?.find((user) => user.id === userId) ?? null

  return (
    <aside className="flex w-full shrink-0 flex-col gap-[var(--space-5)] border-b border-border bg-sidebar p-[var(--space-4)] max-[600px]:gap-[var(--space-3)] min-[601px]:h-screen min-[601px]:w-[240px] min-[601px]:border-r min-[601px]:border-b-0">
      <div className="flex items-center gap-[var(--space-2)]">
        <span className="flex size-[26px] items-center justify-center rounded-logo bg-accent text-[length:var(--font-size-sm)] font-semibold text-on-accent">
          T
        </span>
        <span className="text-[length:var(--font-size-base)] font-semibold">TaskFlow</span>
      </div>

      <nav aria-label="Primary" className="flex flex-col gap-[2px]">
        <NavLink to="/board" className={navLinkClass}>
          <BoardIcon />
          Board
        </NavLink>
      </nav>

      <div className="flex flex-col gap-[2px]">
        <span className="px-[var(--space-2)] text-[length:var(--font-size-2xs)] font-semibold uppercase tracking-[0.44px] text-text-muted">
          Views
        </span>
        <NavLink to="/overdue" className={navLinkClass}>
          <span className="size-2 shrink-0 rounded-pill bg-danger" aria-hidden="true" />
          Overdue
          {overdueCount > 0 && <span className="ml-auto text-text-muted">{overdueCount}</span>}
        </NavLink>
      </div>

      <div className="mt-auto flex items-center gap-[var(--space-2)] border-t border-border pt-[var(--space-3)]">
        {currentUser !== null && (
          <>
            <span
              className="flex size-[26px] shrink-0 items-center justify-center rounded-pill text-[length:var(--font-size-2xs)] font-semibold"
              style={{
                backgroundColor: `var(--avatar-${avatarPaletteIndex(currentUser.id)}-bg)`,
                color: `var(--avatar-${avatarPaletteIndex(currentUser.id)}-ink)`,
              }}
            >
              {initialsOf(currentUser.userName)}
            </span>
            <span className="flex min-w-0 flex-col">
              <span className="truncate text-[length:var(--font-size-base)] font-medium">{currentUser.userName}</span>
              <span className="text-[length:var(--font-size-xs)] text-text-muted">{currentUser.role}</span>
            </span>
          </>
        )}
        <button
          type="button"
          className="ml-auto min-h-9 shrink-0 cursor-pointer rounded-control border border-border bg-surface px-[var(--space-3)] text-[length:var(--font-size-sm)] text-text hover:bg-column"
          onClick={logout}
        >
          Log out
        </button>
      </div>
    </aside>
  )
}
