import clsx from 'clsx'
import { useRef } from 'react'

import type { TaskStatus } from '../../../api/types'
import { describeErrorCode } from '../../../api/errorMessages'
import { useAuthSession } from '../../../shared/hooks/useAuthSession'
import { useDialogA11y } from '../../../shared/hooks/useDialogA11y'
import { IN_PROGRESS_LIMIT, useTeamCapacity } from '../../../shared/hooks/useTeamCapacity'
import { useUsersQuery } from '../../../shared/hooks/useUsersQuery'
import { avatarPaletteIndex, initialsOf } from '../../../shared/lib/avatar'

interface AssigneePickerDialogProps {
  currentAssigneeId: number | null
  /** F02 D2: only `InProgress` occupies a capacity slot — the "HAS ROOM"/"AT CAPACITY" grouping below only applies then (F01 D1: assigning a Backlog/Todo/Review/Done task has no limit). */
  taskStatus: TaskStatus
  /**
   * `task.canAssignOthers` (Revision R2 wire changes) — this dialog is only
   * ever opened for a task that isn't locked (`PropertiesPanel` never opens
   * it for Review/Done), so here it reduces to "the caller holds
   * `AssignAnyone`"; read directly off the task rather than re-deriving it
   * from the permission alone, same D10 "never re-derive server-computed
   * flags" principle `taskActions.ts` already follows for `canTakeOver`.
   */
  canAssignOthers: boolean
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
 * `docs/design/SPEC.md` §5.5 and `docs/design/png/AssignPicker.png` — its
 * "HAS ROOM · MOST FIRST" / "AT CAPACITY" grouping (reopened 2026-11-11,
 * SPEC §10 point 2; `docs/design/COMING-SOON.md` A8) applies only while
 * `taskStatus === 'InProgress'` (F02 D2 `TaskCapacityRule.OccupiesSlot`) —
 * reassigning a Backlog/Todo/Review/Done task has no capacity limit (F01
 * D1), so grouping/disabling people there would be actively misleading.
 * Everywhere else, the list stays the plain alphabetical one it always was.
 * Stays open on a non-conflict failure (e.g. `AssigneeNotFound`,
 * `CapacityReached`) so the user can pick again; a conflict closes it,
 * because the page's own refusal handler already refreshes the task.
 */
export function AssigneePickerDialog({
  currentAssigneeId,
  taskStatus,
  canAssignOthers,
  isPending,
  errorCode,
  errorMessage = null,
  onClose,
  onSelect,
}: AssigneePickerDialogProps) {
  const appliesCapacityGrouping = taskStatus === 'InProgress'
  const usersQuery = useUsersQuery()
  const capacity = useTeamCapacity()
  const { userId } = useAuthSession()
  // Revision R2 (D12): without `AssignAnyone`, a caller may only assign to
  // themselves (or clear their own assignment) — the roster below is
  // restricted to that one person instead of offering a choice
  // `Task.Assign.SelfOnly` would refuse.
  const assignees = (usersQuery.data ?? []).filter((user) => canAssignOthers || user.id === userId)
  const closeButtonRef = useRef<HTMLButtonElement>(null)

  const hasRoom = appliesCapacityGrouping
    ? capacity.entries
        .filter((e) => !e.isFull && (canAssignOthers || e.user.id === userId))
        .sort((a, b) => a.inProgressCount - b.inProgressCount || a.user.userName.localeCompare(b.user.userName))
    : []
  const atCapacity = appliesCapacityGrouping
    ? capacity.entries
        .filter((e) => e.isFull && (canAssignOthers || e.user.id === userId))
        .sort((a, b) => a.user.userName.localeCompare(b.user.userName))
    : []

  useDialogA11y(closeButtonRef, onClose, isPending)

  return (
    <div
      className="fixed inset-0 z-[100] flex items-center justify-center bg-overlay p-[var(--space-4)]"
      onClick={() => !isPending && onClose()}
    >
      <div
        role="dialog"
        aria-modal="true"
        aria-labelledby="ap-title"
        className="flex w-full max-w-[360px] flex-col gap-[var(--space-3)] rounded-xl bg-surface-raised p-[var(--space-5)] shadow-modal"
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
          {!canAssignOthers
            ? 'You can only assign this task to yourself, or clear it.'
            : appliesCapacityGrouping
              ? 'People already at the in-progress limit are shown last and can’t be picked.'
              : 'Listed alphabetically, everyone shown — the workload limit isn’t shown here.'}
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

        {/* Clearing someone else's assignment also needs `AssignAnyone` (D12) — the Unassigned option is only offered when there is nothing-but-the-caller's-own to clear. */}
        {(canAssignOthers || currentAssigneeId === null || currentAssigneeId === userId) && (
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
          </ul>
        )}

        {appliesCapacityGrouping && hasRoom.length > 0 && (
          <p className="m-0 text-[length:var(--font-size-2xs)] font-semibold tracking-[0.44px] text-text-muted uppercase">
            Has room · most first
          </p>
        )}

        <ul role="listbox" aria-label={appliesCapacityGrouping ? 'People with room' : 'Assign to'} className="m-0 flex list-none flex-col p-0">
          {!appliesCapacityGrouping &&
            assignees.map((assignee) => (
              <li key={assignee.id} role="option" aria-selected={currentAssigneeId === assignee.id}>
                <button
                  type="button"
                  className={clsx(
                    optionClass,
                    'gap-[var(--space-2)]',
                    currentAssigneeId === assignee.id && 'bg-accent-soft font-semibold text-accent-ink',
                  )}
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
                  {assignee.userName}
                </button>
              </li>
            ))}
          {appliesCapacityGrouping &&
            hasRoom.map(({ user, inProgressCount }) => (
              <li key={user.id} role="option" aria-selected={currentAssigneeId === user.id}>
                <button
                  type="button"
                  className={clsx(
                    optionClass,
                    'gap-[var(--space-2)]',
                    currentAssigneeId === user.id && 'bg-accent-soft font-semibold text-accent-ink',
                  )}
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
                  {user.userName}
                  <span className="ml-auto text-[length:var(--font-size-xs)] text-text-muted">
                    {inProgressCount}/{IN_PROGRESS_LIMIT}
                  </span>
                </button>
              </li>
            ))}
        </ul>

        {appliesCapacityGrouping && atCapacity.length > 0 && (
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

        {(appliesCapacityGrouping ? capacity.isPending : usersQuery.isPending) && (
          <p role="status" className="m-0 text-[length:var(--font-size-sm)] text-text-muted">
            Loading people…
          </p>
        )}

        {!appliesCapacityGrouping && usersQuery.isSuccess && assignees.length === 0 && (
          <p className="m-0 text-[length:var(--font-size-sm)] text-text-muted">
            No one is in the directory yet, so there is no one to pick besides Unassigned.
          </p>
        )}
      </div>
    </div>
  )
}
