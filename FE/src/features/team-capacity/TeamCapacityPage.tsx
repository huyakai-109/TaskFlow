import { avatarPaletteIndex, initialsOf } from '../../shared/lib/avatar'
import { IN_PROGRESS_LIMIT, useTeamCapacity } from '../../shared/hooks/useTeamCapacity'

/**
 * `docs/design/SPEC.md` §5.2/§10 point 3 (reopened 2026-11-11): a standalone
 * page version of the Team capacity popover's list — same rows (avatar,
 * name, meter, `n/3`/"Full"), same full-first/most-loaded-first order, same
 * summary line. The popover itself (anchored under the Board toolbar's
 * avatar stack) is not built this session — `docs/design/COMING-SOON.md` A9.
 */
export function TeamCapacityPage() {
  const capacity = useTeamCapacity()

  return (
    <section className="flex max-w-[560px] flex-col gap-[var(--space-4)]">
      <div>
        <h1 className="m-0 text-[length:var(--font-size-xl)] font-semibold">Team capacity</h1>
        <p className="m-0 mt-[var(--space-1)] text-[length:var(--font-size-sm)] text-text-muted">
          In progress · max {IN_PROGRESS_LIMIT}
        </p>
      </div>

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

          <ul className="m-0 flex list-none flex-col gap-[2px] p-0">
            {capacity.entries.map(({ user, inProgressCount, isFull }) => (
              <li
                key={user.id}
                className={
                  isFull
                    ? 'flex min-h-9 items-center gap-[var(--space-3)] rounded-control bg-danger-surface px-[var(--space-3)]'
                    : 'flex min-h-9 items-center gap-[var(--space-3)] rounded-control px-[var(--space-3)]'
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
                          i < inProgressCount
                            ? isFull
                              ? 'var(--color-priority-urgent)'
                              : 'var(--color-accent)'
                            : 'var(--color-meter-empty)',
                      }}
                    />
                  ))}
                </span>
                <span
                  className={
                    isFull
                      ? 'text-[length:var(--font-size-xs)] font-semibold text-danger'
                      : 'text-[length:var(--font-size-xs)] text-text-muted'
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
