import clsx from 'clsx'
import { useEffect, useRef, useState } from 'react'

import { ComingSoon } from '../../../shared/components/ComingSoon'
import { useComingSoon } from '../../../shared/hooks/useComingSoon'

type SortOption = 'priority' | 'dueDate' | 'recentlyUpdated'

const SORT_OPTIONS: ReadonlyArray<{ value: SortOption; label: string }> = [
  { value: 'priority', label: 'Priority' },
  { value: 'dueDate', label: 'Due date' },
  { value: 'recentlyUpdated', label: 'Recently updated' },
]

function SortIcon() {
  return (
    <svg aria-hidden="true" width="14" height="14" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="1.8" strokeLinecap="round" strokeLinejoin="round">
      <path d="M7 4v16M7 4 4 7M7 4l3 3M17 20V4M17 20l-3-3M17 20l3-3" />
    </svg>
  )
}

function CheckIcon() {
  return (
    <svg aria-hidden="true" width="14" height="14" viewBox="0 0 16 16" fill="none" stroke="currentColor" strokeWidth="1.8" strokeLinecap="round" strokeLinejoin="round">
      <path d="M3 8.5 6.5 12 13 4.5" />
    </svg>
  )
}

/**
 * The Board/List toolbar's Sort control (`docs/design/SPEC.md` §4 "Sort
 * button") — was a plain `<span>` (no `onClick`, no keyboard handling, no
 * `role`), so clicking or tabbing to it did nothing; this is the fix, not a
 * re-skin. "Priority" is the one real, current sort: F03 always orders the
 * board priority-first then earliest due date (`docs/design/SPEC.md` §6
 * point 6), so it is shown as the current selection, not a default guess.
 *
 * The other two options are real menu items — `role="menu"`/`"menuitem"`,
 * arrow-key navigation, outside-click/Esc to close, modelled on
 * `CardMoveAction`'s own menu rather than a new pattern — but selecting one
 * never re-sorts the already-loaded page client-side: the board's first page
 * is capped (`BoardColumnPageSize` 50, Done 20, `docs/design/COMING-SOON.md`),
 * so sorting only what happens to be loaded would silently produce a wrong
 * order once a column has more than one page. There is no server-side sort
 * parameter yet (see COMING-SOON.md), so picking "Due date" or "Recently
 * updated" opens the same `ComingSoon` toast every other blocked control
 * uses, and fires no request of its own.
 */
export function SortMenu() {
  const [open, setOpen] = useState(false)
  const [activeIndex, setActiveIndex] = useState(0)
  const comingSoon = useComingSoon('Sorting by this is coming soon — it needs a server-side sort parameter.')
  const containerRef = useRef<HTMLDivElement>(null)
  const triggerRef = useRef<HTMLButtonElement>(null)
  const itemRefs = useRef<(HTMLButtonElement | null)[]>([])

  useEffect(() => {
    if (!open) {
      return
    }

    function onPointerDown(event: PointerEvent) {
      if (event.target instanceof Node && containerRef.current?.contains(event.target) === false) {
        setOpen(false)
      }
    }

    function onKeyDown(event: KeyboardEvent) {
      if (event.key === 'Escape') {
        event.preventDefault()
        setOpen(false)
        triggerRef.current?.focus()
        return
      }

      if (event.key === 'ArrowDown' || event.key === 'ArrowUp') {
        event.preventDefault()
        setActiveIndex((index) => {
          const next = event.key === 'ArrowDown' ? (index + 1) % SORT_OPTIONS.length : (index - 1 + SORT_OPTIONS.length) % SORT_OPTIONS.length
          itemRefs.current[next]?.focus()
          return next
        })
      }
    }

    document.addEventListener('pointerdown', onPointerDown)
    document.addEventListener('keydown', onKeyDown)
    return () => {
      document.removeEventListener('pointerdown', onPointerDown)
      document.removeEventListener('keydown', onKeyDown)
    }
  }, [open])

  useEffect(() => {
    if (open) {
      itemRefs.current[activeIndex]?.focus()
    }
  }, [open, activeIndex])

  function select(option: SortOption) {
    setOpen(false)
    triggerRef.current?.focus()

    if (option !== 'priority') {
      comingSoon.show()
    }
  }

  return (
    <div ref={containerRef} className="relative">
      <button
        type="button"
        ref={triggerRef}
        aria-haspopup="menu"
        aria-expanded={open}
        title="Urgent → High → Normal, then earliest due date. Tasks with no date go last."
        className="inline-flex min-h-9 cursor-pointer items-center gap-[var(--space-1)] rounded-control-lg border border-border bg-surface px-[var(--space-3)] text-[length:var(--font-size-sm)] text-text-muted hover:bg-column"
        onClick={() => {
          setActiveIndex(0)
          setOpen((value) => !value)
        }}
      >
        <SortIcon />
        Sort: <strong className="ml-[var(--space-1)] font-semibold text-text">Priority</strong>
      </button>

      {open && (
        <ul
          role="menu"
          aria-label="Sort by"
          className="absolute top-[calc(100%+4px)] left-0 z-50 m-0 min-w-[200px] list-none rounded-md border border-border bg-surface-raised p-[var(--space-1)] shadow-raised"
        >
          {SORT_OPTIONS.map((option, index) => (
            <li key={option.value} role="none">
              <button
                type="button"
                role="menuitem"
                aria-current={option.value === 'priority' ? 'true' : undefined}
                ref={(el) => {
                  itemRefs.current[index] = el
                }}
                tabIndex={activeIndex === index ? 0 : -1}
                className={clsx(
                  'flex w-full cursor-pointer items-center justify-between gap-[var(--space-2)] rounded-sm border-0 bg-transparent px-[var(--space-2)] py-[var(--space-2)] text-left text-[length:var(--font-size-sm)] text-text hover:bg-accent-soft hover:text-accent-ink',
                  option.value === 'priority' && 'font-semibold',
                )}
                onClick={() => select(option.value)}
              >
                {option.label}
                {option.value === 'priority' && <CheckIcon />}
              </button>
            </li>
          ))}
        </ul>
      )}

      {comingSoon.message !== null && <ComingSoon message={comingSoon.message} onDismiss={comingSoon.hide} />}
    </div>
  )
}
