import clsx from 'clsx'
import { useRef } from 'react'

import { describeErrorCode } from '../../../api/errorMessages'
import { useUsersQuery } from '../../../shared/hooks/useUsersQuery'
import { useDialogA11y } from '../../../shared/hooks/useDialogA11y'

interface AssigneePickerDialogProps {
  currentAssigneeId: number | null
  isPending: boolean
  errorCode: string | null
  /** The server's raw `error` string — shown verbatim for `Task.Assign.CapacityReached`. */
  errorMessage?: string | null
  onClose: () => void
  onSelect: (assigneeId: number | null) => void
}

const optionClass =
  'flex min-h-10 w-full cursor-pointer items-center rounded-sm border-0 bg-transparent px-[var(--space-3)] text-left text-text [font:inherit] [&:hover:not(:disabled)]:bg-accent-soft disabled:cursor-not-allowed disabled:opacity-60'

/**
 * `docs/design/SPEC.md` §5.5 and `docs/design/png/AssignPicker.png`, minus its "has room" sort and
 * "at capacity" disabling — both are F02's rejected slice 3.5 ("Workload in
 * the picker", not scheduled, `docs/ROADMAP.md` unit 3). Listed
 * alphabetically; everyone enabled. Stays open on a non-conflict failure
 * (e.g. `AssigneeNotFound`, `CapacityReached`) so the user can pick again; a
 * conflict closes it, because the page's own refusal handler already
 * refreshes the task.
 */
export function AssigneePickerDialog({
  currentAssigneeId,
  isPending,
  errorCode,
  errorMessage = null,
  onClose,
  onSelect,
}: AssigneePickerDialogProps) {
  const usersQuery = useUsersQuery()
  const assignees = usersQuery.data ?? []
  const closeButtonRef = useRef<HTMLButtonElement>(null)

  useDialogA11y(closeButtonRef, onClose, isPending)

  return (
    <div
      className="fixed inset-0 z-[100] flex items-center justify-center bg-[rgb(16_16_24_/_40%)] p-[var(--space-4)]"
      onClick={() => !isPending && onClose()}
    >
      <div
        role="dialog"
        aria-modal="true"
        aria-labelledby="ap-title"
        className="flex w-full max-w-[360px] flex-col gap-[var(--space-3)] rounded-xl bg-surface-raised p-[var(--space-5)] shadow-raised"
        onClick={(event) => event.stopPropagation()}
      >
        <div className="flex items-center gap-[var(--space-2)]">
          <h2 id="ap-title" className="m-0 text-[length:var(--font-size-lg)] font-semibold">
            Assign to
          </h2>
          <button
            type="button"
            ref={closeButtonRef}
            aria-label="Close"
            className="ml-auto inline-flex size-8 cursor-pointer items-center justify-center rounded-sm border-0 bg-transparent text-[length:var(--font-size-lg)] text-text-muted disabled:cursor-not-allowed disabled:opacity-60"
            onClick={onClose}
            disabled={isPending}
          >
            ×
          </button>
        </div>

        <p className="m-0 text-[length:var(--font-size-xs)] text-text-muted">
          Listed alphabetically, everyone shown — the workload limit isn&rsquo;t shown here.
        </p>

        {errorCode !== null && (
          <p
            role="alert"
            className="m-0 rounded-md border border-danger-border bg-danger-surface p-[var(--space-3)] text-[length:var(--font-size-sm)] text-danger"
          >
            {describeErrorCode(errorCode, errorMessage)}
          </p>
        )}

        {usersQuery.isError && (
          <p role="alert" className="m-0 text-[length:var(--font-size-sm)] text-danger">
            The list of people could not be loaded. Try closing and reopening this dialog.
          </p>
        )}

        <ul role="listbox" aria-label="Assign to" className="m-0 flex list-none flex-col p-0">
          <li role="option" aria-selected={currentAssigneeId === null}>
            <button
              type="button"
              className={clsx(optionClass, currentAssigneeId === null && 'bg-accent-soft font-semibold text-accent-ink')}
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
                className={clsx(
                  optionClass,
                  currentAssigneeId === assignee.id && 'bg-accent-soft font-semibold text-accent-ink',
                )}
                disabled={isPending}
                onClick={() => onSelect(assignee.id)}
              >
                {assignee.userName}
              </button>
            </li>
          ))}
        </ul>

        {usersQuery.isPending && (
          <p role="status" className="m-0 text-[length:var(--font-size-sm)] text-text-muted">
            Loading people…
          </p>
        )}

        {usersQuery.isSuccess && assignees.length === 0 && (
          <p className="m-0 text-[length:var(--font-size-sm)] text-text-muted">
            No one is in the directory yet, so there is no one to pick besides Unassigned.
          </p>
        )}
      </div>
    </div>
  )
}
