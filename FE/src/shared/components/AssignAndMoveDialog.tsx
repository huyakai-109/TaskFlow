import clsx from 'clsx'
import { useRef } from 'react'

import { describeErrorCode } from '../../api/errorMessages'
import { useDialogA11y } from '../hooks/useDialogA11y'
import { useUsersQuery } from '../hooks/useUsersQuery'
import { avatarPaletteIndex, initialsOf } from '../lib/avatar'

function PersonIcon() {
  return (
    <svg aria-hidden="true" width="16" height="16" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="1.8" strokeLinecap="round" strokeLinejoin="round">
      <circle cx="12" cy="8" r="4" />
      <path d="M4 20c0-4.4 3.6-7 8-7s8 2.6 8 7" />
    </svg>
  )
}

interface AssignAndMoveDialogProps {
  taskTitle: string
  /** Display label for the target column, e.g. "In Progress". */
  toStatusLabel: string
  /**
   * Why this dialog opened — `Task.Move.AssigneeRequired`'s generic sentence
   * by default, or the server's own `Task.Move.CapacityReached` message
   * (names the current assignee and the limit) when that's what triggered
   * it. Same dialog, same chained assign-then-move, different opening line.
   */
  reasonMessage?: string
  isPending: boolean
  errorCode: string | null
  /** The server's raw `error` string — shown verbatim for `CapacityReached`. */
  errorMessage: string | null
  onClose: () => void
  onSelect: (assigneeId: number) => void
}

const optionClass =
  'flex min-h-11 w-full cursor-pointer items-center rounded-sm border-0 bg-transparent px-[var(--space-3)] text-left text-text [font:inherit] [&:hover:not(:disabled)]:bg-accent-soft disabled:cursor-not-allowed disabled:opacity-60'

/**
 * `Task.Move.AssigneeRequired`'s way out: `docs/design/SPEC.md` §5.6, "the client
 * may chain both calls behind one button" — picking a name here does both
 * `PUT …/assignee` and `PUT …/status` (`useAssignAndMoveTaskMutation`). No
 * "Unassigned" option, unlike `AssigneePickerDialog`: an assignee is exactly
 * what this task is missing (F02 D5). Listed alphabetically, everyone
 * enabled — the "has room" sort and "at capacity" disabling from
 * `docs/design/SPEC.md` §5.5 and `docs/design/png/AssignPicker.png` are slice 3.5, not scheduled.
 */
export function AssignAndMoveDialog({
  taskTitle,
  toStatusLabel,
  reasonMessage,
  isPending,
  errorCode,
  errorMessage,
  onClose,
  onSelect,
}: AssignAndMoveDialogProps) {
  const usersQuery = useUsersQuery()
  const assignees = usersQuery.data ?? []
  const closeButtonRef = useRef<HTMLButtonElement>(null)

  useDialogA11y(closeButtonRef, onClose, isPending)

  return (
    <div
      className="fixed inset-0 z-[100] flex items-center justify-center bg-overlay p-[var(--space-4)]"
      onClick={() => !isPending && onClose()}
    >
      <div
        role="dialog"
        aria-modal="true"
        aria-labelledby="am-title"
        aria-describedby="am-description"
        className="flex w-full max-w-[400px] flex-col gap-[var(--space-3)] rounded-xl bg-surface-raised p-[var(--space-5)] shadow-modal"
        onClick={(event) => event.stopPropagation()}
      >
        <span
          aria-hidden="true"
          className="inline-flex size-8 items-center justify-center rounded-control bg-danger-surface text-danger"
        >
          <PersonIcon />
        </span>
        <div className="flex items-center gap-[var(--space-2)]">
          <h2 id="am-title" className="m-0 text-[length:var(--font-size-dialog-title)] font-semibold">
            Pick someone to start it
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

        <p id="am-description" className="m-0 text-text-muted [line-height:var(--line-height)]">
          {reasonMessage ?? `“${taskTitle}” needs an assignee before it can move to ${toStatusLabel}.`}
        </p>

        {errorCode !== null && (
          <p
            role="alert"
            className="m-0 rounded-md border border-danger-border bg-danger-surface p-[var(--space-3)] text-[length:var(--font-size-sm)] text-danger"
          >
            {describeErrorCode(errorCode, errorMessage)}
          </p>
        )}

        {usersQuery.isPending && (
          <p role="status" className="m-0 text-[length:var(--font-size-sm)] text-text-muted">
            Loading people…
          </p>
        )}

        {usersQuery.isError && (
          <p role="alert" className="m-0 text-[length:var(--font-size-sm)] text-danger">
            The list of people could not be loaded. Try closing and reopening this dialog.
          </p>
        )}

        {usersQuery.isSuccess && assignees.length === 0 && (
          <p className="m-0 text-[length:var(--font-size-sm)] text-text-muted">
            No one is in the directory yet, so there is no one to assign this to.
          </p>
        )}

        <ul role="listbox" aria-label="Assign to and start" className="m-0 flex list-none flex-col p-0">
          {assignees.map((assignee) => (
            <li key={assignee.id} role="option" aria-selected="false">
              <button
                type="button"
                className={clsx(optionClass, 'gap-[var(--space-2)]')}
                disabled={isPending}
                onClick={() => onSelect(assignee.id)}
              >
                <span
                  aria-hidden="true"
                  className="inline-flex size-[22px] shrink-0 items-center justify-center rounded-pill text-[length:var(--font-size-2xs)] font-semibold"
                  style={{
                    backgroundColor: `var(--avatar-${avatarPaletteIndex(assignee.id)}-bg)`,
                    color: `var(--avatar-${avatarPaletteIndex(assignee.id)}-ink)`,
                  }}
                >
                  {initialsOf(assignee.userName)}
                </span>
                Assign to {assignee.userName} and start
              </button>
            </li>
          ))}
        </ul>
      </div>
    </div>
  )
}
