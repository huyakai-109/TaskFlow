import { ComingSoon } from '../../shared/components/ComingSoon'
import { useComingSoon } from '../../shared/hooks/useComingSoon'
import { Link } from 'react-router-dom'

import { avatarPaletteIndex, initialsOf } from '../../shared/lib/avatar'
import { IN_PROGRESS_LIMIT, useTeamCapacity } from '../../shared/hooks/useTeamCapacity'

/**
 * `docs/design/SPEC.md` §5.2/§10 point 3 (reopened 2026-11-11): a standalone
 * page version of the Team capacity popover's list — same rows (avatar,
 * name, meter, `n/3`/"Full"), same full-first/most-loaded-first order, same
 * summary line. The popover itself (anchored under the Board toolbar's
 * avatar stack) is not built this session — `docs/design/COMING-SOON.md` A9.
 *
 * The header's "max —" (`docs/design/COMING-SOON.md` B4) is a real "Coming
 * soon" stub: the configured in-progress limit isn't exposed by any
 * endpoint, so this shows `—` rather than the `IN_PROGRESS_LIMIT` constant
 * the rows below still use internally (an already-approved, flagged
 * assumption for sorting/grouping — unchanged by this stub).
 */
export function TeamCapacityPage() {
  const capacity = useTeamCapacity()
  const comingSoon = useComingSoon('Reading the configured limit from the server is coming soon.')

  return (
    // No hard `max-w` (layout fix, 2026-11-12 again, "pages use the full
    // width" pass) — this page's content used to be gated to 560px, popover
    // row width, inside a ~1312px content area. A grid of cards (one per
    // teammate) replaces the single popover-style list below so the extra
    // width is actually used, not just stretched.
    <section className="mx-auto flex w-full max-w-[1680px] flex-col gap-[var(--space-4)]">
      <div>
        <h1 className="m-0 text-[length:var(--font-size-xl)] font-semibold">Team capacity</h1>
        <p className="m-0 mt-[var(--space-1)] text-[length:var(--font-size-sm)] text-text-muted">
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

      {!capacity.isPending && !capacity.isError && capacity.mayBeIncomplete && (
        <p className="m-0 text-[length:var(--font-size-xs)] text-text-muted">
          This list may not be complete —{' '}
          <Link to="/board" className="text-accent-ink underline">
            see the full In Progress column on the Board
          </Link>{' '}
          for complete data.
        </p>
      )}

      {capacity.isPending && (
        <p role="status" className="text-[length:var(--font-size-sm)] text-text-muted">
          Loading team capacity…
        </p>
      )}

      {capacity.isError && (
        <p role="alert" className="text-[length:var(--font-size-sm)] text-danger">
          Could not load team capacity.
        </p>
      )}

      {!capacity.isPending && !capacity.isError && (
        <>
          <p className="m-0 text-[length:var(--font-size-sm)] text-text-muted">
            <strong className="font-semibold text-danger">{capacity.fullCount}</strong> full,{' '}
            <strong className="font-semibold text-text">{capacity.almostFullCount}</strong> almost full,{' '}
            <strong className="font-semibold text-text">
              {capacity.entries.length - capacity.fullCount - capacity.almostFullCount}
            </strong>{' '}
            have room
          </p>

          <ul
            className="m-0 grid list-none gap-[var(--space-3)] p-0"
            style={{ gridTemplateColumns: 'repeat(auto-fill, minmax(280px, 1fr))' }}
          >
            {capacity.entries.map(({ user, inProgressCount, isFull }) => (
              <li
                key={user.id}
                className={
                  isFull
                    ? 'flex min-h-[72px] min-w-0 items-center gap-[var(--space-3)] rounded-card-lg border border-danger-border bg-danger-surface p-[var(--space-3)]'
                    : 'flex min-h-[72px] min-w-0 items-center gap-[var(--space-3)] rounded-card-lg border border-border bg-surface p-[var(--space-3)]'
                }
              >
                <span
                  className="flex size-[32px] shrink-0 items-center justify-center rounded-pill text-[12px] font-semibold"
                  style={{
                    backgroundColor: `var(--avatar-${avatarPaletteIndex(user.id)}-bg)`,
                    color: `var(--avatar-${avatarPaletteIndex(user.id)}-ink)`,
                  }}
                >
                  {initialsOf(user.userName)}
                </span>
                <span className="flex min-w-0 flex-1 flex-col gap-[var(--space-1)]">
                  <span className="truncate text-[length:var(--font-size-base)] font-medium">{user.userName}</span>
                  <span className="flex items-center gap-[3px]" aria-hidden="true">
                    {Array.from({ length: IN_PROGRESS_LIMIT }).map((_, i) => (
                      <span
                        key={i}
                        className="h-[6px] w-[14px] rounded-[3px]"
                        style={{
                          backgroundColor:
                            i < inProgressCount
                              ? isFull
                                ? 'var(--color-priority-urgent)'
                                : 'var(--color-accent)'
                              : 'var(--color-meter-empty)',
                        }}
                      />
                    ))}
                  </span>
                </span>
                <span
                  className={
                    isFull
                      ? 'shrink-0 text-[length:var(--font-size-xs)] font-semibold text-danger'
                      : 'shrink-0 text-[length:var(--font-size-xs)] text-text-muted'
                  }
                >
                  {isFull ? 'Full' : `${inProgressCount}/${IN_PROGRESS_LIMIT}`}
                </span>
              </li>
            ))}
          </ul>
        </>
      )}

      {comingSoon.message !== null && <ComingSoon message={comingSoon.message} onDismiss={comingSoon.hide} />}
    </section>
  )
}
