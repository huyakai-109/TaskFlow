import clsx from 'clsx'
import { NavLink, Outlet } from 'react-router-dom'

import { NotificationBell } from '../features/notifications/components/NotificationBell'
import { useLogout } from '../shared/hooks/useLogout'

const navLinkClass = ({ isActive }: { isActive: boolean }) =>
  clsx(
    'min-h-10 inline-flex items-center rounded-md px-[var(--space-3)] text-[length:var(--font-size-sm)] font-medium no-underline',
    isActive ? 'bg-accent-soft text-accent-ink' : 'text-text-muted hover:bg-column hover:text-text',
  )

export function AppLayout() {
  const logout = useLogout()

  return (
    <div className="flex min-h-screen flex-col">
      <header className="flex items-center gap-[var(--space-3)] border-b border-border bg-surface px-[var(--space-5)] py-[var(--space-3)] max-[600px]:px-[var(--space-4)]">
        <h1 className="text-[length:var(--font-size-lg)] font-semibold">TaskFlow</h1>
        <nav aria-label="Primary" className="flex items-center gap-[var(--space-1)]">
          <NavLink to="/board" className={navLinkClass}>
            Board
          </NavLink>
          <NavLink to="/overdue" className={navLinkClass}>
            Overdue
          </NavLink>
        </nav>
        <div className="ml-auto flex items-center gap-[var(--space-3)]">
          <NotificationBell />
          <button
            type="button"
            className="cursor-pointer rounded-md border border-border bg-surface px-[var(--space-3)] py-[var(--space-2)] text-[length:var(--font-size-sm)] text-text hover:bg-column"
            onClick={logout}
          >
            Log out
          </button>
        </div>
      </header>
      <main className="flex-1 p-[var(--space-5)] max-[600px]:px-[var(--space-4)]">
        <Outlet />
      </main>
    </div>
  )
}
