import type { ReportDistribution } from '../../../api/types'

import { formatDuration } from './formatDuration'

/** §C.1 (2026-11-22 brief) — one plain-English line per scorecard card, generated from the data, not a static label. */

export function summarizeDelivery(done: number): string {
  if (done === 0) {
    return 'No finished tasks in this period'
  }

  return `Finished ${done} task${done === 1 ? '' : 's'}`
}

export function summarizeSpeed(cycleTime: ReportDistribution): string {
  if (cycleTime.sampleSize === 0) {
    return 'No finished tasks in this period'
  }

  let text = `Typically finished in ${formatDuration(cycleTime.medianSeconds)}`

  if (cycleTime.p85Seconds !== null) {
    text += `; most within ${formatDuration(cycleTime.p85Seconds)}`
  }

  if (cycleTime.lowSample) {
    text += ' (few data)'
  }

  return text
}

export function summarizeReliability(onTime: number, late: number): string {
  const total = onTime + late

  if (total === 0) {
    return 'No finished tasks in this period'
  }

  const rate = Math.round((onTime / total) * 100)
  return `${onTime} of ${total} on time (${rate}%)`
}

export function summarizeQuality(ticketsWorked: number, sentBack: number, submissionsDecided: number, lowSample: boolean): string {
  if (ticketsWorked === 0 && submissionsDecided === 0) {
    return 'No finished tasks in this period'
  }

  let text = `Worked on ${ticketsWorked} ticket${ticketsWorked === 1 ? '' : 's'}`

  if (submissionsDecided > 0) {
    const rate = Math.round((sentBack / submissionsDecided) * 100)
    text += `; ${sentBack} of ${submissionsDecided} sent back (${rate}%)`
  }

  if (lowSample) {
    text += ' (few data)'
  }

  return text
}
