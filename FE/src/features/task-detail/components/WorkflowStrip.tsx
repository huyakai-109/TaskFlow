import clsx from 'clsx'

import { BOARD_COLUMNS } from '../../../api/types'
import type { AllowedTransition, TaskStatus } from '../../../api/types'
import { STATUS_LABELS } from '../../../shared/lib/statusLabels'

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

/** `InProgress → Review` is the one transition the design names explicitly ("Submit for review", SPEC §5.3); every other move keeps its plain "Move to X" label rather than inventing unasked-for copy for the rest. */
function labelFor(status: TaskStatus, move: AllowedTransition): string {
  return status === 'InProgress' && move.status === 'Review' ? 'Submit for review' : `Move to ${STATUS_LABELS[move.status]}`
}

/**
 * The workflow stages plus the caller's legal actions. `docs/design/SPEC.md`'s
 * `TaskDetail.dc.html` draws exactly one next-step button because its sample
 * task is `InProgress`; `Review` legally offers two to a non-assignee
 * (`Done` and back to `InProgress`), so every `Move` entry in
 * `allowedTransitions` gets its own button — D10's "show only legal moves",
 * not a narrower reading of the wireframe's single sample.
 */
export function WorkflowStrip({ status, moves, reopen, onMove, onReopenRequested }: WorkflowStripProps) {
  const currentIndex = BOARD_COLUMNS.indexOf(status)

  return (
    <section
      aria-labelledby="workflow-heading"
      className="flex flex-col gap-[var(--space-4)] rounded-xl border border-border bg-surface p-[var(--space-5)] shadow-card"
    >
      <h2 id="workflow-heading" className="text-[length:var(--font-size-sm)] font-semibold text-text-muted">
        Workflow
      </h2>
      <ol
        aria-label="Workflow stages"
        className="m-0 flex list-none flex-wrap items-center gap-[var(--space-2)] p-0 text-[length:var(--font-size-sm)]"
      >
        {BOARD_COLUMNS.map((stage, index) => (
          <li key={stage} className="flex items-center gap-[var(--space-2)]">
            {index > 0 && (
              <span aria-hidden="true" className="text-faint">
                —
              </span>
            )}
            <span
              aria-current={stage === status ? 'step' : undefined}
              className={clsx(
                'inline-flex min-h-6 items-center gap-[var(--space-2)] rounded-lg bg-column px-[var(--space-3)] text-text-muted',
                stage === status && 'bg-accent-soft font-semibold text-accent-ink',
                index > currentIndex && 'min-h-[26px] border border-dashed border-border-strong bg-transparent',
              )}
            >
              {index < currentIndex && <CheckIcon />}
              {STATUS_LABELS[stage]}
            </span>
          </li>
        ))}
      </ol>

      <div className="flex flex-wrap items-center gap-[var(--space-3)]">
        {moves.map((move) => (
          <button
            key={move.status}
            type="button"
            className="inline-flex min-h-11 cursor-pointer items-center rounded-md border-0 bg-accent px-[var(--space-4)] font-semibold text-on-accent hover:bg-accent-hover"
            onClick={() => onMove(move.status)}
          >
            {labelFor(status, move)}
          </button>
        ))}

        {reopen !== undefined && (
          <button
            type="button"
            className="inline-flex min-h-11 cursor-pointer items-center rounded-md border border-border bg-surface px-[var(--space-4)] font-medium text-text hover:bg-column"
            onClick={onReopenRequested}
          >
            Reopen
          </button>
        )}

        {moves.length === 0 && reopen === undefined && (
          <span className="text-[length:var(--font-size-sm)] text-text-muted">
            {status === 'Done'
              ? "This one's finished. If it needs more work, a manager can reopen it."
              : status === 'Review'
                ? 'Only someone other than the assignee can move this out of Review.'
                : 'No action available for you right now.'}
          </span>
        )}
      </div>

      {moves.length === 1 && (
        <p className="m-0 text-[length:var(--font-size-xs)] text-text-muted">
          The only next step from {STATUS_LABELS[status]}.
          {status === 'InProgress' && ' Anyone other than you can send it back here for rework.'}
        </p>
      )}
    </section>
  )
}
