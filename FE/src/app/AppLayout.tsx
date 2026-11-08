import { Outlet } from 'react-router-dom'

import { NotificationBell } from '../features/notifications/components/NotificationBell'

import { Sidebar } from './Sidebar'

/**
 * `docs/design/SPEC.md` §3: sidebar (nav + views + signed-in user) beside the
 * content, a slim topbar above it. Stacks (sidebar above content) under
 * 600px, matching the board's own existing breakpoint (`BoardPage.tsx`).
 */
export function AppLayout() {
  return (
    <div className="flex min-h-screen flex-col min-[601px]:flex-row">
      <Sidebar />
      <div className="flex min-w-0 flex-1 flex-col">
        <header className="flex items-center justify-end border-b border-border bg-surface px-[var(--space-5)] py-[var(--space-3)] max-[600px]:px-[var(--space-4)]">
          <NotificationBell />
        </header>
        <main className="flex-1 p-[var(--space-5)] max-[600px]:px-[var(--space-4)]">
          <Outlet />
        </main>
      </div>
    </div>
  )
}
