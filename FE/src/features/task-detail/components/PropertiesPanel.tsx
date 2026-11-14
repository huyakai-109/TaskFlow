import type { ReactNode } from 'react'

import type { TaskDetail, TaskPriority } from '../../../api/types'
import { PriorityIcon } from '../../../shared/components/PriorityIcon'
import { useHasPermission } from '../../../shared/hooks/useHasPermission'
import { useUsersQuery } from '../../../shared/hooks/useUsersQuery'
import { STATUS_LABELS } from '../../../shared/lib/statusLabels'

const dateTimeFormatter = new Intl.DateTimeFormat('en', { day: 'numeric', month: 'short', year: 'numeric' })

function formatInstant(value: string): string {
  return dateTimeFormatter.format(new Date(value))
}

const labelClass = 'text-[length:var(--font-size-sm)] text-text-muted'
const selectOrDateInputClass =
  'min-h-9 rounded-sm border border-border bg-surface px-[var(--space-2)] text-[length:var(--font-size-sm)] text-text [font:inherit] disabled:opacity-60'
const linkButtonClass =
  'cursor-pointer border-0 bg-transparent text-[length:var(--font-size-sm)] text-accent-ink underline [font:inherit] disabled:cursor-not-allowed disabled:text-text-muted disabled:no-underline'
const pendingClass = 'text-[length:var(--font-size-xs)] text-text-muted'
const fieldErrorClass = 'm-0 text-[length:var(--font-size-xs)] text-danger'

interface PropertyRowProps {
  label: string
  htmlFor?: string
  children: ReactNode
}

function PropertyRow({ label, htmlFor, children }: PropertyRowProps) {
  return (
    <div className="grid min-h-10 grid-cols-[96px_minmax(0,1fr)] items-center gap-[var(--space-2)]">
      {htmlFor === undefined ? (
        <span className={labelClass}>{label}</span>
      ) : (
        <label htmlFor={htmlFor} className={labelClass}>
          {label}
        </label>
      )}
      <span className="flex flex-wrap items-center gap-[var(--space-2)] text-[length:var(--font-size-base)]">
        {children}
      </span>
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
 * `docs/design/SPEC.md`'s "Wireframe vs. decided specs" table says to drop it and let
 * the history timeline show who submitted the task for review instead.
 *
 * Priority and due date are edited inline (`PUT …/priority`, `PUT …/due-date`);
 * assignee opens `AssigneePickerDialog` instead of an inline control, because
 * "Unassigned" plus a names list does not fit a one-line row as cleanly as a
 * dropdown does for priority — `docs/design/SPEC.md` does not say either way for this
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
  const usersQuery = useUsersQuery()
  const createdByName = usersQuery.data?.find((user) => user.id === task.createdBy)?.userName ?? null
  const canPlan = useHasPermission('PlanTasks')
  // Revision R2 (D12, A3): the assignee is locked while Review/Done for
  // every role — the picker is never opened for those statuses rather than
  // opened and then refused (`docs/design/SPEC.md` §10 point 17).
  const assignmentLocked = task.status === 'Review' || task.status === 'Done'

  return (
    <aside
      aria-label="Properties"
      className="flex min-w-[260px] max-w-[300px] flex-[1_1_300px] flex-col gap-[var(--space-1)] rounded-xl border border-border bg-side-panel p-[var(--space-5)]"
    >
      <PropertyRow label="Status">{STATUS_LABELS[task.status]}</PropertyRow>

      <PropertyRow label="Priority" htmlFor={canPlan ? 'pp-priority' : undefined}>
        {canPlan ? (
          <select
            id="pp-priority"
            className={selectOrDateInputClass}
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
        ) : (
          <span className="inline-flex items-center gap-[var(--space-2)]">
            <PriorityIcon priority={task.priority} />
            {task.priority}
          </span>
        )}
        {isPriorityPending && (
          <span role="status" className={pendingClass}>
            Saving…
          </span>
        )}
      </PropertyRow>
      {priorityError !== null && (
        <p role="alert" className={fieldErrorClass}>
          {priorityError}
        </p>
      )}

      <PropertyRow label="Assignee">
        {assignmentLocked ? (
          <span className={pendingClass}>
            {task.assignee !== null ? task.assignee.userName : 'Unassigned'} — locked while {STATUS_LABELS[task.status]}
          </span>
        ) : (
          <button type="button" className={linkButtonClass} disabled={isAssigneePending} onClick={onOpenAssigneePicker}>
            {task.assignee !== null ? task.assignee.userName : 'Unassigned'}
          </button>
        )}
        {isAssigneePending && (
          <span role="status" className={pendingClass}>
            Saving…
          </span>
        )}
      </PropertyRow>

      <PropertyRow label="Due date" htmlFor={canPlan ? 'pp-due-date' : undefined}>
        {canPlan ? (
          <input
            id="pp-due-date"
            type="date"
            className={selectOrDateInputClass}
            value={task.dueDate ?? ''}
            disabled={isDueDatePending}
            onChange={(event) => onEditDueDate(event.target.value.length > 0 ? event.target.value : null)}
          />
        ) : (
          <span>{task.dueDate ?? 'No due date'}</span>
        )}
        {canPlan && task.dueDate !== null && (
          <button type="button" className={linkButtonClass} disabled={isDueDatePending} onClick={() => onEditDueDate(null)}>
            Clear
          </button>
        )}
        {task.isOverdue && <span className="font-semibold text-overdue">Overdue</span>}
        {isDueDatePending && (
          <span role="status" className={pendingClass}>
            Saving…
          </span>
        )}
      </PropertyRow>
      {task.isOverdue && canPlan && (
        <p className="m-0 text-[length:var(--font-size-xs)] text-text-muted">
          Moving the date into the future clears the overdue flag.
        </p>
      )}
      {dueDateError !== null && (
        <p role="alert" className={fieldErrorClass}>
          {dueDateError}
        </p>
      )}

      <PropertyRow label="Created">
        {formatInstant(task.createdAt)}
        {createdByName !== null && ` by ${createdByName}`}
      </PropertyRow>
      <PropertyRow label="Updated">{formatInstant(task.updatedAt)}</PropertyRow>
    </aside>
  )
}
