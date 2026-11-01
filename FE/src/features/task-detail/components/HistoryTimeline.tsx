import type { TaskHistoryEntry } from '../../../api/types'
import { avatarPaletteIndex, initialsOf } from '../../../shared/lib/avatar'

import styles from './HistoryTimeline.module.css'

const timestampFormatter = new Intl.DateTimeFormat('en', { day: 'numeric', month: 'short', hour: '2-digit', minute: '2-digit' })

function SystemIcon() {
  return (
    <svg aria-hidden="true" width="11" height="11" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2.4" strokeLinecap="round">
      <circle cx="12" cy="12" r="9" />
      <path d="M12 7v5l3 2" />
    </svg>
  )
}

interface HistoryTimelineProps {
  entries: TaskHistoryEntry[]
}

/**
 * The read-only activity timeline (F04). `message` is already a full,
 * human-readable sentence built server-side (`TaskHistoryMessages.Build`), so
 * this renders it as-is rather than re-deriving copy from `event`/`fromValue`/
 * `toValue` — F04 owns that text, not the client. A System entry (no actor)
 * gets its own icon, not an avatar (`docs/ui.md`).
 */
export function HistoryTimeline({ entries }: HistoryTimelineProps) {
  if (entries.length === 0) {
    return <p className={styles.empty}>No activity yet.</p>
  }

  return (
    <ol className={styles.timeline}>
      {entries.map((entry) => (
        <li key={entry.id} className={styles.entry}>
          {entry.actorUserId === null ? (
            <span aria-hidden="true" className={styles.systemIcon}>
              <SystemIcon />
            </span>
          ) : (
            <span
              aria-hidden="true"
              className={styles.actorAvatar}
              style={{
                backgroundColor: `var(--avatar-${avatarPaletteIndex(entry.actorUserId)}-bg)`,
                color: `var(--avatar-${avatarPaletteIndex(entry.actorUserId)}-ink)`,
              }}
            >
              {initialsOf(entry.actorName ?? '?')}
            </span>
          )}
          <span className={styles.message}>{entry.message}</span>
          <time className={styles.timestamp} dateTime={entry.occurredAt}>
            {timestampFormatter.format(new Date(entry.occurredAtLocal))}
          </time>
        </li>
      ))}
    </ol>
  )
}
