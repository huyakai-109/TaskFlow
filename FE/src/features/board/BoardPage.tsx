import { BOARD_COLUMNS } from '../../api/types'

import styles from './BoardPage.module.css'

/**
 * Placeholder. The columns are the five statuses from F01; nothing is fetched yet
 * because the API contract for F01 has not been agreed, and building against a
 * guessed shape is what the contract-first rule in CLAUDE.md exists to prevent.
 *
 * When the contract lands, this page gains: one paged query per column
 * (50 rows, `Done` 20 ordered by `CompletedAt`), the four async states —
 * loading, empty, error, success — and drag-and-drop whose drop targets come
 * from each task's `allowedTransitions`.
 */
export function BoardPage() {
  return (
    <section>
      <h2 className={styles.heading}>Board</h2>
      <p className={styles.note}>
        Waiting on the F01 API contract — no data is fetched yet.
      </p>
      <div className={styles.columns}>
        {BOARD_COLUMNS.map((status) => (
          <section key={status} className={styles.column} aria-label={status}>
            <h3 className={styles.columnTitle}>{status}</h3>
            <p className={styles.empty}>No tasks</p>
          </section>
        ))}
      </div>
    </section>
  )
}
