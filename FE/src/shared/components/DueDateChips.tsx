import clsx from 'clsx'

import { daysLate, dueDateFormatter, isToday, parseDueDate } from '../lib/dueDate'

export const chipBase = 'inline-flex min-h-[21px] shrink-0 items-center gap-[var(--space-1)] whitespace-nowrap rounded-sm border border-border px-[var(--space-2)]'

/** Second use (`MyTasksPage.tsx`) — pulled out of `TaskCard.tsx` so both share one due/overdue chip, not two hand-rolled formats. */
export function DueChip({ dueDate }: { dueDate: string | null }) {
  if (dueDate === null) {
    return <span className={clsx(chipBase, 'border-dashed border-dashed-border')}>No due date</span>
  }

  if (isToday(dueDate)) {
    return <span className={clsx(chipBase, 'font-medium text-text')}>Due today</span>
  }

  return <span className={chipBase}>{dueDateFormatter.format(parseDueDate(dueDate))}</span>
}

export function OverdueChip({ dueDate }: { dueDate: string | null }) {
  return (
    <span className={clsx(chipBase, 'border-transparent bg-overdue-surface font-semibold text-overdue')}>
      <svg aria-hidden="true" width="12" height="12" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2.2" strokeLinecap="round">
        <circle cx="12" cy="12" r="9" />
        <path d="M12 7v5l3 2" />
      </svg>
      {dueDate === null ? 'Overdue' : `Overdue ${daysLate(dueDate)}d`}
    </span>
  )
}
