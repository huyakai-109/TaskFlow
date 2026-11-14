import { useRef } from 'react'

import { describeErrorCode } from '../../api/errorMessages'
import { useAuthSession } from '../hooks/useAuthSession'
import { useDialogA11y } from '../hooks/useDialogA11y'
import { IN_PROGRESS_LIMIT, useTeamCapacity } from '../hooks/useTeamCapacity'

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
  isPending: boolean
  errorCode: string | null
  /** The server's raw `error` string — shown verbatim for `CapacityReached`. */
  errorMessage: string | null
  onClose: () => void
  onConfirm: () => void
}

/**
 * `Task.Move.AssigneeRequired`'s way out: "Take it and start"
 * (`docs/design/SPEC.md` §10 point 17, Revision R2 of F01 D11/D12) — this
 * **always** assigns the task to the signed-in caller, then moves it; no
 * one, not even a Manager or Admin, can start a task on someone else's
 * behalf (no bypass, A1). This replaces the earlier roster-picking version
 * of this dialog (every user, "HAS ROOM"/"AT CAPACITY" grouping): holder-
 * only progression means there is only ever one legal choice here — the
 * caller themselves — so there is nothing left to pick from.
 *
 * `CapacityReached` is a rejection, not a silent failure (per the brief):
 * the error box below shows the server's own message verbatim
 * (`describeErrorCode`'s `CapacityReached` handling), same as every other
 * refusal path in the app.
 */
export function AssignAndMoveDialog({
  taskTitle,
  toStatusLabel,
  isPending,
  errorCode,
  errorMessage,
  onClose,
  onConfirm,
}: AssignAndMoveDialogProps) {
  const { userId } = useAuthSession()
  const capacity = useTeamCapacity()
  const ownEntry = capacity.entries.find((entry) => entry.user.id === userId)
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
            Take it and start
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
          “{taskTitle}” needs an assignee before it can move to {toStatusLabel}. Assign it to yourself and start it?
        </p>

        {ownEntry !== undefined && (
          <p className="m-0 text-[length:var(--font-size-sm)] text-text-muted">
            You have {ownEntry.inProgressCount}/{IN_PROGRESS_LIMIT} in progress.
          </p>
        )}

        {errorCode !== null && (
          <p
            role="alert"
            className="m-0 rounded-md border border-danger-border bg-danger-surface p-[var(--space-3)] text-[length:var(--font-size-sm)] text-danger"
          >
            {describeErrorCode(errorCode, errorMessage)}
          </p>
        )}

        <div className="flex items-center justify-end gap-[var(--space-3)]">
          <button
            type="button"
            className="min-h-11 cursor-pointer rounded-md border border-border bg-surface px-[var(--space-4)] font-medium text-text disabled:cursor-not-allowed disabled:opacity-60"
            onClick={onClose}
            disabled={isPending}
          >
            Cancel
          </button>
          <button
            type="button"
            className="min-h-11 cursor-pointer rounded-md border-0 bg-accent px-[var(--space-4)] font-semibold text-on-accent disabled:cursor-not-allowed disabled:opacity-60"
            disabled={isPending}
            onClick={onConfirm}
          >
            {isPending ? 'Starting…' : 'Take it and start'}
          </button>
        </div>
      </div>
    </div>
  )
}
