import { useEffect, useRef, useState } from 'react'
import { useNavigate } from 'react-router-dom'

import type { TaskSummary } from '../../../api/types'

interface BoardSearchProps {
  tasks: TaskSummary[]
}

function matches(task: TaskSummary, query: string): boolean {
  const q = query.trim().toLowerCase()
  if (q.length === 0) {
    return false
  }

  return task.title.toLowerCase().includes(q) || String(task.id).includes(q)
}

/**
 * `docs/design/SPEC.md` §3/§10 point 5 (reopened 2026-11-11): the topbar
 * search box, `⌘K` to focus. This is a client-side jump to a task already
 * loaded on the board, **not** a server search — there is no search
 * endpoint (`docs/design/COMING-SOON.md` A4). Scoped to the Board page: the
 * data it searches (`useBoardQuery`) is already loaded there.
 */
export function BoardSearch({ tasks }: BoardSearchProps) {
  const navigate = useNavigate()
  const inputRef = useRef<HTMLInputElement>(null)
  const containerRef = useRef<HTMLDivElement>(null)
  const [query, setQuery] = useState('')
  const [open, setOpen] = useState(false)

  useEffect(() => {
    function onKeyDown(event: KeyboardEvent) {
      if ((event.metaKey || event.ctrlKey) && event.key.toLowerCase() === 'k') {
        event.preventDefault()
        inputRef.current?.focus()
        inputRef.current?.select()
      }
    }

    function onPointerDown(event: PointerEvent) {
      if (event.target instanceof Node && containerRef.current?.contains(event.target) === false) {
        setOpen(false)
      }
    }

    document.addEventListener('keydown', onKeyDown)
    document.addEventListener('pointerdown', onPointerDown)
    return () => {
      document.removeEventListener('keydown', onKeyDown)
      document.removeEventListener('pointerdown', onPointerDown)
    }
  }, [])

  const results = open ? tasks.filter((task) => matches(task, query)).slice(0, 8) : []

  function go(taskId: number) {
    setQuery('')
    setOpen(false)
    navigate(`/tasks/${taskId}`)
  }

  return (
    <div ref={containerRef} className="relative w-[260px] max-[600px]:w-full">
      <label htmlFor="board-search-input" className="sr-only">
        Search tasks
      </label>
      <div className="flex min-h-9 items-center gap-[var(--space-2)] rounded-control-lg border border-border bg-surface px-[var(--space-3)]">
        <svg aria-hidden="true" width="14" height="14" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="1.8" strokeLinecap="round">
          <circle cx="11" cy="11" r="7" />
          <path d="m21 21-4.3-4.3" />
        </svg>
        <input
          id="board-search-input"
          ref={inputRef}
          type="text"
          placeholder="Search tasks…"
          className="min-w-0 flex-1 border-0 bg-transparent text-[length:var(--font-size-sm)] text-text outline-none"
          value={query}
          onChange={(event) => {
            setQuery(event.target.value)
            setOpen(true)
          }}
          onFocus={() => setOpen(true)}
          onKeyDown={(event) => {
            if (event.key === 'Escape') {
              setQuery('')
              setOpen(false)
              inputRef.current?.blur()
            }

            if (event.key === 'Enter' && results.length > 0) {
              go(results[0].id)
            }
          }}
        />
        <kbd className="rounded-[5px] bg-sidebar px-[6px] py-[1px] font-mono text-[length:var(--font-size-2xs)] text-text-muted">
          ⌘K
        </kbd>
      </div>

      {open && query.trim().length > 0 && (
        <ul
          role="listbox"
          aria-label="Matching tasks"
          className="absolute top-[calc(100%+4px)] left-0 z-50 max-h-[260px] w-full overflow-y-auto rounded-lg border border-border bg-surface-raised p-[var(--space-1)] shadow-raised"
        >
          {results.length === 0 && (
            <li className="px-[var(--space-3)] py-[var(--space-2)] text-[length:var(--font-size-sm)] text-text-muted">
              No matching task
            </li>
          )}
          {results.map((task) => (
            <li key={task.id}>
              <button
                type="button"
                className="flex w-full cursor-pointer items-baseline gap-[var(--space-2)] rounded-md border-0 bg-transparent px-[var(--space-3)] py-[var(--space-2)] text-left text-text hover:bg-column"
                onClick={() => go(task.id)}
              >
                <span className="font-mono text-[length:var(--font-size-xs)] text-text-muted">#{task.id}</span>
                <span className="truncate">{task.title}</span>
              </button>
            </li>
          ))}
        </ul>
      )}
    </div>
  )
}
