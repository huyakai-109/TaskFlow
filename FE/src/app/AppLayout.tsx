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
 *
 * `relative` on this root (live /reports/team check: the document scrolled
 * ~584px beyond the shell, `scrollHeight` 1439 vs `innerHeight` 855): any
 * `position: absolute` descendant — found cause was a `<caption
 * class="sr-only">` inside a `ChartFigure`/heatmap data table — positions
 * against the nearest positioned ancestor, or the initial containing block
 * if there is none. Without `relative` here, `overflow-hidden` on this same
 * element does not clip such a descendant at all (clipping only applies to
 * the box the overflow is set on once there is something to contain it
 * against); `relative` makes this the containing block so every existing
 * and future off-screen/absolute element in the tree stays invisible and
 * non-scrolling, without touching each one individually. The caption stays
 * in the accessibility tree (`sr-only` hides visually, not from assistive
 * tech) — this is a layout fix, not an accessibility one.
 */
export function AppLayout() {
  const [collapsed, setCollapsed] = useSidebarCollapse()
  const [mobileOpen, setMobileOpen] = useState(false)
  const mobileMenuButtonRef = useRef<HTMLButtonElement>(null)

  return (
    <div className="relative flex h-dvh overflow-hidden">
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
