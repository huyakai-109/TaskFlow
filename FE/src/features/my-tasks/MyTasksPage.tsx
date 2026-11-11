import { useState } from 'react'
import { Link } from 'react-router-dom'

import { describeError, describeErrorCode } from '../../api/errorMessages'
import type { TaskStatus, TaskSummary } from '../../api/types'
import { DueChip, OverdueChip } from '../../shared/components/DueDateChips'
import { MoveToast } from '../../shared/components/MoveToast'
import { PriorityIcon } from '../../shared/components/PriorityIcon'
import { useAuthSession } from '../../shared/hooks/useAuthSession'
import { useBoardQuery } from '../../shared/hooks/useBoardQuery'
import { useMoveTaskMutation } from '../../shared/hooks/useMoveTaskMutation'
import { useUsersQuery } from '../../shared/hooks/useUsersQuery'
import { avatarPaletteIndex, initialsOf } from '../../shared/lib/avatar'
import { moveButtonLabel } from '../../shared/lib/moveButtonLabel'

import { useSentBackBy } from './hooks/useSentBackBy'

/** "Good morning"/"afternoon"/"evening" by the viewer's local clock — decorative copy only, not a server fact. */
function greetingWord(): string {
  const hour = new Date().getHours()
  if (hour < 12) return 'morning'
  if (hour < 18) return 'afternoon'
  return 'evening'
}

interface TaskRowProps {
  task: TaskSummary
  primary?: boolean
  /** Set only for a Todo→InProgress "Start" row while the viewer is already at 3 in progress — disables the button and shows why (SPEC §5.8's "Frees up when one of your 3 moves to Review."), a client-side hint only; the server stays the real enforcer. */
  disabledReason?: string
  /** "In progress" section only (`docs/design/COMING-SOON.md` A11) — a small, bounded per-row history read, not a board-wide one. */
  checkSentBack?: boolean
  onMove: (taskId: number, toStatus: TaskStatus, version: string, taskTitle: string) => void
}

function TaskRow({ task, primary = false, disabledReason, checkSentBack = false, onMove }: TaskRowProps) {
  const move = task.allowedTransitions.find((t) => t.operation === 'Move')
  const disabled = disabledReason !== undefined
  const sentBackQuery = useSentBackBy(task.id, checkSentBack)
  const sentBackBy = sentBackQuery.data

  return (
    <div className="flex flex-wrap items-center gap-[var(--space-3)] rounded-card-lg border border-border bg-surface p-[var(--space-3)]">
      <PriorityIcon priority={task.priority} />
      {task.assignee !== null && (
        <span
          aria-label={`Assigned to ${task.assignee.userName}`}
          className="inline-flex size-5 shrink-0 items-center justify-center rounded-pill text-[length:var(--font-size-2xs)] font-semibold"
          style={{
            backgroundColor: `var(--avatar-${avatarPaletteIndex(task.assignee.id)}-bg)`,
            color: `var(--avatar-${avatarPaletteIndex(task.assignee.id)}-ink)`,
          }}
        >
          {initialsOf(task.assignee.userName)}
        </span>
      )}
      <div className="flex min-w-0 flex-1 flex-col">
        <Link to={`/tasks/${task.id}`} className="truncate font-medium text-text no-underline hover:underline">
          {task.title} <span className="font-mono text-[length:var(--font-size-2xs)] text-text-muted">#{task.id}</span>
        </Link>
        <span className="mt-[2px] flex flex-wrap gap-[var(--space-2)]">
          {task.isOverdue ? <OverdueChip dueDate={task.dueDate} /> : <DueChip dueDate={task.dueDate} />}
          {sentBackBy !== null && sentBackBy !== undefined && (
            <span className="inline-flex min-h-[21px] items-center gap-[var(--space-1)] rounded-sm border border-transparent bg-warning-surface px-[var(--space-2)] font-semibold text-warning">
              Sent back by {sentBackBy.actorName ?? 'someone'} for rework
            </span>
          )}
        </span>
      </div>
      {move !== undefined && (
        <div className="flex flex-col items-end gap-[2px]">
          <button
            type="button"
            disabled={disabled}
            title={disabledReason}
            className={
              primary
                ? 'min-h-10 cursor-pointer rounded-md border-0 bg-accent px-[var(--space-4)] font-semibold text-on-accent hover:bg-accent-hover disabled:cursor-not-allowed disabled:opacity-60'
                : 'min-h-10 cursor-pointer rounded-md border border-border bg-surface px-[var(--space-4)] font-medium text-text hover:bg-column disabled:cursor-not-allowed disabled:opacity-60'
            }
            onClick={() => onMove(task.id, move.status, task.version, task.title)}
          >
            {moveButtonLabel(task.status, move)}
          </button>
          {disabledReason !== undefined && (
            <span className="max-w-[200px] text-right text-[length:var(--font-size-2xs)] text-text-muted">{disabledReason}</span>
          )}
        </div>
      )}
    </div>
  )
}

