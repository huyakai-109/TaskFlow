import type { TaskHistoryEntry } from '../../../api/types'
import { avatarPaletteIndex, initialsOf } from '../../../shared/lib/avatar'

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
 * Splits the server's `message` into the actor's name (bold, SPEC §4 "actor
 * 600") and the rest of the sentence (regular, "sentence 400") — never
 * re-deriving the action text itself, only the leading name the server
 * already put there. Falls back to the whole message unsplit if it doesn't
 * start with the expected actor label (defensive, not expected in practice).
 */
function splitActorFromMessage(message: string, actorLabel: string): [string, string] {
  if (!message.startsWith(actorLabel)) {
    return ['', message]
  }

  return [actorLabel, message.slice(actorLabel.length)]
}

/**
 * The read-only activity timeline (F04). `message` is already a full,
 * human-readable sentence built server-side (`TaskHistoryMessages.Build`), so
 * this renders it as-is rather than re-deriving copy from `event`/`fromValue`/
 * `toValue` — F04 owns that text, not the client. A System entry (no actor)
 * gets its own icon, not an avatar (`docs/design/SPEC.md`).
 */
export function HistoryTimeline({ entries }: HistoryTimelineProps) {
  if (entries.length === 0) {
    return <p className="text-[length:var(--font-size-sm)] text-text-muted">No activity yet.</p>
  }

  return (
    <ol className="m-0 flex list-none flex-col border-l border-border py-0 pr-0 pl-[4px]">
      {entries.map((entry) => {
        const actorLabel = entry.actorUserId === null ? 'System' : (entry.actorName ?? '?')
        const [actorPart, restPart] = splitActorFromMessage(entry.message, actorLabel)

        return (
          <li
            key={entry.id}
            className="-ml-[11px] flex items-center gap-[var(--space-3)] py-[var(--space-2)] pr-0 pl-[var(--space-4)]"
          >
            {entry.actorUserId === null ? (
              <span
                aria-hidden="true"
                className="inline-flex size-5 shrink-0 items-center justify-center rounded-pill border border-danger-border bg-danger-surface text-danger"
              >
                <SystemIcon />
              </span>
            ) : (
              <span
                aria-hidden="true"
                className="inline-flex size-5 shrink-0 items-center justify-center rounded-pill text-[9px] font-semibold"
                style={{
                  backgroundColor: `var(--avatar-${avatarPaletteIndex(entry.actorUserId)}-bg)`,
                  color: `var(--avatar-${avatarPaletteIndex(entry.actorUserId)}-ink)`,
                }}
              >
                {initialsOf(entry.actorName ?? '?')}
              </span>
            )}
            <span className="grow">
              {actorPart !== '' && <strong className="font-semibold">{actorPart}</strong>}
              {restPart}
            </span>
            <time className="shrink-0 text-[length:var(--font-size-xs)] whitespace-nowrap text-text-muted" dateTime={entry.occurredAt}>
              {timestampFormatter.format(new Date(entry.occurredAtLocal))}
            </time>
          </li>
        )
      })}
    </ol>
  )
}
