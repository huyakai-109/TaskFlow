import { Link } from 'react-router-dom'

import { describeError } from '../../api/errorMessages'
import { avatarPaletteIndex, initialsOf } from '../../shared/lib/avatar'
import { PriorityIcon } from '../../shared/components/PriorityIcon'
import { STATUS_LABELS } from '../../shared/lib/statusLabels'
import { daysLate, dueDateFormatter, parseDueDate } from '../../shared/lib/dueDate'
import { useOverdueTasksQuery } from '../../shared/hooks/useOverdueTasksQuery'

function formatDueDate(dueDate: string): string {
  return dueDateFormatter.format(parseDueDate(dueDate))
}

function ClockIcon() {
  return (
    <svg aria-hidden="true" width="16" height="16" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="1.8" strokeLinecap="round">
      <circle cx="12" cy="12" r="9" />
      <path d="M12 7v5l3 2" />
    </svg>
  )
}

const cellClass = 'px-[var(--space-3)] py-[var(--space-3)] text-left align-middle'

/**
 * The Overdue view (F05 T9) — "Overdue now" only, per the signed-off scope:
 * no "due today", "flag cleared this week", "last check" or "late by N days"
 * (those need data this contract does not provide). Server order (priority,
 * then earliest due date) is rendered as-is, never re-sorted client-side.
 */
export function OverdueListPage() {
  const query = useOverdueTasksQuery()
  const pages = query.data?.pages ?? []
  const items = pages.flatMap((page) => page.result ?? [])
  const pagination = pages.at(-1)?.pagination ?? null
  const totalItems = pagination?.totalItems ?? items.length
  const hasMore = items.length < totalItems

  return (
    <section>
      {/* AppLayout no longer owns a page-level h1 (docs/design/SPEC.md shell redesign) — each routed page is its own landmark now, like BoardPage's and TaskDetailPage's. */}
      <h1 className="m-0 text-[length:var(--font-size-xl)] font-semibold">Overdue tasks</h1>
      <div className="mt-[var(--space-3)] flex items-start gap-[var(--space-2)] rounded-lg border border-border bg-surface p-[var(--space-3)] text-[length:var(--font-size-sm)] text-text-muted">
        <ClockIcon />
        <p className="m-0">
          Checked automatically every night at 00:05. A task due today becomes overdue tomorrow. Finishing it, or
          moving the due date into the future, clears the flag.
        </p>
      </div>

      {query.isPending && (
        <p className="mt-[var(--space-5)] text-[length:var(--font-size-sm)] text-text-muted" role="status">
          Loading overdue tasks…
        </p>
      )}

      {query.isError && (
        <div
          className="mt-[var(--space-5)] flex flex-col items-start gap-[var(--space-3)] rounded-md border border-danger-border bg-danger-surface p-[var(--space-4)] text-danger"
          role="alert"
        >
          <p>{describeError(query.error)}</p>
          <button type="button" onClick={() => query.refetch()}>
            Try again
          </button>
        </div>
      )}

      {query.isSuccess && items.length === 0 && (
        <p className="mt-[var(--space-5)] text-[length:var(--font-size-sm)] text-text-muted">Nothing is overdue.</p>
      )}

      {query.isSuccess && items.length > 0 && (
        <div className="mt-[var(--space-5)] flex flex-col gap-[var(--space-3)]">
          <h2 className="m-0 flex items-center gap-[var(--space-2)] text-[length:var(--font-size-sm)] font-semibold">
            <span className="size-2 shrink-0 rounded-pill bg-danger" aria-hidden="true" />
            Overdue now <span className="font-normal text-text-muted">{totalItems}</span>
          </h2>
          <div className="overflow-x-auto rounded-lg border border-border bg-surface">
            <table className="w-full min-w-[640px] border-collapse text-[length:var(--font-size-sm)]">
              <thead>
                <tr className="text-left text-text-muted">
                  <th scope="col" className={cellClass}>
                    Task
                  </th>
                  <th scope="col" className={cellClass}>
                    Status
                  </th>
                  <th scope="col" className={cellClass}>
                    Priority
                  </th>
                  <th scope="col" className={cellClass}>
                    Due
                  </th>
                  <th scope="col" className={cellClass}>
                    Late by
                  </th>
                  <th scope="col" className={cellClass}>
                    Assignee
                  </th>
                </tr>
              </thead>
              <tbody>
                {items.map((task) => (
                  <tr key={task.id} className="border-t border-border">
                    <td className={cellClass}>
                      <Link
                        to={`/tasks/${task.id}`}
                        className="inline-flex items-baseline gap-[var(--space-2)] text-text no-underline hover:underline"
                      >
                        <span className="font-mono text-[length:var(--font-size-xs)] text-text-muted">
                          #{task.id}
                        </span>
                        <span className="font-medium">{task.title}</span>
                      </Link>
                    </td>
                    <td className={cellClass}>{STATUS_LABELS[task.status]}</td>
                    <td className={cellClass}>
                      <span className="inline-flex items-center gap-[var(--space-1)]">
                        <PriorityIcon priority={task.priority} />
                        {task.priority}
                      </span>
                    </td>
                    <td className={cellClass}>{task.dueDate !== null ? formatDueDate(task.dueDate) : '—'}</td>
                    <td className={cellClass}>
                      {task.dueDate !== null ? (
                        <span className="inline-flex items-center rounded-pill bg-overdue-surface px-[var(--space-2)] py-[2px] text-[length:var(--font-size-xs)] font-semibold text-overdue">
                          {daysLate(task.dueDate)} day{daysLate(task.dueDate) === 1 ? '' : 's'}
                        </span>
                      ) : (
                        '—'
                      )}
                    </td>
                    <td className={cellClass}>
                      {task.assignee !== null ? (
                        <span className="inline-flex items-center gap-[var(--space-2)]">
                          <span
                            className="inline-flex size-5 shrink-0 items-center justify-center rounded-pill text-[9px] font-semibold"
                            aria-label={`Assigned to ${task.assignee.userName}`}
                            style={{
                              backgroundColor: `var(--avatar-${avatarPaletteIndex(task.assignee.id)}-bg)`,
                              color: `var(--avatar-${avatarPaletteIndex(task.assignee.id)}-ink)`,
                            }}
                          >
                            {initialsOf(task.assignee.userName)}
                          </span>
                          {task.assignee.userName}
                        </span>
                      ) : (
                        <span className="text-text-muted">Unassigned</span>
                      )}
                    </td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
          <p className="m-0 text-[length:var(--font-size-xs)] text-text-muted">
            Sorted like the board: priority first (Urgent → High → Normal), then earliest due date.
          </p>

          {hasMore && (
            <button
              type="button"
              className="min-h-10 cursor-pointer self-start rounded-md border border-border bg-surface px-[var(--space-3)] text-[length:var(--font-size-sm)] font-medium text-text hover:border-accent hover:bg-accent-soft hover:text-accent-ink disabled:cursor-not-allowed disabled:opacity-60"
              onClick={() => query.fetchNextPage()}
              disabled={query.isFetchingNextPage}
            >
              {query.isFetchingNextPage ? 'Loading…' : `Show more (${items.length} of ${totalItems})`}
            </button>
          )}
        </div>
      )}
    </section>
  )
}
