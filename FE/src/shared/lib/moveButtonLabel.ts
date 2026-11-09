import type { AllowedTransition, TaskStatus } from '../../api/types'
import { STATUS_LABELS } from './statusLabels'

/**
 * The two transition labels SPEC.md names explicitly — "Submit for review"
 * (Task detail §5.3) and "Start" (My tasks §5.8, the "Up next" card) — both
 * only for the one single-move case they're drawn for. Every other
 * transition keeps the plain "Move to X" rather than inventing unasked-for
 * copy (second use, pulled out of `WorkflowStrip.tsx`).
 */
export function moveButtonLabel(status: TaskStatus, move: AllowedTransition): string {
  if (status === 'InProgress' && move.status === 'Review') {
    return 'Submit for review'
  }

  if (status === 'Todo' && move.status === 'InProgress') {
    return 'Start'
  }

  return `Move to ${STATUS_LABELS[move.status]}`
}
