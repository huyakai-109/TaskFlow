import { BOARD_COLUMNS } from '../../api/types'
import type { AllowedTransition, TaskStatus } from '../../api/types'
import { STATUS_LABELS } from '../board/statusLabels'

import styles from './WorkflowStrip.module.css'

function CheckIcon() {
  return (
    <svg aria-hidden="true" width="12" height="12" viewBox="0 0 16 16">
      <path d="m3.5 8.2 3 3 6-6.2" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round" />
    </svg>
  )
}

interface WorkflowStripProps {
  status: TaskStatus
  /** `allowedTransitions` already filtered to `operation === 'Move'` — every legal next step, not just one. */
  moves: AllowedTransition[]
  /** The `Done → Review` reopen edge, present only when the caller holds `ReopenTasks` (F01 D10). */
  reopen: AllowedTransition | undefined
  onMove: (toStatus: TaskStatus) => void
  onReopenRequested: () => void
}

/**
 * The workflow stages plus the caller's legal actions. `docs/ui.md`'s
 * `TaskDetail.dc.html` draws exactly one next-step button because its sample
 * task is `InProgress`; `Review` legally offers two to a non-assignee
 * (`Done` and back to `InProgress`), so every `Move` entry in
 * `allowedTransitions` gets its own button — D10's "show only legal moves",
 * not a narrower reading of the wireframe's single sample.
 */
export function WorkflowStrip({ status, moves, reopen, onMove, onReopenRequested }: WorkflowStripProps) {
  const currentIndex = BOARD_COLUMNS.indexOf(status)

  return (
    <section aria-labelledby="workflow-heading" className={styles.card}>
      <h2 id="workflow-heading" className={styles.sectionTitle}>
        Workflow
      </h2>
      <ol aria-label="Workflow stages" className={styles.stages}>
        {BOARD_COLUMNS.map((stage, index) => (
          <li
            key={stage}
            aria-current={stage === status ? 'step' : undefined}
            className={`${styles.stage} ${stage === status ? styles.stageCurrent : ''} ${index > currentIndex ? styles.stageFuture : ''}`}
          >
            {index < currentIndex && <CheckIcon />}
            {STATUS_LABELS[stage]}
          </li>
        ))}
      </ol>

      <div className={styles.actions}>
        {moves.map((move) => (
          <button key={move.status} type="button" className={styles.primaryAction} onClick={() => onMove(move.status)}>
            Move to {STATUS_LABELS[move.status]}
          </button>
        ))}

        {reopen !== undefined && (
          <button type="button" className={styles.secondaryAction} onClick={onReopenRequested}>
            Reopen
          </button>
        )}

        {moves.length === 0 && reopen === undefined && (
          <span className={styles.lockedNote}>
            {status === 'Done'
              ? "This one's finished. If it needs more work, a manager can reopen it."
              : status === 'Review'
                ? 'Only someone other than the assignee can move this out of Review.'
                : 'No action available for you right now.'}
          </span>
        )}
      </div>
    </section>
  )
}
