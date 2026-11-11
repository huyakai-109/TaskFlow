import { useState } from 'react'
import { Link } from 'react-router-dom'

import { ComingSoon } from '../../../shared/components/ComingSoon'
import { useComingSoon } from '../../../shared/hooks/useComingSoon'
import { avatarPaletteIndex, initialsOf } from '../../../shared/lib/avatar'
import { IN_PROGRESS_LIMIT, useTeamCapacity } from '../../../shared/hooks/useTeamCapacity'

interface TeamCapacityPopoverProps {
  onClose: () => void
}

/**
 * `docs/design/SPEC.md` §4 "Popover (Team capacity)" / §5.2, anchored under
 * the Board toolbar's `AvatarStack` chevron (`docs/design/COMING-SOON.md` A9,
 * built this session). Same `useTeamCapacity` data and ordering as the
 * standalone `/team-capacity` page (A2) — same completeness caveat (board's
 * `InProgress` column, capped at `BoardColumnPageSize`, see COMING-SOON.md).
 * The page behind is not dimmed (§5.2). The header's "max —" is the same
 * B4 Coming-soon stub as the standalone page's.
 */
export function TeamCapacityPopover({ onClose }: TeamCapacityPopoverProps) {
  const capacity = useTeamCapacity()
  const [query, setQuery] = useState('')
  const comingSoon = useComingSoon('Reading the configured limit from the server is coming soon.')

  const filtered = capacity.entries.filter((entry) => entry.user.userName.toLowerCase().includes(query.trim().toLowerCase()))

  return (
    <div
      role="dialog"
      aria-label="Team capacity"
      className="fixed inset-x-[var(--space-4)] top-[72px] z-50 flex max-h-[467px] flex-col gap-[var(--space-3)] overflow-y-auto rounded-xl border border-border bg-surface-raised p-[var(--space-3)] shadow-raised min-[601px]:absolute min-[601px]:inset-x-auto min-[601px]:top-[calc(100%+8px)] min-[601px]:right-0 min-[601px]:w-[340px]"
    >
      <div className="flex items-start justify-between gap-[var(--space-2)]">
        <div>
          <h2 className="m-0 text-[length:var(--font-size-base)] font-semibold">Team capacity</h2>
          <p className="m-0 text-[length:var(--font-size-xs)] text-text-muted">
            In progress · max{' '}
            <button
              type="button"
              aria-disabled="true"
              className="cursor-pointer border-0 bg-transparent p-0 text-inherit underline decoration-dashed [font:inherit]"
              onClick={() => comingSoon.show()}
            >
              —
            </button>
          </p>
        </div>
        <button
          type="button"
          aria-label="Close"
          className="inline-flex size-6 shrink-0 cursor-pointer items-center justify-center rounded-sm border-0 bg-transparent text-text-muted"
          onClick={onClose}
        >
          ×
        </button>
      </div>

      <input
        type="text"
        placeholder="Find a teammate"
        aria-label="Find a teammate"
        className="min-h-9 rounded-md border border-border bg-surface px-[var(--space-3)] text-[length:var(--font-size-sm)] text-text"
        value={query}
        onChange={(event) => setQuery(event.target.value)}
      />

      {!capacity.isPending && !capacity.isError && (
        <p className="m-0 text-[length:var(--font-size-xs)] text-text-muted">
          <strong className="font-semibold text-danger">{capacity.fullCount}</strong> full,{' '}
          <strong className="font-semibold text-text">{capacity.almostFullCount}</strong> almost full,{' '}
          <strong className="font-semibold text-text">
            {capacity.entries.length - capacity.fullCount - capacity.almostFullCount}
          </strong>{' '}
          have room
        </p>
      )}

      {capacity.isPending && (
        <p role="status" className="text-[length:var(--font-size-sm)] text-text-muted">
          Loading…
        </p>
      )}

      {!capacity.isPending && !capacity.isError && capacity.mayBeIncomplete && (
        <p className="m-0 text-[length:var(--font-size-xs)] text-text-muted">
          May not be complete —{' '}
          <Link to="/board" className="text-accent-ink underline" onClick={onClose}>
            see the full Board
          </Link>
          .
        </p>
      )}

      <ul className="m-0 flex list-none flex-col gap-[2px] p-0">
        {filtered.map(({ user, inProgressCount, isFull }) => (
          <li
            key={user.id}
            className={
              isFull
                ? 'flex min-h-9 items-center gap-[var(--space-2)] rounded-control bg-danger-surface px-[var(--space-2)]'
                : 'flex min-h-9 items-center gap-[var(--space-2)] rounded-control px-[var(--space-2)]'
            }
          >
            <span
              className="flex size-[22px] shrink-0 items-center justify-center rounded-pill text-[11px] font-semibold"
              style={{
                backgroundColor: `var(--avatar-${avatarPaletteIndex(user.id)}-bg)`,
                color: `var(--avatar-${avatarPaletteIndex(user.id)}-ink)`,
              }}
            >
              {initialsOf(user.userName)}
            </span>
            <span className="min-w-0 flex-1 truncate text-[length:var(--font-size-sm)]">{user.userName}</span>
            <span className="flex items-center gap-[3px]" aria-hidden="true">
              {Array.from({ length: IN_PROGRESS_LIMIT }).map((_, i) => (
                <span
                  key={i}
                  className="h-[6px] w-[14px] rounded-[3px]"
                  style={{
                    backgroundColor:
                      i < inProgressCount ? (isFull ? 'var(--color-priority-urgent)' : 'var(--color-accent)') : 'var(--color-meter-empty)',
                  }}
                />
              ))}
            </span>
            <span className={isFull ? 'text-[length:var(--font-size-xs)] font-semibold text-danger' : 'text-[length:var(--font-size-xs)] text-text-muted'}>
              {isFull ? 'Full' : `${inProgressCount}/${IN_PROGRESS_LIMIT}`}
            </span>
          </li>
        ))}
        {filtered.length === 0 && !capacity.isPending && (
          <li className="px-[var(--space-2)] py-[var(--space-2)] text-[length:var(--font-size-sm)] text-text-muted">No match</li>
        )}
      </ul>

      <Link
        to="/team-capacity"
        className="text-[length:var(--font-size-sm)] font-medium text-accent-ink no-underline hover:underline"
        onClick={onClose}
      >
        Open Team capacity →
      </Link>

      {comingSoon.message !== null && <ComingSoon message={comingSoon.message} onDismiss={comingSoon.hide} />}
    </div>
  )
}
