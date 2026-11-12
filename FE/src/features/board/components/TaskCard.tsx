import { useDraggable } from '@dnd-kit/core'
import clsx from 'clsx'
import type { CSSProperties } from 'react'
import { Link } from 'react-router-dom'

import type { TaskStatus, TaskSummary } from '../../../api/types'
import { CardMoveAction } from '../../../shared/components/CardMoveAction'
import { chipBase, DueChip, OverdueChip } from '../../../shared/components/DueDateChips'
import { PriorityIcon } from '../../../shared/components/PriorityIcon'
import { avatarPaletteIndex, initialsOf } from '../../../shared/lib/avatar'
import { dueDateFormatter } from '../../../shared/lib/dueDate'

function LockIcon() {
  return (
    <svg aria-hidden="true" width="11" height="11" viewBox="0 0 16 16" fill="none" stroke="currentColor" strokeWidth="1.6" strokeLinecap="round" strokeLinejoin="round">
      <rect x="3" y="7" width="10" height="7" rx="1.5" />
      <path d="M5.5 7V5a2.5 2.5 0 0 1 5 0v2" />
    </svg>
  )
}

function GripIcon() {
  return (
    <svg aria-hidden="true" width="12" height="12" viewBox="0 0 16 16" fill="currentColor">
      <circle cx="5" cy="4" r="1.3" />
      <circle cx="11" cy="4" r="1.3" />
      <circle cx="5" cy="8" r="1.3" />
      <circle cx="11" cy="8" r="1.3" />
      <circle cx="5" cy="12" r="1.3" />
      <circle cx="11" cy="12" r="1.3" />
    </svg>
  )
}

interface TaskCardProps {
  task: TaskSummary
  onMove: (taskId: number, toStatus: TaskStatus, version: string) => void
}

/**
 * A board card: draggable to any column in its own `allowedTransitions` (F01
 * D10), plus the same moves as plain buttons underneath — the "non-drag path"
 * `docs/design/SPEC.md` requires for keyboard and screen-reader users. The drag handle
 * itself is `aria-hidden` and out of the tab order: it is a pointer-only
 * shortcut, not a second way to reach the same action for keyboard users, who
 * use the buttons instead.
 */
export function TaskCard({ task, onMove }: TaskCardProps) {
  const moves = task.allowedTransitions.filter((t) => t.operation === 'Move')
  const { setNodeRef, setActivatorNodeRef, listeners, attributes, transform, isDragging } = useDraggable({
    id: `task-${task.id}`,
    data: { task },
    disabled: moves.length === 0,
  })

  const style: CSSProperties | undefined =
    transform !== null ? { transform: `translate3d(${transform.x}px, ${transform.y}px, 0)`, zIndex: 10 } : undefined

  const isDone = task.status === 'Done'

  return (
    <article
      ref={setNodeRef}
      style={style}
      className={clsx(
        // No `overflow-hidden` (layout fix, 2026-11-12 again) — the move
        // action's popover menu needs to be able to extend past the card's
        // own edge when it opens; `min-w-0`/`break-words`/chip
        // `whitespace-nowrap` below are what actually keep content inside
        // the card, not a clip.
        'group flex min-w-0 flex-col gap-[var(--space-2)] rounded-md border border-border bg-surface p-[13px] shadow-card',
        task.isOverdue && 'border-overdue-border',
        isDone && 'opacity-85',
        isDragging && 'opacity-60 shadow-raised',
      )}
    >
      {/*
       * `docs/design/SPEC.md` §10 point 11 (layout fix, 2026-11-12 again):
       * the action button lives in this first row, in the gap between the
       * key and the avatar — never an overlay on the chips/title below, so
       * hovering a card never changes its height or covers its own content.
       */}
      <div className="flex min-w-0 items-center gap-[var(--space-2)]">
        <Link
          to={`/tasks/${task.id}`}
          className="shrink-0 font-mono text-[length:var(--font-size-2xs)] text-text-muted no-underline hover:underline"
        >
          #{task.id}
        </Link>
        {moves.length > 0 && (
          <span
            ref={setActivatorNodeRef}
            {...listeners}
            {...attributes}
            aria-hidden="true"
            tabIndex={-1}
            title="Drag to move"
            className="inline-flex shrink-0 cursor-grab touch-none items-center justify-center text-text-muted opacity-0 [transition:opacity_var(--transition-fast)] group-hover:opacity-100 group-focus-within:opacity-100 [@media(hover:none)]:opacity-100 [@media(pointer:coarse)]:min-h-11 [@media(pointer:coarse)]:min-w-11"
          >
            <GripIcon />
          </span>
        )}
        <span className="min-w-0 flex-1" />
        <CardMoveAction taskId={task.id} taskTitle={task.title} taskStatus={task.status} moves={moves} onMove={(status) => onMove(task.id, status, task.version)} />
        {task.assignee !== null && (
          <span
            className="inline-flex size-4 shrink-0 items-center justify-center rounded-pill text-[9px] leading-none font-semibold @min-[170px]:size-5"
            aria-label={`Assigned to ${task.assignee.userName}`}
            style={{
              backgroundColor: `var(--avatar-${avatarPaletteIndex(task.assignee.id)}-bg)`,
              color: `var(--avatar-${avatarPaletteIndex(task.assignee.id)}-ink)`,
            }}
          >
            {initialsOf(task.assignee.userName)}
          </span>
        )}
      </div>
      {/*
       * Container-query tiers against the column's own rendered width
       * (`@container` on `BoardColumn`, Tailwind v4 built-in — layout fix,
       * 2026-11-12 again): below 170px the title clamps to 3 lines instead
       * of pushing the card arbitrarily tall; at 170px and up it reads in
       * full, same as always.
       */}
      <h3
        className={clsx(
          'm-0 line-clamp-3 min-w-0 text-[length:var(--font-size-base)] font-medium break-words [line-height:20px] @min-[170px]:line-clamp-none',
          isDone && 'text-text-muted',
        )}
      >
        <Link to={`/tasks/${task.id}`} className="text-inherit no-underline hover:underline">
          {task.title}
        </Link>
      </h3>
      {/*
       * Priority + due/overdue chip on one row (`docs/design/SPEC.md` §4) —
       * `flex-wrap` is a last-resort fallback only (a whole chip drops to a
       * second row, never breaks text inside one: `chipBase` carries
       * `whitespace-nowrap shrink-0`); the column width below is chosen so
       * this normally never triggers.
       */}
      <div className="flex flex-wrap gap-[var(--space-1)] text-[length:var(--font-size-xs)] text-text-muted">
        {/*
         * Below 170px the priority chip drops its text label (icon +
         * `aria-label` only) — the label stays for assistive tech even
         * though it's visually hidden, same container-query tier as the
         * title clamp and avatar size above.
         */}
        <span className={chipBase} aria-label={`Priority: ${task.priority}`}>
          <PriorityIcon priority={task.priority} />
          <span className="hidden @min-[170px]:inline">{task.priority}</span>
        </span>
        {task.isOverdue ? <OverdueChip dueDate={task.dueDate} /> : <DueChip dueDate={task.dueDate} />}
      </div>
      {isDone && (
        <p className="m-0 flex items-center gap-[var(--space-1)] text-[length:var(--font-size-xs)] text-text-muted">
          <LockIcon />
          Locked
          {task.completedAt !== null && ` · ${dueDateFormatter.format(new Date(task.completedAt))}`}
        </p>
      )}
    </article>
  )
}
