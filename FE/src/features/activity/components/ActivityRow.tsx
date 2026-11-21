import type { ActivityEntry } from '../../../api/types'
import { Avatar } from '../../../shared/components/Avatar'
import { useUsersQuery } from '../../../shared/hooks/useUsersQuery'

const dateTimeFormatter = new Intl.DateTimeFormat('en', { day: 'numeric', month: 'short', hour: '2-digit', minute: '2-digit' })

function SystemClockIcon() {
  return (
    <svg aria-hidden="true" width="20" height="20" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="1.8" strokeLinecap="round">
      <circle cx="12" cy="12" r="9" />
      <path d="M12 7v5l3 2" />
    </svg>
  )
}

/** One cross-task history row (`docs/design/SPEC.md` §4 "Activity row"). */
export function ActivityRow({ entry }: { entry: ActivityEntry }) {
  const usersQuery = useUsersQuery()
  const roster = entry.actorUserId === null ? null : usersQuery.data?.find((user) => user.id === entry.actorUserId)
  const isSystem = entry.actorUserId === null

  return (
    <li className="flex items-start gap-[var(--space-3)] border-b border-border px-[var(--space-2)] py-[var(--space-3)]">
      {isSystem ? (
        <span className="flex size-5 shrink-0 items-center justify-center rounded-pill bg-danger-surface text-danger">
          <SystemClockIcon />
        </span>
      ) : (
        <Avatar userId={entry.actorUserId!} name={roster?.displayName ?? entry.actorName ?? 'Unknown'} avatarUrl={roster?.avatarUrl ?? null} size={20} />
      )}
      <div className="flex min-w-0 flex-1 flex-col gap-[2px]">
        <p className="m-0 text-[length:var(--font-size-sm)]">
          <span className="font-semibold text-text">{isSystem ? 'System' : (roster?.displayName ?? entry.actorName ?? 'Unknown')}</span>{' '}
          <span className="text-text">{entry.message}</span>
        </p>
        {entry.task.isDeleted && <span className="text-[length:var(--font-size-2xs)] text-text-muted">(task deleted)</span>}
      </div>
      <time dateTime={entry.occurredAt} className="shrink-0 text-[length:var(--font-size-xs)] text-text-muted">
        {dateTimeFormatter.format(new Date(entry.occurredAt))}
      </time>
    </li>
  )
}
