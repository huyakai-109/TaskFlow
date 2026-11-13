import clsx from 'clsx'
import { useRef } from 'react'
import type { ReactNode, RefObject } from 'react'
import { NavLink } from 'react-router-dom'

import { useAuthSession } from '../shared/hooks/useAuthSession'
import { useDialogA11y } from '../shared/hooks/useDialogA11y'
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

function MyTasksIcon() {
  return (
    <svg aria-hidden="true" width="16" height="16" viewBox="0 0 16 16" fill="none" stroke="currentColor" strokeWidth="1.6" strokeLinecap="round" strokeLinejoin="round">
      <circle cx="8" cy="5" r="2.5" />
      <path d="M2.5 14c0-3.3 2.5-5 5.5-5s5.5 1.7 5.5 5" />
    </svg>
  )
}

function TeamCapacityIcon() {
  return (
    <svg aria-hidden="true" width="16" height="16" viewBox="0 0 16 16" fill="none" stroke="currentColor" strokeWidth="1.6" strokeLinecap="round" strokeLinejoin="round">
      <circle cx="5.5" cy="5" r="2" />
      <circle cx="11" cy="6" r="1.6" />
      <path d="M1.5 13c0-2.5 1.8-3.8 4-3.8s4 1.3 4 3.8M9.8 9.6c1.7.1 3.2 1.3 3.2 3.4" />
    </svg>
  )
}

function CollapseIcon({ collapsed }: { collapsed: boolean }) {
  return (
    <svg
      aria-hidden="true"
      width="14"
      height="14"
      viewBox="0 0 16 16"
      fill="none"
      stroke="currentColor"
      strokeWidth="1.6"
      strokeLinecap="round"
      strokeLinejoin="round"
      className={clsx('transition-transform duration-200 motion-reduce:transition-none', collapsed && 'rotate-180')}
    >
      <rect x="1.5" y="2.5" width="13" height="11" rx="1.5" />
      <path d="M6 2.5v11" />
      <path d="M10 6.5 8.5 8l1.5 1.5" />
    </svg>
  )
}

function CloseIcon() {
  return (
    <svg aria-hidden="true" width="16" height="16" viewBox="0 0 16 16" fill="none" stroke="currentColor" strokeWidth="1.8" strokeLinecap="round">
      <path d="m3 3 10 10M13 3 3 13" />
    </svg>
  )
}

function LogoutIcon() {
  return (
    <svg aria-hidden="true" width="14" height="14" viewBox="0 0 16 16" fill="none" stroke="currentColor" strokeWidth="1.6" strokeLinecap="round" strokeLinejoin="round">
      <path d="M6.5 2H3.5a1 1 0 0 0-1 1v10a1 1 0 0 0 1 1h3" />
      <path d="M10.5 11 14 8l-3.5-3M14 8H5.5" />
    </svg>
  )
}

const navLinkBaseClass = 'group relative flex min-h-[34px] items-center gap-[var(--space-2)] rounded-control px-[var(--space-2)] text-[length:var(--font-size-base)] no-underline'

function navLinkClass(collapsed: boolean) {
  return ({ isActive }: { isActive: boolean }) =>
    clsx(
      navLinkBaseClass,
      collapsed && 'justify-center px-0',
      isActive ? 'bg-accent-soft font-medium text-accent-ink' : 'text-text-muted hover:bg-column hover:text-text',
    )
}

/** Shown next to the icon when expanded; collapsed into a small dot badge when collapsed (item 3 of the layout fix). */
function Counter({ count, collapsed }: { count: number; collapsed: boolean }) {
  if (count <= 0) {
    return null
  }

  if (collapsed) {
    return (
      <span
        aria-hidden="true"
        className="absolute top-0.5 right-0.5 size-[7px] rounded-pill bg-danger"
      />
    )
  }

  return <span className="ml-auto text-text-muted">{count}</span>
}

/** Visible only when the sidebar is collapsed, on hover/focus of the nav item — plain CSS tooltip, no new dependency. */
function CollapsedTooltip({ children }: { children: ReactNode }) {
  return (
    <span
      role="tooltip"
      className="pointer-events-none absolute left-[calc(100%+8px)] top-1/2 z-20 -translate-y-1/2 whitespace-nowrap rounded-control border border-border bg-surface-raised px-[var(--space-2)] py-[var(--space-1)] text-[length:var(--font-size-xs)] text-text opacity-0 shadow-raised group-hover:opacity-100 group-focus-visible:opacity-100"
    >
      {children}
    </span>
  )
}

interface SidebarNavProps {
  collapsed: boolean
  onNavigate?: () => void
}

