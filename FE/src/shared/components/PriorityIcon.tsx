import type { TaskPriority } from '../../api/types'

/**
 * The priority glyph — urgent "!" square, or a 2/3-bar meter for High/Normal.
 * Extracted from the board card (`TaskCard`) at the Overdue view's second use
 * (`frontend.md` §2) so the icon is defined once and both screens render the
 * same priority, the same way.
 */
export function PriorityIcon({ priority }: { priority: TaskPriority }) {
  if (priority === 'Urgent') {
    return (
      <svg aria-hidden="true" width="12" height="12" viewBox="0 0 16 16">
        <rect x="1" y="1" width="14" height="14" rx="3.5" fill="var(--color-priority-urgent)" />
        <path d="M8 4.5v4.5M8 11.5v.2" stroke="var(--color-surface)" strokeWidth="1.9" strokeLinecap="round" />
      </svg>
    )
  }

  const filledBars = priority === 'High' ? 3 : 2
  const color = priority === 'High' ? 'var(--color-priority-high)' : 'var(--color-priority-normal)'
  const empty = 'var(--color-meter-empty)'

  return (
    <svg aria-hidden="true" width="12" height="12" viewBox="0 0 16 16">
      <rect x="1.5" y="9" width="3" height="5" rx="1" fill={filledBars >= 1 ? color : empty} />
      <rect x="6.5" y="5.5" width="3" height="8.5" rx="1" fill={filledBars >= 2 ? color : empty} />
      <rect x="11.5" y="2" width="3" height="12" rx="1" fill={filledBars >= 3 ? color : empty} />
    </svg>
  )
}
