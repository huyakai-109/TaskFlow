import clsx from 'clsx'
import { Link } from 'react-router-dom'

import { BOARD_COLUMNS } from '../../../api/types'
import type { PaginationRes, TaskStatus, TaskSummary } from '../../../api/types'
import { DueChip, OverdueChip } from '../../../shared/components/DueDateChips'
import { PriorityIcon } from '../../../shared/components/PriorityIcon'
import { avatarPaletteIndex, initialsOf } from '../../../shared/lib/avatar'
import { STATUS_LABELS } from '../../../shared/lib/statusLabels'

const cellClass = 'px-[var(--space-3)] py-[var(--space-2)] text-left align-middle'

interface BoardListViewProps {
  itemsByStatus: Record<TaskStatus, TaskSummary[]>
  /** Board page 1's pagination per column, same source `BoardColumn` reads — `null` entries while the board is still loading. */
  paginationByStatus: Record<TaskStatus, PaginationRes | null>
  onLoadMore: (status: TaskStatus) => void
  isLoadingMore: (status: TaskStatus) => boolean
}

/**
 * `docs/design/SPEC.md` §5.1/§10 point 5 (reopened 2026-11-11): the Board's
 * List segment. Re-renders the same `useBoardQuery` data the Board (card)
 * view already has as one flat table, grouped by status in board order —
 * no new request, same data, same per-column ordering the server already
 * sends (priority, then earliest due date; F03).
 *
 * Each status's rows are capped the same way the board's own column is
 * (`BoardColumnPageSize`/`DoneColumnPageSize`, `docs/design/COMING-SOON.md`
 * A3) — this view gets its own "Show more" per status, same control
 * `BoardColumn` already has, so List is never silently missing rows Board
 * itself would show via its own "Show more".
 */
export function BoardListView({ itemsByStatus, paginationByStatus, onLoadMore, isLoadingMore }: BoardListViewProps) {
  const allEmpty = BOARD_COLUMNS.every((status) => itemsByStatus[status].length === 0)

  if (allEmpty) {
    return <p className="mt-[var(--space-5)] text-[length:var(--font-size-sm)] text-text-muted">No tasks yet.</p>
  }

  return (
    // No forced `min-width` and `table-fixed` (layout fix, 2026-11-12
    // again) — a hard 720px floor forced a horizontal scrollbar on this
    // view at every width below that, which the board area itself no
    // longer needs at any width. Declared `%` column widths plus wrapping
    // cell content (not truncation) keep every width horizontal-scroll-free.
    <div className="bg-surface">
      <table className="w-full table-fixed border-collapse text-[length:var(--font-size-sm)]">
        <thead className="sticky top-0 z-[1] bg-surface">
          <tr className="text-left text-text-muted">
            <th scope="col" className={clsx(cellClass, 'w-[34%]')}>
              Task
            </th>
            <th scope="col" className={clsx(cellClass, 'w-[16%]')}>
              Status
            </th>
            <th scope="col" className={clsx(cellClass, 'w-[16%]')}>
              Priority
            </th>
            <th scope="col" className={clsx(cellClass, 'w-[16%]')}>
              Due
            </th>
            <th scope="col" className={clsx(cellClass, 'w-[18%]')}>
              Assignee
            </th>
          </tr>
        </thead>
        <tbody>
          {BOARD_COLUMNS.flatMap((status) => {
            const items = itemsByStatus[status]
            const pagination = paginationByStatus[status]
            const totalItems = pagination?.totalItems ?? items.length
            const hasMore = items.length < totalItems

            const rows = items.map((task) => (
              <tr key={task.id} className="border-t border-border">
                <td className={cellClass}>
                  <Link
                    to={`/tasks/${task.id}`}
                    className="flex min-w-0 items-baseline gap-[var(--space-2)] text-text no-underline hover:underline"
                  >
                    <span className="shrink-0 font-mono text-[length:var(--font-size-xs)] text-text-muted">#{task.id}</span>
                    <span className="min-w-0 break-words font-medium">{task.title}</span>
                  </Link>
                </td>
                <td className={cellClass}>{STATUS_LABELS[status]}</td>
                <td className={cellClass}>
                  <span className="inline-flex items-center gap-[var(--space-1)]">
                    <PriorityIcon priority={task.priority} />
                    {task.priority}
                  </span>
                </td>
                <td className={cellClass}>{task.isOverdue ? <OverdueChip dueDate={task.dueDate} /> : <DueChip dueDate={task.dueDate} />}</td>
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
            ))

            if (hasMore) {
              rows.push(
                <tr key={`${status}-more`} className="border-t border-border">
                  <td className={cellClass} colSpan={5}>
                    <button
                      type="button"
                      className="min-h-8 cursor-pointer rounded-md border border-border bg-surface px-[var(--space-3)] text-[length:var(--font-size-xs)] font-medium text-text hover:border-accent hover:bg-accent-soft hover:text-accent-ink disabled:cursor-not-allowed disabled:opacity-60"
                      onClick={() => onLoadMore(status)}
                      disabled={isLoadingMore(status)}
                    >
                      {isLoadingMore(status) ? 'Loading…' : `Show more ${STATUS_LABELS[status]} (${items.length} of ${totalItems})`}
                    </button>
                  </td>
                </tr>,
              )
            }

            return rows
          })}
        </tbody>
      </table>
    </div>
  )
}
