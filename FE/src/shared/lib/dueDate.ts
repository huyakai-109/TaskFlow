/**
 * `dueDate` is a calendar date with no zone of its own (F01); "today" here is
 * read from the browser's local date, an approximation of the server's
 * configured zone used for `isOverdue` (F05). The server's `isOverdue` flag,
 * never this comparison, is what actually drives overdue styling — these are
 * cosmetic copy only (due chip, overdue chip, the properties panel's overdue
 * hint — second use, pulled out of `TaskCard.tsx` per `frontend.md` §2).
 */
export const dueDateFormatter = new Intl.DateTimeFormat('en', { day: 'numeric', month: 'short' })

const MS_PER_DAY = 86_400_000

export function parseDueDate(dueDate: string): Date {
  const [year, month, day] = dueDate.split('-').map(Number)
  return new Date(year, month - 1, day)
}

export function isToday(dueDate: string): boolean {
  const today = new Date()
  const parsed = parseDueDate(dueDate)
  return (
    parsed.getFullYear() === today.getFullYear() &&
    parsed.getMonth() === today.getMonth() &&
    parsed.getDate() === today.getDate()
  )
}

export function daysLate(dueDate: string): number {
  const today = new Date()
  const startOfToday = new Date(today.getFullYear(), today.getMonth(), today.getDate())
  return Math.max(1, Math.round((startOfToday.getTime() - parseDueDate(dueDate).getTime()) / MS_PER_DAY))
}