/**
 * Nav links, "Views" and the signed-in-user footer — the part of the
 * sidebar shared between the desktop rail and the mobile drawer
 * (`docs/design/SPEC.md` §3). Kept as its own component so neither copy
 * drifts from the other.
 */
function SidebarNav({ collapsed, onNavigate }: SidebarNavProps) {
  const logout = useLogout()
  const { userId } = useAuthSession()
  const overdueTasksQuery = useOverdueTasksQuery()
  const usersQuery = useUsersQuery()

  const overdueCount = overdueTasksQuery.data?.pages.at(-1)?.pagination?.totalItems ?? 0
  const currentUser = usersQuery.data?.find((user) => user.id === userId) ?? null

  return (
    <>
      <nav aria-label="Primary" className="flex flex-col gap-[2px]">
        <NavLink to="/my-tasks" className={navLinkClass(collapsed)} onClick={onNavigate}>
          <MyTasksIcon />
          {!collapsed && 'My tasks'}
          {collapsed && <CollapsedTooltip>My tasks</CollapsedTooltip>}
        </NavLink>
        <NavLink to="/board" className={navLinkClass(collapsed)} onClick={onNavigate}>
          <BoardIcon />
          {!collapsed && 'Board'}
          {collapsed && <CollapsedTooltip>Board</CollapsedTooltip>}
        </NavLink>
        <NavLink to="/team-capacity" className={navLinkClass(collapsed)} onClick={onNavigate}>
          <TeamCapacityIcon />
          {!collapsed && 'Team capacity'}
          {collapsed && <CollapsedTooltip>Team capacity</CollapsedTooltip>}
        </NavLink>
      </nav>

      <div className="flex flex-col gap-[2px]">
        {!collapsed && (
          <span className="px-[var(--space-2)] text-[length:var(--font-size-2xs)] font-semibold uppercase tracking-[0.44px] text-text-muted">
            Views
          </span>
        )}
        <NavLink to="/overdue" className={navLinkClass(collapsed)} onClick={onNavigate}>
          <span className="size-2 shrink-0 rounded-pill bg-danger" aria-hidden="true" />
          {!collapsed && 'Overdue'}
          <Counter count={overdueCount} collapsed={collapsed} />
          {collapsed && <CollapsedTooltip>Overdue{overdueCount > 0 && ` (${overdueCount})`}</CollapsedTooltip>}
        </NavLink>
      </div>

      <div
        className={clsx(
          // `docs/design/SPEC.md` §3: footer row 49px high. A compact
          // icon-only logout button (26px, matching the avatar) keeps it
          // that short — a full "Log out" text button was taller than the
          // avatar/name block and stretched the footer past 49px.
          'mt-auto flex min-h-[26px] items-center gap-[var(--space-2)] border-t border-border pt-[var(--space-3)]',
          collapsed && 'flex-col',
        )}
      >
        {currentUser !== null && (
          <span
            className="group relative flex size-[26px] shrink-0 items-center justify-center rounded-pill text-[length:var(--font-size-2xs)] leading-none font-semibold"
            style={{
              backgroundColor: `var(--avatar-${avatarPaletteIndex(currentUser.id)}-bg)`,
              color: `var(--avatar-${avatarPaletteIndex(currentUser.id)}-ink)`,
            }}
          >
            {initialsOf(currentUser.userName)}
            {collapsed && (
              <CollapsedTooltip>
                {currentUser.userName} · {currentUser.role}
              </CollapsedTooltip>
            )}
          </span>
        )}
        {!collapsed && currentUser !== null && (
          <span className="flex min-w-0 flex-col">
            <span className="truncate text-[length:var(--font-size-base)] font-medium">{currentUser.userName}</span>
            <span className="text-[length:var(--font-size-xs)] text-text-muted">{currentUser.role}</span>
          </span>
        )}
        <button
          type="button"
          className={clsx(
            'inline-flex size-[26px] shrink-0 cursor-pointer items-center justify-center rounded-control border border-border bg-surface text-text-muted hover:bg-column hover:text-text',
            !collapsed && 'ml-auto',
          )}
          aria-label="Log out"
          title="Log out"
          onClick={logout}
        >
          <LogoutIcon />
        </button>
      </div>
    </>
  )
}

interface MobileSidebarDrawerProps {
  onClose: () => void
  triggerRef: RefObject<HTMLButtonElement | null>
}

/**
 * The <600px drawer (`docs/design/SPEC.md` §3 — additive, see the "Below
 * 600px" note added alongside this component). Mounted only while open, so
 * it is naturally out of the tab order when closed — the same pattern every
 * other dialog in this app already uses (`useDialogA11y`), reused here
 * rather than inventing a second focus-trap implementation.
 */
