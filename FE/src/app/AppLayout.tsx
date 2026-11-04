import { Outlet } from 'react-router-dom'

import { NotificationBell } from '../features/notifications/components/NotificationBell'
import { useLogout } from '../shared/hooks/useLogout'

export function AppLayout() {
  const logout = useLogout()

  return (
    <div className="flex min-h-screen flex-col">
      <header className="flex items-center gap-[var(--space-3)] border-b border-border bg-surface px-[var(--space-5)] py-[var(--space-3)] max-[600px]:px-[var(--space-4)]">
        <h1 className="text-[length:var(--font-size-lg)] font-semibold">TaskFlow</h1>
        <NotificationBell />
        <button
          type="button"
          className="cursor-pointer rounded-md border border-border bg-surface px-[var(--space-3)] py-[var(--space-2)] text-[length:var(--font-size-sm)] text-text hover:bg-column"
          onClick={logout}
        >
          Log out
        </button>
      </header>
      <main className="flex-1 p-[var(--space-5)] max-[600px]:px-[var(--space-4)]">
        <Outlet />
      </main>
    </div>
  )
}
