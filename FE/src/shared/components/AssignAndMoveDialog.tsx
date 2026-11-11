import clsx from 'clsx'
import { useRef } from 'react'

import { describeErrorCode } from '../../api/errorMessages'
import { useDialogA11y } from '../hooks/useDialogA11y'
import { IN_PROGRESS_LIMIT, useTeamCapacity } from '../hooks/useTeamCapacity'
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
 * what this task is missing (F02 D5).
 *
 * "HAS ROOM · MOST FIRST" / "AT CAPACITY" grouping (`docs/design/SPEC.md`
 * §5.5 and `docs/design/png/AssignPicker.png`, reopened 2026-11-11, SPEC §10
 * point 2; `docs/design/COMING-SOON.md` A8) — unlike `AssigneePickerDialog`,
 * this dialog is always about to send the task into `InProgress`, so
 * disabling someone already full is never wrong here the way it could be for
 * a general reassignment of a Backlog/Todo task (F01 D1 — assigning those has
 * no limit). Counts: `useTeamCapacity`, same source and completeness caveat
 * as Team capacity (A2).
 */
export function AssignAndMoveDialog({
  taskTitle,
  toStatusLabel,
  isPending,
  errorCode,
  errorMessage,
  onClose,
  onSelect,
}: AssignAndMoveDialogProps) {
  const capacity = useTeamCapacity()
  const closeButtonRef = useRef<HTMLButtonElement>(null)

  // "HAS ROOM · MOST FIRST" sorts by most room (fewest in-progress) first —
  // the opposite of `useTeamCapacity`'s own full-first order (built for the
  // Team capacity page/popover) — so each group is re-sorted locally.
  const hasRoom = capacity.entries.filter((e) => !e.isFull).sort((a, b) => a.inProgressCount - b.inProgressCount || a.user.userName.localeCompare(b.user.userName))
  const atCapacity = capacity.entries.filter((e) => e.isFull).sort((a, b) => a.user.userName.localeCompare(b.user.userName))

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
          “{taskTitle}” needs an assignee before it can move to {toStatusLabel}.
        </p>

        {errorCode !== null && (
          <p
            role="alert"
            className="m-0 rounded-md border border-danger-border bg-danger-surface p-[var(--space-3)] text-[length:var(--font-size-sm)] text-danger"
          >
            {describeErrorCode(errorCode, errorMessage)}
          </p>
        )}

        {capacity.isPending && (
          <p role="status" className="m-0 text-[length:var(--font-size-sm)] text-text-muted">
            Loading people…
          </p>
        )}

        {capacity.isError && (
          <p role="alert" className="m-0 text-[length:var(--font-size-sm)] text-danger">
            The list of people could not be loaded. Try closing and reopening this dialog.
          </p>
        )}

        {!capacity.isPending && !capacity.isError && capacity.entries.length === 0 && (
          <p className="m-0 text-[length:var(--font-size-sm)] text-text-muted">
            No one is in the directory yet, so there is no one to assign this to.
          </p>
        )}

        {!capacity.isPending && !capacity.isError && capacity.entries.length > 0 && (
          <>
            {hasRoom.length > 0 && (
              <p className="m-0 text-[length:var(--font-size-2xs)] font-semibold tracking-[0.44px] text-text-muted uppercase">
                Has room · most first
              </p>
            )}
            <ul role="listbox" aria-label="Assign to and start" className="m-0 flex list-none flex-col p-0">
              {hasRoom.map(({ user, inProgressCount }) => (
                <li key={user.id} role="option" aria-selected="false">
                  <button
                    type="button"
                    className={clsx(optionClass, 'gap-[var(--space-2)]')}
                    disabled={isPending}
                    onClick={() => onSelect(user.id)}
                  >
                    <span
                      aria-hidden="true"
                      className="inline-flex size-[22px] shrink-0 items-center justify-center rounded-pill text-[length:var(--font-size-2xs)] font-semibold"
                      style={{
                        backgroundColor: `var(--avatar-${avatarPaletteIndex(user.id)}-bg)`,
                        color: `var(--avatar-${avatarPaletteIndex(user.id)}-ink)`,
                      }}
                    >
                      {initialsOf(user.userName)}
                    </span>
                    Assign to {user.userName} and start
                    <span className="ml-auto text-[length:var(--font-size-xs)] text-text-muted">
                      {inProgressCount}/{IN_PROGRESS_LIMIT}
                    </span>
                  </button>
                </li>
              ))}
            </ul>

            {atCapacity.length > 0 && (
              <>
                <p className="m-0 text-[length:var(--font-size-2xs)] font-semibold tracking-[0.44px] text-text-muted uppercase">
                  At capacity
                </p>
                <ul role="listbox" aria-label="At capacity" className="m-0 flex list-none flex-col p-0">
                  {atCapacity.map(({ user }) => (
                    <li key={user.id} role="option" aria-selected="false" aria-disabled="true">
                      <button type="button" className={clsx(optionClass, 'gap-[var(--space-2)] opacity-60')} disabled>
                        <span
                          aria-hidden="true"
                          className="inline-flex size-[22px] shrink-0 items-center justify-center rounded-pill text-[length:var(--font-size-2xs)] font-semibold opacity-60"
                          style={{
                            backgroundColor: `var(--avatar-${avatarPaletteIndex(user.id)}-bg)`,
                            color: `var(--avatar-${avatarPaletteIndex(user.id)}-ink)`,
                          }}
                        >
                          {initialsOf(user.userName)}
                        </span>
                        {user.userName} · can&rsquo;t take more active work
                        <span className="ml-auto text-[length:var(--font-size-xs)] font-semibold text-danger">Full</span>
                      </button>
                    </li>
                  ))}
                </ul>
              </>
            )}
          </>
        )}
      </div>
    </div>
  )
}
