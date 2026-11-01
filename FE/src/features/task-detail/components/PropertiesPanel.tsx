import type { ReactNode } from 'react'

import type { TaskDetail, TaskPriority } from '../../../api/types'
import { STATUS_LABELS } from '../../../shared/lib/statusLabels'

import styles from './PropertiesPanel.module.css'

const dateTimeFormatter = new Intl.DateTimeFormat('en', { day: 'numeric', month: 'short', year: 'numeric' })

function formatInstant(value: string): string {
  return dateTimeFormatter.format(new Date(value))
}

interface PropertyRowProps {
  label: string
  htmlFor?: string
  children: ReactNode
}

function PropertyRow({ label, htmlFor, children }: PropertyRowProps) {
  return (
    <div className={styles.row}>
      {htmlFor === undefined ? (
        <span className={styles.label}>{label}</span>
      ) : (
        <label htmlFor={htmlFor} className={styles.label}>
          {label}
        </label>
      )}
      <span className={styles.value}>{children}</span>
    </div>
  )
}

const PRIORITIES: TaskPriority[] = ['Urgent', 'High', 'Normal']

interface PropertiesPanelProps {
  task: TaskDetail
  onEditPriority: (priority: TaskPriority) => void
  isPriorityPending: boolean
  priorityError: string | null
  onEditDueDate: (dueDate: string | null) => void
  isDueDatePending: boolean
  dueDateError: string | null
  onOpenAssigneePicker: () => void
  isAssigneePending: boolean
}

/**
 * The properties panel. No "Reviewer" field: F01 D5 has no reviewer role —
 * `docs/ui.md`'s "Wireframe vs. decided specs" table says to drop it and let
 * the history timeline show who submitted the task for review instead.
 *
 * Priority and due date are edited inline (`PUT …/priority`, `PUT …/due-date`);
 * assignee opens `AssigneePickerDialog` instead of an inline control, because
 * "Unassigned" plus a names list does not fit a one-line row as cleanly as a
 * dropdown does for priority — `docs/ui.md` does not say either way for this
 * screen's not-yet-designed controls, so this is a deliberate pick, not a
 * guess at a spec.
 */
export function PropertiesPanel({
  task,
  onEditPriority,
  isPriorityPending,
  priorityError,
  onEditDueDate,
  isDueDatePending,
  dueDateError,
  onOpenAssigneePicker,
  isAssigneePending,
}: PropertiesPanelProps) {
  return (
    <aside aria-label="Properties" className={styles.panel}>
      <PropertyRow label="Status">{STATUS_LABELS[task.status]}</PropertyRow>

      <PropertyRow label="Priority" htmlFor="pp-priority">
        <select
          id="pp-priority"
          className={styles.select}
          value={task.priority}
          disabled={isPriorityPending}
          onChange={(event) => onEditPriority(event.target.value as TaskPriority)}
        >
          {PRIORITIES.map((priority) => (
            <option key={priority} value={priority}>
              {priority}
            </option>
          ))}
        </select>
        {isPriorityPending && (
          <span role="status" className={styles.pending}>
            Saving…
          </span>
        )}
      </PropertyRow>
      {priorityError !== null && (
        <p role="alert" className={styles.fieldError}>
          {priorityError}
        </p>
      )}

      <PropertyRow label="Assignee">
        <button
          type="button"
          className={styles.linkButton}
          disabled={isAssigneePending}
          onClick={onOpenAssigneePicker}
        >
          {task.assignee !== null ? task.assignee.userName : 'Unassigned'}
        </button>
        {isAssigneePending && (
          <span role="status" className={styles.pending}>
            Saving…
          </span>
        )}
      </PropertyRow>

      <PropertyRow label="Due date" htmlFor="pp-due-date">
        <input
          id="pp-due-date"
          type="date"
          className={styles.dateInput}
          value={task.dueDate ?? ''}
          disabled={isDueDatePending}
          onChange={(event) => onEditDueDate(event.target.value.length > 0 ? event.target.value : null)}
        />
        {task.dueDate !== null && (
          <button
            type="button"
            className={styles.linkButton}
            disabled={isDueDatePending}
            onClick={() => onEditDueDate(null)}
          >
            Clear
          </button>
        )}
        {task.isOverdue && <span className={styles.overdue}>Overdue</span>}
        {isDueDatePending && (
          <span role="status" className={styles.pending}>
            Saving…
          </span>
        )}
      </PropertyRow>
      {dueDateError !== null && (
        <p role="alert" className={styles.fieldError}>
          {dueDateError}
        </p>
      )}

      <PropertyRow label="Created">{formatInstant(task.createdAt)}</PropertyRow>
      <PropertyRow label="Updated">{formatInstant(task.updatedAt)}</PropertyRow>
    </aside>
  )
}
