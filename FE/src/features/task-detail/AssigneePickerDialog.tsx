import { useRef } from 'react'

import { describeErrorCode } from '../../api/errorMessages'
import { useKnownAssignees } from '../../shared/useKnownAssignees'
import { useDialogA11y } from '../../shared/useDialogA11y'

import styles from './AssigneePickerDialog.module.css'

interface AssigneePickerDialogProps {
  currentAssigneeId: number | null
  isPending: boolean
  errorCode: string | null
  onClose: () => void
  onSelect: (assigneeId: number | null) => void
}

/**
 * `docs/ui/screens/AssignPicker.dc.html`, minus its "has room" sort and
 * "at capacity" disabling: both are F02, not built yet (`useKnownAssignees`).
 * Listed alphabetically; everyone enabled. Stays open on a non-conflict
 * failure (e.g. `AssigneeNotFound`) so the user can pick again; a conflict
 * closes it, because the page's own refusal handler already refreshes the
 * task.
 */
export function AssigneePickerDialog({
  currentAssigneeId,
  isPending,
  errorCode,
  onClose,
  onSelect,
}: AssigneePickerDialogProps) {
  const assignees = useKnownAssignees()
  const closeButtonRef = useRef<HTMLButtonElement>(null)

  useDialogA11y(closeButtonRef, onClose, isPending)

  return (
    <div className={styles.overlay} onClick={() => !isPending && onClose()}>
      <div
        role="dialog"
        aria-modal="true"
        aria-labelledby="ap-title"
        className={styles.dialog}
        onClick={(event) => event.stopPropagation()}
      >
        <div className={styles.header}>
          <h2 id="ap-title" className={styles.heading}>
            Assign to
          </h2>
          <button
            type="button"
            ref={closeButtonRef}
            aria-label="Close"
            className={styles.closeButton}
            onClick={onClose}
            disabled={isPending}
          >
            ×
          </button>
        </div>

        <p className={styles.hint}>
          Listed alphabetically, everyone shown — the workload limit (F02) isn&rsquo;t built yet, so nobody is marked
          full.
        </p>

        {errorCode !== null && (
          <p role="alert" className={styles.serverError}>
            {describeErrorCode(errorCode)}
          </p>
        )}

        <ul role="listbox" aria-label="Assign to" className={styles.list}>
          <li role="option" aria-selected={currentAssigneeId === null}>
            <button
              type="button"
              className={`${styles.option} ${currentAssigneeId === null ? styles.optionSelected : ''}`}
              disabled={isPending}
              onClick={() => onSelect(null)}
            >
              Unassigned
            </button>
          </li>
          {assignees.map((assignee) => (
            <li key={assignee.id} role="option" aria-selected={currentAssigneeId === assignee.id}>
              <button
                type="button"
                className={`${styles.option} ${currentAssigneeId === assignee.id ? styles.optionSelected : ''}`}
                disabled={isPending}
                onClick={() => onSelect(assignee.id)}
              >
                {assignee.userName}
              </button>
            </li>
          ))}
        </ul>

        {assignees.length === 0 && (
          <p className={styles.note}>No one is assigned to any task yet, so there is no one to pick besides Unassigned.</p>
        )}
      </div>
    </div>
  )
}