/**
 * `docs/design/SPEC.md` §5.8. "Sent back by `<name>` for rework" (reopened
 * 2026-11-11, `docs/design/COMING-SOON.md` A11) reads each visible
 * "In progress" row's own history — a small, bounded set of requests (≤3
 * cards), not a board-wide read. "You finished N tasks this week" stays out
 * (§10 point 6, COMING-SOON.md B2 — needs a dated aggregate this branch
 * doesn't add). Reads the already-loaded board — no new endpoint for the
 * task lists themselves (§10 point 9); a per-user item beyond a column's
 * first `BoardColumnPageSize` (50) page would be missing, a documented
 * limitation, not something this page tries to paper over with its own
 * pagination.
 */
export function MyTasksPage() {
  const { userId } = useAuthSession()
  const boardQuery = useBoardQuery()
  const usersQuery = useUsersQuery()
  const [toastMessage, setToastMessage] = useState<string | null>(null)

  const moveMutation = useMoveTaskMutation({
    onRefused: (code, _variables, message) => setToastMessage(describeErrorCode(code, message)),
  })

  const me = usersQuery.data?.find((user) => user.id === userId) ?? null

  if (boardQuery.isPending) {
    return (
      <p className="text-[length:var(--font-size-sm)] text-text-muted" role="status">
        Loading your tasks…
      </p>
    )
  }

  if (boardQuery.isError) {
    return (
      <div
        className="flex flex-col items-start gap-[var(--space-3)] rounded-md border border-danger-border bg-danger-surface p-[var(--space-4)] text-danger"
        role="alert"
      >
        <p>{describeError(boardQuery.error)}</p>
        <button type="button" onClick={() => boardQuery.refetch()}>
          Try again
        </button>
      </div>
    )
  }

  const column = (status: TaskStatus) => boardQuery.data.columns.find((c) => c.status === status)
  const columnItems = (status: TaskStatus) => column(status)?.items ?? []
  const columnHasMore = (status: TaskStatus) => {
    const pagination = column(status)?.pagination
    return pagination !== null && pagination !== undefined && columnItems(status).length < pagination.totalItems
  }
  const mine = (items: TaskSummary[]) => items.filter((item) => item.assignee?.id === userId)

  const inProgress = mine(columnItems('InProgress'))
  const upNext = mine(columnItems('Todo'))
  const review = columnItems('Review')
  // The board's own column page (BoardColumnPageSize = 50) may not hold every
  // active task team-wide — this page never auto-fetches more of it (SPEC.md
  // §10 point 9), so say so rather than silently showing a possibly-partial
  // list as if it were complete.
  const todoMayBeIncomplete = columnHasMore('Todo')
  const reviewMayBeIncomplete = columnHasMore('Review')

  function handleMove(taskId: number, toStatus: TaskStatus, version: string, taskTitle: string) {
    moveMutation.mutate({ taskId, toStatus, version, taskTitle })
  }

  return (
    <section className="flex max-w-[1000px] flex-col gap-[var(--space-5)]">
      <h1 className="m-0 text-[length:var(--font-size-xl)] font-semibold">My tasks</h1>

      <div className="flex flex-wrap items-center justify-between gap-[var(--space-3)] rounded-xl border border-border bg-surface p-[var(--space-4)] shadow-card">
        <p className="m-0 text-[length:var(--font-size-greeting)] font-semibold">
          Good {greetingWord()}{me !== null ? `, ${me.userName}` : ''}
        </p>
        <div className="flex items-center gap-[var(--space-2)]">
          <div className="flex gap-[2px]" aria-hidden="true">
            {[0, 1, 2].map((i) => {
              const filled = i < Math.min(inProgress.length, 3)
              const full = inProgress.length >= 3
              return (
                <span
                  key={i}
                  className={`h-[6px] w-[14px] rounded-meter ${filled ? (full ? 'bg-danger' : 'bg-accent') : 'bg-meter-empty'}`}
                />
              )
            })}
          </div>
          <span className={inProgress.length >= 3 ? 'text-[length:var(--font-size-sm)] font-semibold text-overdue' : 'text-[length:var(--font-size-sm)] text-text-muted'}>
            {inProgress.length >= 3 ? 'Full' : `${inProgress.length} in progress`}
          </span>
        </div>
      </div>

      <div className="flex flex-col gap-[var(--space-3)]">
        <h2 className="m-0 text-[length:var(--font-size-sm)] font-semibold text-text-muted">
          In progress <span className="font-normal">{inProgress.length}</span>
        </h2>
        {inProgress.length === 0 ? (
          <p className="m-0 rounded-card-lg border border-dashed border-dashed-border p-[var(--space-4)] text-[length:var(--font-size-sm)] text-text-muted">
            Nothing in progress.
          </p>
        ) : (
          inProgress.map((task, index) => (
            <TaskRow key={task.id} task={task} primary={index === 0} checkSentBack onMove={handleMove} />
          ))
        )}
      </div>

      <div className="flex flex-col gap-[var(--space-3)]">
        <h2 className="m-0 text-[length:var(--font-size-sm)] font-semibold text-text-muted">
          Up next <span className="font-normal">{upNext.length}</span>
        </h2>
        {todoMayBeIncomplete && (
          <p className="m-0 text-[length:var(--font-size-xs)] text-text-muted">
            This list may not be complete —{' '}
            <Link to="/board" className="text-accent-ink underline">
              see the full Todo column on the Board
            </Link>
            .
          </p>
        )}
        {upNext.length === 0 ? (
          <p className="m-0 rounded-card-lg border border-dashed border-dashed-border p-[var(--space-4)] text-[length:var(--font-size-sm)] text-text-muted">
            Nothing queued up.
          </p>
        ) : (
          upNext.map((task) => (
            <TaskRow
              key={task.id}
              task={task}
              disabledReason={inProgress.length >= 3 ? 'Frees up when one of your 3 moves to Review.' : undefined}
              onMove={handleMove}
            />
          ))
        )}
      </div>

      <div className="flex flex-col gap-[var(--space-3)]">
        <h2 className="m-0 text-[length:var(--font-size-sm)] font-semibold text-text-muted">
          Review <span className="font-normal">{review.length}</span>
        </h2>
        {reviewMayBeIncomplete && (
          <p className="m-0 text-[length:var(--font-size-xs)] text-text-muted">
            This list may not be complete —{' '}
            <Link to="/board" className="text-accent-ink underline">
              see the full Review column on the Board
            </Link>
            .
          </p>
        )}
        {review.length === 0 ? (
          <p className="m-0 rounded-card-lg border border-dashed border-dashed-border p-[var(--space-4)] text-[length:var(--font-size-sm)] text-text-muted">
            Nothing waiting on you — no one has asked you to review, and none of your work is waiting on a reviewer.
          </p>
        ) : (
          review.map((task) => <TaskRow key={task.id} task={task} onMove={handleMove} />)
        )}
      </div>

      {toastMessage !== null && <MoveToast message={toastMessage} onDismiss={() => setToastMessage(null)} />}
    </section>
  )
}
