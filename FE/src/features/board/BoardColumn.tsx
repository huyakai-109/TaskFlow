import { useDroppable } from '@dnd-kit/core'
import type { ReactNode } from 'react'

import type { TaskStatus, TaskSummary } from '../../api/types'

import styles from './BoardColumn.module.css'
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

  const dropState = !isDragging ? '' : isDropTarget ? (isOver ? styles.dropActive : styles.dropAllowed) : styles.dropDisabled

  return (
    <section ref={setNodeRef} className={`${styles.column} ${dropState}`} aria-label={label}>
      <h3 className={styles.columnTitle}>
        {icon}
        {label} <span className={styles.columnCount}>{items.length}</span>
      </h3>
      {items.length === 0 ? (
        <p className={styles.empty}>No tasks</p>
      ) : (
        items.map((task) => <TaskCard key={task.id} task={task} onMove={onMove} />)
      )}
    </section>
  )
}
