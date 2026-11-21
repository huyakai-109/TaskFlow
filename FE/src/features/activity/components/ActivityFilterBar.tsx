import type { TaskHistoryEvent } from '../../../api/types'
import { useUsersQuery } from '../../../shared/hooks/useUsersQuery'

export interface ActivityFilterState {
  actor: string
  from: string
  to: string
  events: TaskHistoryEvent[]
}

const EVENT_OPTIONS: TaskHistoryEvent[] = [
  'Created',
  'StatusChanged',
  'AssigneeChanged',
  'PriorityChanged',
  'DueDateChanged',
  'Reopened',
  'OverdueMarked',
  'Deleted',
]

const inputClass = 'min-h-9 rounded-md border border-border bg-surface px-[var(--space-2)] text-text [font:inherit] disabled:opacity-60'

interface ActivityFilterBarProps {
  value: ActivityFilterState
  onChange: (value: ActivityFilterState) => void
  onRefresh: () => void
  isRefreshing: boolean
}

/**
 * Actor (a user, or "System") + date range + event-type multi-select
 * (`docs/features/f04-activity-feed.md` "UI"). A native multi-`<select>` for
 * event types, same reasoning `AssigneeSelect.tsx` already gives for staying
 * with a plain native control instead of a custom listbox: fully keyboard-
 * and screen-reader-operable with no extra widget logic to get right.
 */
export function ActivityFilterBar({ value, onChange, onRefresh, isRefreshing }: ActivityFilterBarProps) {
  const usersQuery = useUsersQuery()

  return (
    <div className="flex flex-wrap items-end gap-[var(--space-3)]">
      <div className="flex flex-col gap-[var(--space-1)]">
        <label htmlFor="activity-actor" className="text-[length:var(--font-size-xs)] text-text-muted">
          Actor
        </label>
        <select
          id="activity-actor"
          className={inputClass}
          value={value.actor}
          onChange={(event) => onChange({ ...value, actor: event.target.value })}
        >
          <option value="">Everyone</option>
          <option value="system">System</option>
          {(usersQuery.data ?? []).map((user) => (
            <option key={user.id} value={String(user.id)}>
              {user.displayName ?? user.userName}
            </option>
          ))}
        </select>
      </div>

      <div className="flex flex-col gap-[var(--space-1)]">
        <label htmlFor="activity-from" className="text-[length:var(--font-size-xs)] text-text-muted">
          From
        </label>
        <input
          id="activity-from"
          type="date"
          className={inputClass}
          value={value.from}
          onChange={(event) => onChange({ ...value, from: event.target.value })}
        />
      </div>

      <div className="flex flex-col gap-[var(--space-1)]">
        <label htmlFor="activity-to" className="text-[length:var(--font-size-xs)] text-text-muted">
          To
        </label>
        <input
          id="activity-to"
          type="date"
          className={inputClass}
          value={value.to}
          onChange={(event) => onChange({ ...value, to: event.target.value })}
        />
      </div>

      <div className="flex flex-col gap-[var(--space-1)]">
        <label htmlFor="activity-events" className="text-[length:var(--font-size-xs)] text-text-muted">
          Event type
        </label>
        <select
          id="activity-events"
          multiple
          className={`${inputClass} h-[72px]`}
          value={value.events}
          onChange={(event) => {
            const selected = Array.from(event.target.selectedOptions, (option) => option.value as TaskHistoryEvent)
            onChange({ ...value, events: selected })
          }}
        >
          {EVENT_OPTIONS.map((option) => (
            <option key={option} value={option}>
              {option}
            </option>
          ))}
        </select>
      </div>

      {(value.actor !== '' || value.from !== '' || value.to !== '' || value.events.length > 0) && (
        <button
          type="button"
          className="min-h-9 cursor-pointer border-0 bg-transparent text-[length:var(--font-size-sm)] text-accent-ink underline"
          onClick={() => onChange({ actor: '', from: '', to: '', events: [] })}
        >
          Clear filters
        </button>
      )}

      <button
        type="button"
        className="min-h-9 cursor-pointer rounded-md border border-border bg-surface px-[var(--space-3)] text-[length:var(--font-size-sm)] font-medium text-text hover:bg-column disabled:cursor-not-allowed disabled:opacity-60"
        disabled={isRefreshing}
        onClick={onRefresh}
      >
        {isRefreshing ? 'Refreshing…' : 'Refresh'}
      </button>
    </div>
  )
}
