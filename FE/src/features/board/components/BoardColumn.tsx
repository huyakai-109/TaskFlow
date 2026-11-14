import { useDroppable } from '@dnd-kit/core'
import clsx from 'clsx'
import type { ReactNode } from 'react'

import type { PaginationRes, TaskStatus, TaskSummary } from '../../../api/types'

import { TaskCard } from './TaskCard'

interface BoardColumnProps {
  status: TaskStatus
  label: string
  icon: ReactNode
  items: TaskSummary[]
  /** Board page 1's pagination for this column — `null` while the board is still loading. */
  pagination: PaginationRes | null
  /** Set once "show more" is clicked or more pages are already loaded. */
  onLoadMore: () => void
  isLoadingMore: boolean
  /** A drag is in progress somewhere on the board (any card, any column). */
  isDragging: boolean
  /** This column is one of the dragged card's own `allowedTransitions` (F01 D10) — the only legal drop. */
  isDropTarget: boolean
  /** Below 640px the board shows one status at a time (`BoardPage`'s tab bar) — this column is not the one picked. */
  isMobileHidden: boolean
  onMove: (taskId: number, toStatus: TaskStatus, version: string) => void
  onTakeOver: (taskId: number, version: string) => void
}

/**
 * One board column and drop target. `useDroppable`'s `disabled` flag is what
 * makes an illegal column genuinely non-droppable (dnd-kit's own collision
 * detection skips it), not just visually dimmed — `docs/design/SPEC.md` "columns not
 * in the card's allowed list are visibly disabled".
 */
export function BoardColumn({
  status,
  label,
  icon,
  items,
  pagination,
  onLoadMore,
  isLoadingMore,
  isDragging,
  isDropTarget,
  isMobileHidden,
  onMove,
  onTakeOver,
}: BoardColumnProps) {
  const { setNodeRef, isOver } = useDroppable({ id: status, disabled: !isDropTarget })
  const totalItems = pagination?.totalItems ?? items.length
  const hasMore = items.length < totalItems

  return (
    <section
      ref={setNodeRef}
      className={clsx(
        // Flexible (layout fix, revised 2026-11-12 again) — a `grid-cols-5`
        // item (`minmax(0,1fr)` track, from the parent grid), so the column
        // shrinks and grows with the viewport instead of forcing a
        // horizontal scrollbar; `min-w-0` is what lets a grid item shrink
        // below its content's natural width at all. `@container` turns the
        // column's own rendered width into a container-query context so
        // `TaskCard` can adapt (priority label, avatar size, title clamp) —
        // Tailwind v4 built-in, no new dependency.
        'flex min-w-0 @container flex-col gap-[var(--space-2)] self-stretch rounded-lg border-2 border-transparent bg-column p-[var(--space-3)] [transition:border-color_var(--transition-fast),opacity_var(--transition-fast)]',
        isMobileHidden && 'max-[639px]:hidden',
        isDragging &&
          (isDropTarget
            ? isOver
              ? 'border-solid border-accent bg-accent-soft'
              : 'border-dashed border-accent'
            : 'opacity-45'),
      )}
      aria-label={label}
    >
      {/*
       * Sticky to the board area's own scroll (`BoardPage`'s `overflow-y-auto`
       * container, not `<main>` — the column itself never scrolls, and
       * `overflow` stays `visible` here on purpose: anything else would make
       * this column its own nearest scroll container and break the header's
       * stickiness against the board area). `bg-column`, fully opaque, keeps
       * cards from showing through as they scroll past underneath it.
       */}
      <h3 className="sticky top-0 z-[1] m-0 flex items-center gap-[var(--space-2)] bg-column px-[var(--space-1)] pt-[var(--space-1)] pb-[var(--space-2)] text-[length:var(--font-size-sm)] font-semibold">
        {icon}
        {label} <span className="font-normal text-text-muted">{totalItems}</span>
      </h3>
      {items.length === 0 ? (
        <p className="mt-[var(--space-2)] text-[length:var(--font-size-sm)] text-text-muted">No tasks</p>
      ) : (
        items.map((task) => <TaskCard key={task.id} task={task} onMove={onMove} onTakeOver={onTakeOver} />)
      )}
      {hasMore && (
        <button
          type="button"
          className="mt-[var(--space-2)] min-h-10 cursor-pointer rounded-md border border-border bg-surface px-[var(--space-3)] text-[length:var(--font-size-sm)] font-medium text-text hover:border-accent hover:bg-accent-soft hover:text-accent-ink disabled:cursor-not-allowed disabled:opacity-60"
          onClick={onLoadMore}
          disabled={isLoadingMore}
        >
          {isLoadingMore ? 'Loading…' : `Show more (${items.length} of ${totalItems})`}
        </button>
      )}
    </section>
  )
}
