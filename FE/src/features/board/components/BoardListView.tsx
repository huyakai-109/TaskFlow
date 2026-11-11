import { Link } from 'react-router-dom'

import { BOARD_COLUMNS } from '../../../api/types'
import type { TaskStatus, TaskSummary } from '../../../api/types'
import { DueChip, OverdueChip } from '../../../shared/components/DueDateChips'
import { PriorityIcon } from '../../../shared/components/PriorityIcon'
import { avatarPaletteIndex, initialsOf } from '../../../shared/lib/avatar'
import { STATUS_LABELS } from '../../../shared/lib/statusLabels'

const cellClass = 'px-[var(--space-3)] py-[var(--space-2)] text-left align-middle'

interface BoardListViewProps {
  itemsByStatus: Record<TaskStatus, TaskSummary[]>
}

/**
 * `docs/design/SPEC.md` §5.1/§10 point 5 (reopened 2026-11-11): the Board's
 * List segment. Re-renders the same `useBoardQuery` data the Board (card)
 * view already has as one flat table, grouped by status in board order —
 * no new request, same data, same per-column ordering the server already
 * sends (priority, then earliest due date; F03).
 */
export function BoardListView({ itemsByStatus }: BoardListViewProps) {
  const allEmpty = BOARD_COLUMNS.every((status) => itemsByStatus[status].length === 0)

  if (allEmpty) {
    return <p className="mt-[var(--space-5)] text-[length:var(--font-size-sm)] text-text-muted">No tasks yet.</p>
  }

  return (
    <div className="mt-[var(--space-5)] overflow-x-auto rounded-lg border border-border bg-surface">
      <table className="w-full min-w-[720px] border-collapse text-[length:var(--font-size-sm)]">
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
              Assignee
            </th>
          </tr>
        </thead>
        <tbody>
          {BOARD_COLUMNS.flatMap((status) =>
            itemsByStatus[status].map((task) => (
              <tr key={task.id} className="border-t border-border">
                <td className={cellClass}>
                  <Link
                    to={`/tasks/${task.id}`}
                    className="inline-flex items-baseline gap-[var(--space-2)] text-text no-underline hover:underline"
                  >
                    <span className="font-mono text-[length:var(--font-size-xs)] text-text-muted">#{task.id}</span>
                    <span className="font-medium">{task.title}</span>
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
            )),
          )}
        </tbody>
      </table>
    </div>
  )
}
