import { useState } from 'react'
import { Link } from 'react-router-dom'

import { describeError, describeErrorCode } from '../../api/errorMessages'
import type { TaskStatus, TaskSummary } from '../../api/types'
import { MoveToast } from '../../shared/components/MoveToast'
import { PriorityIcon } from '../../shared/components/PriorityIcon'
import { useAuthSession } from '../../shared/hooks/useAuthSession'
import { useBoardQuery } from '../../shared/hooks/useBoardQuery'
import { useMoveTaskMutation } from '../../shared/hooks/useMoveTaskMutation'
import { useUsersQuery } from '../../shared/hooks/useUsersQuery'
import { avatarPaletteIndex, initialsOf } from '../../shared/lib/avatar'
import { dueDateFormatter, isToday, parseDueDate } from '../../shared/lib/dueDate'
import { moveButtonLabel } from '../../shared/lib/moveButtonLabel'

/** "Good morning"/"afternoon"/"evening" by the viewer's local clock — decorative copy only, not a server fact. */
function greetingWord(): string {
  const hour = new Date().getHours()
  if (hour < 12) return 'morning'
  if (hour < 18) return 'afternoon'
  return 'evening'
}

function dueLabel(task: TaskSummary): string {
  if (task.dueDate === null) {
    return 'No due date'
  }
  if (task.isOverdue) {
    return `Overdue · ${dueDateFormatter.format(parseDueDate(task.dueDate))}`
  }
  if (isToday(task.dueDate)) {
    return 'Due today'
  }
  return `Due ${dueDateFormatter.format(parseDueDate(task.dueDate))}`
}

interface TaskRowProps {
  task: TaskSummary
  primary?: boolean
  onMove: (taskId: number, toStatus: TaskStatus, version: string) => void
}

function TaskRow({ task, primary = false, onMove }: TaskRowProps) {
  const move = task.allowedTransitions.find((t) => t.operation === 'Move')

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
        <span className={task.isOverdue ? 'text-[length:var(--font-size-xs)] font-semibold text-overdue' : 'text-[length:var(--font-size-xs)] text-text-muted'}>
          {dueLabel(task)}
        </span>
      </div>
      {move !== undefined && (
        <button
          type="button"
          className={
            primary
              ? 'min-h-10 cursor-pointer rounded-md border-0 bg-accent px-[var(--space-4)] font-semibold text-on-accent hover:bg-accent-hover'
              : 'min-h-10 cursor-pointer rounded-md border border-border bg-surface px-[var(--space-4)] font-medium text-text hover:bg-column'
          }
          onClick={() => onMove(task.id, move.status, task.version)}
        >
          {moveButtonLabel(task.status, move)}
        </button>
      )}
    </div>
  )
}

/**
 * `docs/design/SPEC.md` §5.8, trimmed per §10 point 6: no "Sent back by
 * <name>" chip, no "You finished N tasks this week" line (both need data
 * this branch doesn't add). Reads the already-loaded board — no new
 * endpoint (§10 point 9); a per-user item beyond a column's first
 * `BoardColumnPageSize` (50) page would be missing, a documented limitation,
 * not something this page tries to paper over with its own pagination.
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

  function handleMove(taskId: number, toStatus: TaskStatus, version: string) {
    moveMutation.mutate({ taskId, toStatus, version, taskTitle: `#${taskId}` })
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
          inProgress.map((task, index) => <TaskRow key={task.id} task={task} primary={index === 0} onMove={handleMove} />)
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
          upNext.map((task) => <TaskRow key={task.id} task={task} onMove={handleMove} />)
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
