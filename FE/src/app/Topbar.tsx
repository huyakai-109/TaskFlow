import { useEffect, useState } from 'react'
import type { RefObject } from 'react'
import { Link, useLocation, useNavigate, useParams } from 'react-router-dom'

import { BOARD_COLUMNS } from '../api/types'
import { NewTaskDialog } from '../features/board/components/NewTaskDialog'
import { BoardSearch } from '../features/board/components/BoardSearch'
import { NotificationBell } from '../features/notifications/components/NotificationBell'
import { ThemeToggle } from '../shared/components/ThemeToggle'
import { useBoardQuery } from '../shared/hooks/useBoardQuery'
import { isTypingTarget } from '../shared/lib/isTypingTarget'

function MenuIcon() {
  return (
    <svg aria-hidden="true" width="18" height="18" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="1.8" strokeLinecap="round">
      <path d="M3 6h18M3 12h18M3 18h18" />
    </svg>
  )
}

interface Crumb {
  parent: string | null
  current: string
}

/** `docs/design/SPEC.md` §3: "parent / current", parent muted 15/400, current 15/600. Task detail uses its own back-link form instead (handled separately below). */
function crumbFor(pathname: string): Crumb {
  if (pathname === '/board') return { parent: null, current: 'Board' }
  if (pathname === '/my-tasks') return { parent: null, current: 'My tasks' }
  if (pathname === '/team-capacity') return { parent: null, current: 'Team capacity' }
  if (pathname === '/overdue') return { parent: 'Views', current: 'Overdue' }
  return { parent: null, current: 'TaskFlow' }
}

interface TopbarProps {
  onOpenMobileMenu: () => void
  mobileMenuButtonRef: RefObject<HTMLButtonElement | null>
}

/**
 * `docs/design/SPEC.md` §3 — one topbar for every page, pinned above
 * `<main>` (layout fix; it used to be BoardPage's own toolbar, scoped to one
 * page). Breadcrumb on the left (back-link form on Task detail); search
 * (`⌘K`), New task (`C`), theme toggle and the notification bell on the
 * right, all working from any page now instead of only the Board.
 */
export function Topbar({ onOpenMobileMenu, mobileMenuButtonRef }: TopbarProps) {
  const location = useLocation()
  const navigate = useNavigate()
  const params = useParams<{ id: string }>()
  const [createOpen, setCreateOpen] = useState(false)
  const boardQuery = useBoardQuery()

  const isTaskDetail = location.pathname.startsWith('/tasks/')

  // `C` new task, scoped here (not to the Board page) so it works from any
  // page (`docs/design/SPEC.md` §3/§6 point 7, layout fix — it used to live
  // only on `BoardPage`). Never fires while typing in a field or while the
  // New task dialog it opens is already up.
  useEffect(() => {
    function onKeyDown(event: KeyboardEvent) {
      if (event.key.toLowerCase() !== 'c' || event.metaKey || event.ctrlKey || event.altKey) {
        return
      }

      if (isTypingTarget(event.target) || createOpen) {
        return
      }

      event.preventDefault()
      setCreateOpen(true)
    }

    document.addEventListener('keydown', onKeyDown)
    return () => document.removeEventListener('keydown', onKeyDown)
  }, [createOpen])

  const tasks = BOARD_COLUMNS.flatMap((status) => boardQuery.data?.columns.find((c) => c.status === status)?.items ?? [])
  const mayBeIncomplete = BOARD_COLUMNS.some((status) => {
    const column = boardQuery.data?.columns.find((c) => c.status === status)
    return column?.pagination !== null && column !== undefined && column.items.length < (column.pagination?.totalItems ?? 0)
  })

  const crumb = crumbFor(location.pathname)

  return (
    <header className="flex min-h-[57px] items-center gap-[var(--space-3)] border-b border-border bg-surface px-[var(--space-5)] py-[var(--space-2)] max-[600px]:px-[var(--space-4)]">
      <button
        type="button"
        ref={mobileMenuButtonRef}
        aria-label="Open menu"
        className="inline-flex size-9 shrink-0 cursor-pointer items-center justify-center rounded-control border border-border bg-surface text-text min-[601px]:hidden"
        onClick={onOpenMobileMenu}
      >
        <MenuIcon />
      </button>

      {isTaskDetail ? (
        <Link
          to="/board"
          className="text-[length:var(--font-size-md)] text-text-muted no-underline hover:underline"
        >
          ‹ Board / <span className="font-mono">#{params.id}</span>
        </Link>
      ) : (
        <p className="m-0 flex items-baseline gap-[var(--space-1)] text-[length:var(--font-size-md)]">
          {crumb.parent !== null && <span className="text-text-muted">{crumb.parent} /</span>}
          <span className="font-semibold text-text">{crumb.current}</span>
        </p>
      )}

      <div className="ml-auto flex items-center gap-[var(--space-3)] max-[600px]:gap-[var(--space-2)]">
        <div className="max-[440px]:hidden">
          <BoardSearch tasks={tasks} mayBeIncomplete={mayBeIncomplete} />
        </div>
        <button
          type="button"
          className="inline-flex min-h-9 cursor-pointer items-center gap-[var(--space-2)] rounded-control-lg border-0 bg-accent px-[var(--space-3)] font-semibold text-on-accent hover:bg-accent-hover max-[600px]:px-[var(--space-2)]"
          onClick={() => setCreateOpen(true)}
        >
          <span className="max-[480px]:hidden">New task</span>
          <kbd
            aria-hidden="true"
            className="rounded-[5px] bg-[rgba(255,255,255,0.18)] px-[6px] py-[1px] font-mono text-[length:var(--font-size-2xs)] font-normal"
          >
            C
          </kbd>
        </button>
        <ThemeToggle />
        <NotificationBell />
      </div>

      {createOpen && (
        <NewTaskDialog
          onClose={() => setCreateOpen(false)}
          onCreated={(taskId) => {
            setCreateOpen(false)
            navigate(`/tasks/${taskId}`)
          }}
        />
      )}
    </header>
  )
}
