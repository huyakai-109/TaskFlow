import { Avatar } from '../../shared/components/Avatar'
import { Link } from 'react-router-dom'

import { useTeamCapacity, IN_PROGRESS_LIMIT } from '../../shared/hooks/useTeamCapacity'
import { useWorkflowQuery } from '../../shared/hooks/useWorkflowQuery'
import { userLabel } from '../../shared/lib/userLabel'

/**
 * `docs/design/SPEC.md` §5.2/§10 point 3 (reopened 2026-11-11): a standalone
 * page version of the Team capacity popover's list — same rows (avatar,
 * name, meter, `n/3`/"Full"), same full-first/most-loaded-first order, same
 * summary line. The popover itself (anchored under the Board toolbar's
 * avatar stack) is not built this session — `docs/design/COMING-SOON.md` A9.
 *
 * The header's "max N" used to be a "Coming soon" stub (`docs/design/
 * COMING-SOON.md` B4, "max —") because no endpoint exposed the configured
 * limit — `GET /workflow` (f10-read-models-and-sort.md §4) now does, so this
 * reads the real value instead of a literal. `useTeamCapacity`'s own
 * per-row grouping/meter still uses its separately-flagged `IN_PROGRESS_LIMIT`
 * assumption (unchanged by this fix — see that hook's own doc comment).
 */
export function TeamCapacityPage() {
  const capacity = useTeamCapacity()
  const workflowQuery = useWorkflowQuery()

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
          {workflowQuery.isError ? '—' : (workflowQuery.data?.maxInProgressPerAssignee ?? '…')}
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
                <Avatar userId={user.id} name={userLabel(user)} avatarUrl={user.avatarUrl} size={32} />
                <span className="flex min-w-0 flex-1 flex-col gap-[var(--space-1)]">
                  <span className="truncate text-[length:var(--font-size-base)] font-medium">{userLabel(user)}</span>
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
    </section>
  )
}
