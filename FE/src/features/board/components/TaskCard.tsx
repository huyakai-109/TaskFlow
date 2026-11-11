import { useDraggable } from '@dnd-kit/core'
import clsx from 'clsx'
import type { CSSProperties } from 'react'
import { Link } from 'react-router-dom'

import type { TaskStatus, TaskSummary } from '../../../api/types'
import { chipBase, DueChip, OverdueChip } from '../../../shared/components/DueDateChips'
import { PriorityIcon } from '../../../shared/components/PriorityIcon'
import { avatarPaletteIndex, initialsOf } from '../../../shared/lib/avatar'
import { dueDateFormatter } from '../../../shared/lib/dueDate'
import { STATUS_LABELS } from '../../../shared/lib/statusLabels'

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
        'flex flex-col gap-[var(--space-2)] rounded-md border border-border bg-surface p-[13px] shadow-card',
        task.isOverdue && 'border-overdue-border',
        isDone && 'opacity-85',
        isDragging && 'opacity-60 shadow-raised',
      )}
    >
      <div className="flex items-center gap-[var(--space-2)]">
        <Link
          to={`/tasks/${task.id}`}
          className="font-mono text-[length:var(--font-size-2xs)] text-text-muted no-underline hover:underline"
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
      <h3
        className={clsx(
          'm-0 text-[length:var(--font-size-base)] font-medium [line-height:20px]',
          isDone && 'text-text-muted',
        )}
      >
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
      {isDone && (
        <p className="m-0 flex items-center gap-[var(--space-1)] text-[length:var(--font-size-xs)] text-text-muted">
          <LockIcon />
          Locked
          {task.completedAt !== null && ` · ${dueDateFormatter.format(new Date(task.completedAt))}`}
        </p>
      )}
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
