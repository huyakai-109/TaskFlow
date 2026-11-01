import { Outlet } from 'react-router-dom'

import { NotificationBell } from '../features/notifications/components/NotificationBell'

export function AppLayout() {
  return (
    <div className="flex min-h-screen flex-col">
      <header className="flex items-center border-b border-border bg-surface px-[var(--space-5)] py-[var(--space-3)] max-[600px]:px-[var(--space-4)]">
        <h1 className="text-[length:var(--font-size-lg)] font-semibold">TaskFlow</h1>
        <NotificationBell />
      </header>
      <main className="flex-1 p-[var(--space-5)] max-[600px]:px-[var(--space-4)]">
        <Outlet />
      </main>
    </div>
  )
}
