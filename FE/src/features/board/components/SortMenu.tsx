import clsx from 'clsx'
import { useEffect, useRef, useState } from 'react'

import type { BoardSort } from '../../../api/types'

const SORT_OPTIONS: ReadonlyArray<{ value: BoardSort; label: string }> = [
  { value: 'priority', label: 'Priority' },
  { value: 'dueDate', label: 'Due date' },
  { value: 'newest', label: 'Newest' },
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

interface SortMenuProps {
  value: BoardSort
  onChange: (value: BoardSort) => void
}

/**
 * The Board/List toolbar's Sort control (`docs/design/SPEC.md` §4 "Sort
 * button"). All three options are real now (f10-read-models-and-sort.md §1,
 * R2-5): `priority` (F03's own rule, still the default), `dueDate` and
 * `newest` both call the server's `sort` query parameter on `GET
 * /tasks/board`/`GET /tasks` — never a client-side re-sort of a capped,
 * possibly-partial page (`BoardColumnPageSize` 50, Done 20), which is exactly
 * why this stayed a `ComingSoon` stub before the parameter existed
 * (`docs/design/COMING-SOON.md` B7). `Done` always ignores the server's own
 * order regardless of which option is selected (f10 §1), unchanged here.
 */
export function SortMenu({ value, onChange }: SortMenuProps) {
  const [open, setOpen] = useState(false)
  const [activeIndex, setActiveIndex] = useState(() => SORT_OPTIONS.findIndex((option) => option.value === value))
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

  function select(option: BoardSort) {
    setOpen(false)
    triggerRef.current?.focus()
    onChange(option)
  }

  const currentLabel = SORT_OPTIONS.find((option) => option.value === value)?.label ?? 'Priority'

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
          setActiveIndex(SORT_OPTIONS.findIndex((option) => option.value === value))
          setOpen((open) => !open)
        }}
      >
        <SortIcon />
        Sort: <strong className="ml-[var(--space-1)] font-semibold text-text">{currentLabel}</strong>
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
                aria-current={option.value === value ? 'true' : undefined}
                ref={(el) => {
                  itemRefs.current[index] = el
                }}
                tabIndex={activeIndex === index ? 0 : -1}
                className={clsx(
                  'flex w-full cursor-pointer items-center justify-between gap-[var(--space-2)] rounded-sm border-0 bg-transparent px-[var(--space-2)] py-[var(--space-2)] text-left text-[length:var(--font-size-sm)] text-text hover:bg-accent-soft hover:text-accent-ink',
                  option.value === value && 'font-semibold',
                )}
                onClick={() => select(option.value)}
              >
                {option.label}
                {option.value === value && <CheckIcon />}
              </button>
            </li>
          ))}
        </ul>
      )}
    </div>
  )
}
