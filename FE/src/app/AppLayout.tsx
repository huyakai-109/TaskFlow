import { useRef, useState } from 'react'
import { Outlet } from 'react-router-dom'

import { Sidebar } from './Sidebar'
import { Topbar } from './Topbar'
import { useSidebarCollapse } from './useSidebarCollapse'

/**
 * `docs/design/SPEC.md` §3: sidebar beside the content, a topbar pinned
 * above it. The whole shell is exactly the viewport height (`h-dvh`) with no
 * page-level scroll — `<main>` is the one scrollable region, and the sidebar
 * and topbar never scroll away with it (layout fix; they used to be
 * `position: static` and scrolled off with the page at narrow viewport
 * heights). Below 601px the sidebar becomes a drawer instead of stacking
 * above the page (`Sidebar.tsx`).
 */
export function AppLayout() {
  const [collapsed, setCollapsed] = useSidebarCollapse()
  const [mobileOpen, setMobileOpen] = useState(false)
  const mobileMenuButtonRef = useRef<HTMLButtonElement>(null)

  return (
    <div className="flex h-dvh overflow-hidden">
      <Sidebar
        collapsed={collapsed}
        onToggleCollapse={() => setCollapsed((value) => !value)}
        mobileOpen={mobileOpen}
        onCloseMobile={() => setMobileOpen(false)}
        mobileTriggerRef={mobileMenuButtonRef}
      />
      <div className="flex min-h-0 min-w-0 flex-1 flex-col overflow-hidden">
        <Topbar onOpenMobileMenu={() => setMobileOpen(true)} mobileMenuButtonRef={mobileMenuButtonRef} />
        <main className="min-h-0 flex-1 overflow-y-auto p-[var(--space-5)] max-[600px]:px-[var(--space-4)]">
          <Outlet />
        </main>
      </div>
    </div>
  )
}
