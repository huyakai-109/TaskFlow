import type { TaskPriority, TaskSummary } from '../../api/types'

import { STATUS_LABELS } from './statusLabels'
import styles from './TaskCard.module.css'

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
    return <span className={`${styles.chip} ${styles.chipDashed}`}>No due date</span>
  }

  if (isToday(dueDate)) {
    return <span className={`${styles.chip} ${styles.chipEmphasis}`}>Due today</span>
  }

  return <span className={styles.chip}>{dueDateFormatter.format(parseDueDate(dueDate))}</span>
}

function OverdueChip({ dueDate }: { dueDate: string | null }) {
  return (
    <span className={`${styles.chip} ${styles.chipOverdue}`}>
      <svg aria-hidden="true" width="12" height="12" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2.2" strokeLinecap="round">
        <circle cx="12" cy="12" r="9" />
        <path d="M12 7v5l3 2" />
      </svg>
      {dueDate === null ? 'Overdue' : `Overdue · ${dueDateFormatter.format(parseDueDate(dueDate))}`}
    </span>
  )
}

function PriorityIcon({ priority }: { priority: TaskPriority }) {
  if (priority === 'Urgent') {
    return (
      <svg aria-hidden="true" width="12" height="12" viewBox="0 0 16 16">
        <rect x="1" y="1" width="14" height="14" rx="3.5" fill="var(--color-priority-urgent)" />
        <path d="M8 4.5v4.5M8 11.5v.2" stroke="var(--color-surface)" strokeWidth="1.9" strokeLinecap="round" />
      </svg>
    )
  }

  const filledBars = priority === 'High' ? 3 : 2
  const color = priority === 'High' ? 'var(--color-priority-high)' : 'var(--color-priority-normal)'
  const empty = 'var(--color-meter-empty)'

  return (
    <svg aria-hidden="true" width="12" height="12" viewBox="0 0 16 16">
      <rect x="1.5" y="9" width="3" height="5" rx="1" fill={filledBars >= 1 ? color : empty} />
      <rect x="6.5" y="5.5" width="3" height="8.5" rx="1" fill={filledBars >= 2 ? color : empty} />
      <rect x="11.5" y="2" width="3" height="12" rx="1" fill={filledBars >= 3 ? color : empty} />
    </svg>
  )
}

function initialsOf(userName: string): string {
  return userName.slice(0, 2).toUpperCase()
}

/** A stable background/ink pair per person (`docs/ui.md` Avatar), from a fixed six-colour palette. */
function avatarPaletteIndex(userId: number): number {
  return ((userId - 1) % 6) + 1
}

interface TaskCardProps {
  task: TaskSummary
}

/**
 * A read-only board card. The allowed-move list underneath is this slice's
 * drop-target affordance: it is exactly what a drag onto another column would
 * be allowed to do (F01 D10), shown as plain text because dragging itself is
 * slice 1.6 — see `docs/ui.md` "Keep a non-drag path".
 */
export function TaskCard({ task }: TaskCardProps) {
  const moves = task.allowedTransitions.filter((t) => t.operation === 'Move')

  return (
    <article className={`${styles.card} ${task.isOverdue ? styles.cardOverdue : ''}`} aria-label={task.title}>
      <div className={styles.cardHeader}>
        <span className={styles.key}>#{task.id}</span>
        {task.assignee !== null && (
          <span
            className={styles.avatar}
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
      <h3 className={styles.title}>{task.title}</h3>
      <div className={styles.chips}>
        <span className={styles.chip}>
          <PriorityIcon priority={task.priority} />
          {task.priority}
        </span>
        {task.isOverdue ? <OverdueChip dueDate={task.dueDate} /> : <DueChip dueDate={task.dueDate} />}
      </div>
      {moves.length > 0 && (
        <p className={styles.moves}>Can move to {moves.map((m) => STATUS_LABELS[m.status]).join(', ')}</p>
      )}
    </article>
  )
}
