import { useDroppable } from '@dnd-kit/core'
import clsx from 'clsx'
import type { ReactNode } from 'react'

import type { TaskStatus, TaskSummary } from '../../../api/types'

import { TaskCard } from './TaskCard'

interface BoardColumnProps {
  status: TaskStatus
  label: string
  icon: ReactNode
  items: TaskSummary[]
  /** A drag is in progress somewhere on the board (any card, any column). */
  isDragging: boolean
  /** This column is one of the dragged card's own `allowedTransitions` (F01 D10) — the only legal drop. */
  isDropTarget: boolean
  onMove: (taskId: number, toStatus: TaskStatus, version: string) => void
}

/**
 * One board column and drop target. `useDroppable`'s `disabled` flag is what
 * makes an illegal column genuinely non-droppable (dnd-kit's own collision
 * detection skips it), not just visually dimmed — `docs/ui.md` "columns not
 * in the card's allowed list are visibly disabled".
 */
export function BoardColumn({ status, label, icon, items, isDragging, isDropTarget, onMove }: BoardColumnProps) {
  const { setNodeRef, isOver } = useDroppable({ id: status, disabled: !isDropTarget })

  return (
    <section
      ref={setNodeRef}
      className={clsx(
        'flex flex-col gap-[var(--space-2)] rounded-lg border-2 border-transparent bg-column p-[var(--space-3)] [transition:border-color_var(--transition-fast),opacity_var(--transition-fast)]',
        isDragging &&
          (isDropTarget
            ? isOver
              ? 'border-solid border-accent bg-accent-soft'
              : 'border-dashed border-accent'
            : 'opacity-45'),
      )}
      aria-label={label}
    >
      <h3 className="m-0 flex items-center gap-[var(--space-2)] px-[var(--space-1)] pt-[var(--space-1)] pb-[var(--space-2)] text-[length:var(--font-size-sm)] font-semibold">
        {icon}
        {label} <span className="font-normal text-text-muted">{items.length}</span>
      </h3>
      {items.length === 0 ? (
        <p className="mt-[var(--space-2)] text-[length:var(--font-size-sm)] text-text-muted">No tasks</p>
      ) : (
        items.map((task) => <TaskCard key={task.id} task={task} onMove={onMove} />)
      )}
    </section>
  )
}
