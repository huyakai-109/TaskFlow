import type { ReactNode } from 'react'

import type { TaskDetail } from '../../api/types'
import { STATUS_LABELS } from '../board/statusLabels'

import styles from './PropertiesPanel.module.css'

const dateTimeFormatter = new Intl.DateTimeFormat('en', { day: 'numeric', month: 'short', year: 'numeric' })

function formatInstant(value: string): string {
  return dateTimeFormatter.format(new Date(value))
}

interface PropertyRowProps {
  label: string
  children: ReactNode
}

function PropertyRow({ label, children }: PropertyRowProps) {
  return (
    <div className={styles.row}>
      <span className={styles.label}>{label}</span>
      <span className={styles.value}>{children}</span>
    </div>
  )
}

interface PropertiesPanelProps {
  task: TaskDetail
}

/**
 * The properties panel. No "Reviewer" field: F01 D5 has no reviewer role —
 * `docs/ui.md`'s "Wireframe vs. decided specs" table says to drop it and let
 * the history timeline show who submitted the task for review instead.
 */
export function PropertiesPanel({ task }: PropertiesPanelProps) {
  return (
    <aside aria-label="Properties" className={styles.panel}>
      <PropertyRow label="Status">{STATUS_LABELS[task.status]}</PropertyRow>
      <PropertyRow label="Priority">{task.priority}</PropertyRow>
      <PropertyRow label="Assignee">{task.assignee !== null ? task.assignee.userName : 'Unassigned'}</PropertyRow>
      <PropertyRow label="Due date">
        {task.dueDate === null ? (
          'No due date'
        ) : task.isOverdue ? (
          <span className={styles.overdue}>{task.dueDate} · Overdue</span>
        ) : (
          task.dueDate
        )}
      </PropertyRow>
      <PropertyRow label="Created">{formatInstant(task.createdAt)}</PropertyRow>
      <PropertyRow label="Updated">{formatInstant(task.updatedAt)}</PropertyRow>
    </aside>
  )
}
