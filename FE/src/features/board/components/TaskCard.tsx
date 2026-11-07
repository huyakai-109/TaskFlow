import { useDraggable } from '@dnd-kit/core'
import clsx from 'clsx'
import type { CSSProperties } from 'react'
import { Link } from 'react-router-dom'

import type { TaskStatus, TaskSummary } from '../../../api/types'
import { PriorityIcon } from '../../../shared/components/PriorityIcon'
import { avatarPaletteIndex, initialsOf } from '../../../shared/lib/avatar'
import { STATUS_LABELS } from '../../../shared/lib/statusLabels'

const chipBase =
  'inline-flex items-center gap-[var(--space-1)] rounded-sm border border-border px-[var(--space-2)] py-[2px]'

const dueDateFormatter = new Intl.DateTimeFormat('en', { day: 'numeric', month: 'short' })

/**
 * `dueDate` is a calendar date with no zone of its own (F01); "today" for the
 * due chip is read from the browser's local date, which is an approximation
 * of the server's `Asia/Ho_Chi_Minh` "today" used for `isOverdue` (F05). The
 * server's `isOverdue` flag, not this comparison, is what the overdue chip
 * below is driven by.
 */
function parseDueDate(dueDate: string): Date {
  const [year, month, day] = dueDate.split('-').map(Number)
  return new Date(year, month - 1, day)
}

function isToday(dueDate: string): boolean {
  const today = new Date()
  const parsed = parseDueDate(dueDate)
  return (
    parsed.getFullYear() === today.getFullYear() &&
    parsed.getMonth() === today.getMonth() &&
    parsed.getDate() === today.getDate()
  )
}

function DueChip({ dueDate }: { dueDate: string | null }) {
  if (dueDate === null) {
    return <span className={clsx(chipBase, 'border-dashed')}>No due date</span>
  }

  if (isToday(dueDate)) {
    return <span className={clsx(chipBase, 'font-medium text-text')}>Due today</span>
  }

  return <span className={chipBase}>{dueDateFormatter.format(parseDueDate(dueDate))}</span>
}

function OverdueChip({ dueDate }: { dueDate: string | null }) {
  return (
    <span className={clsx(chipBase, 'border-transparent bg-overdue-surface font-semibold text-overdue')}>
      <svg aria-hidden="true" width="12" height="12" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2.2" strokeLinecap="round">
        <circle cx="12" cy="12" r="9" />
        <path d="M12 7v5l3 2" />
      </svg>
      {dueDate === null ? 'Overdue' : `Overdue · ${dueDateFormatter.format(parseDueDate(dueDate))}`}
    </span>
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
 * `docs/ui.md` requires for keyboard and screen-reader users. The drag handle
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

  return (
    <article
      ref={setNodeRef}
      style={style}
      className={clsx(
        'flex flex-col gap-[var(--space-2)] rounded-md border border-border bg-surface p-[var(--space-3)] shadow-card',
        task.isOverdue && 'border-overdue-border',
        isDragging && 'opacity-60 shadow-raised',
      )}
    >
      <div className="flex items-center gap-[var(--space-2)]">
        <Link
          to={`/tasks/${task.id}`}
          className="font-mono text-[length:var(--font-size-xs)] text-text-muted no-underline hover:underline"
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
            className="mr-auto inline-flex cursor-grab touch-none items-center justify-center text-text-muted [@media(pointer:coarse)]:min-h-11 [@media(pointer:coarse)]:min-w-11"
          >
            <GripIcon />
          </span>
        )}
        {task.assignee !== null && (
          <span
            className="inline-flex size-5 shrink-0 items-center justify-center rounded-pill text-[9px] font-semibold"
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
      <h3 className="m-0 text-[length:var(--font-size-base)] font-medium [line-height:1.4]">
        <Link to={`/tasks/${task.id}`} className="text-inherit no-underline hover:underline">
          {task.title}
        </Link>
      </h3>
      <div className="flex flex-wrap gap-[var(--space-2)] text-[length:var(--font-size-xs)] text-text-muted">
        <span className={chipBase}>
          <PriorityIcon priority={task.priority} />
          {task.priority}
        </span>
        {task.isOverdue ? <OverdueChip dueDate={task.dueDate} /> : <DueChip dueDate={task.dueDate} />}
      </div>
      {moves.length > 0 && (
        <div className="flex flex-wrap gap-[var(--space-2)]" role="group" aria-label={`Move "${task.title}" to`}>
          {moves.map((move) => (
            <button
              key={move.status}
              type="button"
              className="min-h-[28px] cursor-pointer rounded-sm border border-border bg-surface px-[var(--space-2)] py-[2px] text-[length:var(--font-size-xs)] font-medium text-text hover:border-accent hover:bg-accent-soft hover:text-accent-ink [@media(pointer:coarse)]:min-h-11 [@media(pointer:coarse)]:px-[var(--space-3)] [@media(pointer:coarse)]:py-0"
              onClick={() => onMove(task.id, move.status, task.version)}
            >
              Move to {STATUS_LABELS[move.status]}
            </button>
          ))}
        </div>
      )}
    </article>
  )
}
