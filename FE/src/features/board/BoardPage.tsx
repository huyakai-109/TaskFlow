import { BOARD_COLUMNS } from '../../api/types'
import type { TaskStatus } from '../../api/types'
import { describeError } from '../../api/errorMessages'

import styles from './BoardPage.module.css'
import { STATUS_LABELS } from './statusLabels'
import { TaskCard } from './TaskCard'
import { useBoardQuery } from './useBoardQuery'

function StatusIcon({ status }: { status: TaskStatus }) {
  switch (status) {
    case 'Backlog':
      return (
        <svg aria-hidden="true" width="14" height="14" viewBox="0 0 16 16" fill="none" stroke="currentColor" strokeWidth="1.6" strokeDasharray="2.2 2.2">
          <circle cx="8" cy="8" r="6" />
        </svg>
      )
    case 'Todo':
      return (
        <svg aria-hidden="true" width="14" height="14" viewBox="0 0 16 16" fill="none" stroke="currentColor" strokeWidth="1.6">
          <circle cx="8" cy="8" r="6" />
        </svg>
      )
    case 'InProgress':
      return (
        <svg aria-hidden="true" width="14" height="14" viewBox="0 0 16 16">
          <circle cx="8" cy="8" r="6" fill="none" stroke="var(--color-accent)" strokeWidth="1.6" />
          <path d="M8 4a4 4 0 0 1 0 8z" fill="var(--color-accent)" />
        </svg>
      )
    case 'Review':
      return (
        <svg aria-hidden="true" width="14" height="14" viewBox="0 0 16 16">
          <circle cx="8" cy="8" r="6" fill="none" stroke="var(--color-priority-high)" strokeWidth="1.6" />
          <path d="M8 2a6 6 0 1 1-6 6h6z" fill="var(--color-priority-high)" />
        </svg>
      )
    case 'Done':
      return (
        <svg aria-hidden="true" width="14" height="14" viewBox="0 0 16 16">
          <circle cx="8" cy="8" r="7" fill="var(--color-success)" />
          <path d="m5 8.2 2 2 4-4.2" fill="none" stroke="var(--color-surface)" strokeWidth="1.8" strokeLinecap="round" strokeLinejoin="round" />
        </svg>
      )
  }
}

/**
 * The board: five fixed columns, cards read-only. Drag-and-drop interaction is
 * slice 1.6 — each card instead lists the statuses it could legally move to
 * (`TaskCard`'s "Can move to …" line), derived from `allowedTransitions` and
 * never recomputed client-side (F01 D10, `docs/ui.md`).
 *
 * The sidebar, search, team-capacity popover and "N at capacity" chip from the
 * wireframe are left out here: they need F02 (capacity), a user roster or
 * routes this slice does not build (`docs/ui.md`'s MVP scope note). The
 * "N overdue" chip stays, because `isOverdue` is already on every card F01
 * returns today.
 */
export function BoardPage() {
  const boardQuery = useBoardQuery()

  const overdueCount =
    boardQuery.data?.columns.reduce((total, column) => total + column.items.filter((item) => item.isOverdue).length, 0) ?? 0

  return (
    <section>
      <div className={styles.toolbar}>
        <h2 className={styles.heading}>Board</h2>
        <span
          className={styles.sortInfo}
          title="Urgent → High → Normal, then earliest due date. Tasks with no date go last."
        >
          Sort: <strong>Priority</strong>
        </span>
        {overdueCount > 0 && <span className={styles.overdueChip}>{overdueCount} overdue</span>}
      </div>

      {boardQuery.isPending && (
        <p className={styles.note} role="status">
          Loading board…
        </p>
      )}

      {boardQuery.isError && (
        <div className={styles.errorBox} role="alert">
          <p>{describeError(boardQuery.error)}</p>
          <button type="button" onClick={() => boardQuery.refetch()}>
            Try again
          </button>
        </div>
      )}

      {boardQuery.isSuccess && (
        <div className={styles.columns}>
          {BOARD_COLUMNS.map((status) => {
            const column = boardQuery.data.columns.find((c) => c.status === status)
            const items = column?.items ?? []

            return (
              <section key={status} className={styles.column} aria-label={STATUS_LABELS[status]}>
                <h3 className={styles.columnTitle}>
                  <StatusIcon status={status} />
                  {STATUS_LABELS[status]} <span className={styles.columnCount}>{items.length}</span>
                </h3>
                {items.length === 0 ? (
                  <p className={styles.empty}>No tasks</p>
                ) : (
                  items.map((task) => <TaskCard key={task.id} task={task} />)
                )}
              </section>
            )
          })}
        </div>
      )}
    </section>
  )
}