function MobileSidebarDrawer({ onClose, triggerRef }: MobileSidebarDrawerProps) {
  const closeButtonRef = useRef<HTMLButtonElement>(null)
  useDialogA11y(closeButtonRef, onClose)

  function closeAndReturnFocus() {
    onClose()
    triggerRef.current?.focus()
  }

  return (
    <div className="fixed inset-0 z-50 min-[601px]:hidden">
      <div
        aria-hidden="true"
        className="absolute inset-0 bg-overlay"
        onClick={closeAndReturnFocus}
      />
      <aside
        role="dialog"
        aria-modal="true"
        aria-label="Primary navigation"
        className="relative flex h-full w-[240px] flex-col gap-[var(--space-5)] border-r border-border bg-sidebar p-[var(--space-4)]"
      >
        <div className="flex items-center gap-[var(--space-2)]">
          <span className="flex size-[26px] items-center justify-center rounded-logo bg-accent text-[length:var(--font-size-sm)] font-semibold text-on-accent">
            T
          </span>
          <span className="text-[length:var(--font-size-base)] font-semibold">TaskFlow</span>
          <button
            type="button"
            ref={closeButtonRef}
            aria-label="Close menu"
            className="ml-auto inline-flex size-8 cursor-pointer items-center justify-center rounded-control border-0 bg-transparent text-text-muted hover:bg-column"
            onClick={closeAndReturnFocus}
          >
            <CloseIcon />
          </button>
        </div>
        <SidebarNav collapsed={false} onNavigate={closeAndReturnFocus} />
      </aside>
    </div>
  )
}

interface SidebarProps {
  collapsed: boolean
  onToggleCollapse: () => void
  mobileOpen: boolean
  onCloseMobile: () => void
  mobileTriggerRef: RefObject<HTMLButtonElement | null>
}

/**
 * `docs/design/SPEC.md` §3. Scoped down per Gate A (approved 2026-11-08,
 * SPEC.md §10): no "Team Alpha" workspace label (CLAUDE.md — no team entity),
 * no "Due this week" (not in scope at all). Team capacity was reopened and
 * built 2026-11-11 (SPEC.md §10 point 5) — it reads data an existing endpoint
 * already returns, not a new backend addition. Inbox was built the same day,
 * then removed 2026-11-13 (SPEC.md §3/§10 point 5) — its nav slot is empty,
 * replacement undecided (`docs/design/sidebar-inbox-replacement.md`).
 *
 * Desktop (>=601px): a collapsible rail (240px expanded, 64px collapsed —
 * `useSidebarCollapse`, persisted). Below 601px: hidden entirely (`hidden
 * min-[601px]:flex`) in favour of `MobileSidebarDrawer`, opened from the
 * topbar's menu button — it no longer stacks above the page as a static
 * block (layout fix, see `docs/design/SPEC.md` §3 note).
 */
export function Sidebar({ collapsed, onToggleCollapse, mobileOpen, onCloseMobile, mobileTriggerRef }: SidebarProps) {
  return (
    <>
      <aside
        className={clsx(
          'hidden h-full shrink-0 flex-col gap-[var(--space-5)] border-r border-border bg-sidebar p-[var(--space-4)] transition-[width] duration-200 motion-reduce:transition-none min-[601px]:flex',
          collapsed ? 'w-16' : 'w-[240px]',
        )}
      >
        <div className={clsx('flex items-center gap-[var(--space-2)]', collapsed && 'justify-center')}>
          <span className="flex size-[26px] shrink-0 items-center justify-center rounded-logo bg-accent text-[length:var(--font-size-sm)] font-semibold text-on-accent">
            T
          </span>
          {!collapsed && <span className="text-[length:var(--font-size-base)] font-semibold">TaskFlow</span>}
          <button
            type="button"
            aria-expanded={!collapsed}
            aria-label={collapsed ? 'Expand sidebar' : 'Collapse sidebar'}
            className={clsx(
              'inline-flex size-7 shrink-0 cursor-pointer items-center justify-center rounded-control border-0 bg-transparent text-text-muted hover:bg-column hover:text-text',
              !collapsed && 'ml-auto',
            )}
            onClick={onToggleCollapse}
          >
            <CollapseIcon collapsed={collapsed} />
          </button>
        </div>

        <SidebarNav collapsed={collapsed} />
      </aside>

      {mobileOpen && <MobileSidebarDrawer onClose={onCloseMobile} triggerRef={mobileTriggerRef} />}
    </>
  )
}
