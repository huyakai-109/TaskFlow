import { useRef } from 'react'
import { Link } from 'react-router-dom'

import { describeError } from '../../../api/errorMessages'
import type { ReportTaskMetric } from '../../../api/types'
import { Button } from '../../../shared/components/Button'
import { PriorityIcon } from '../../../shared/components/PriorityIcon'
import { useDialogA11y } from '../../../shared/hooks/useDialogA11y'

import { useReportTasksQuery } from '../hooks/useReportTasksQuery'
import { formatDuration } from '../utils/formatDuration'
import { formatDateSafe } from '../utils/safeDate'

const dateFormatter = new Intl.DateTimeFormat('en', { day: 'numeric', month: 'short', hour: '2-digit', minute: '2-digit' })

interface DrillDownDialogProps {
  title: string
  userId?: number
  from: string
  to: string
  metric: ReportTaskMetric
  onClose: () => void
}

/**
 * §11.7 — a `role="dialog"` side panel, 480px wide at ≥768, full screen at
 * 390 (handled with Tailwind breakpoint classes below), `useDialogA11y` for
 * focus in / Esc closes / focus returns. Offset paging ("Show more", 30 per
 * page) — the house drill-down pattern, not keyset (ADR 0007's own rule).
 */
export function DrillDownDialog({ title, userId, from, to, metric, onClose }: DrillDownDialogProps) {
  const closeButtonRef = useRef<HTMLButtonElement>(null)
  useDialogA11y(closeButtonRef, onClose)

  const query = useReportTasksQuery({ userId, from, to, metric, enabled: true })
  const items = query.data?.pages.flatMap((page) => page.result ?? []) ?? []

  return (
    <div className="fixed inset-0 z-[100] flex justify-end bg-overlay" onClick={onClose}>
      <aside
        role="dialog"
        aria-modal="true"
        aria-labelledby="drilldown-title"
        className="flex h-full w-full flex-col gap-[var(--space-3)] overflow-y-auto bg-surface-raised p-[var(--space-4)] shadow-modal-lg min-[768px]:w-[480px]"
        onClick={(event) => event.stopPropagation()}
      >
        <div className="flex items-center justify-between gap-[var(--space-2)]">
          <h2 id="drilldown-title" className="m-0 text-[length:var(--font-size-lg)] font-semibold">
            {title}
          </h2>
          <button
            ref={closeButtonRef}
            type="button"
            aria-label="Close"
            className="inline-flex size-8 shrink-0 cursor-pointer items-center justify-center rounded-control border-0 bg-transparent text-text-muted hover:bg-column"
            onClick={onClose}
          >
            <svg aria-hidden="true" width="14" height="14" viewBox="0 0 16 16" fill="none" stroke="currentColor" strokeWidth="1.8" strokeLinecap="round">
              <path d="m3 3 10 10M13 3 3 13" />
            </svg>
          </button>
        </div>

        {query.isPending && (
          <p role="status" className="text-[length:var(--font-size-sm)] text-text-muted">
            Loading…
          </p>
        )}

        {query.isError && (
          <div role="alert" className="flex flex-col items-start gap-[var(--space-2)] rounded-md border border-danger-border bg-danger-surface p-[var(--space-3)] text-danger">
            <p className="m-0">{describeError(query.error)}</p>
            <Button type="button" size="sm" onClick={() => void query.refetch()}>
              Try again
            </Button>
          </div>
        )}

        {query.isSuccess && items.length === 0 && (
          <p className="text-[length:var(--font-size-sm)] text-text-muted">No tasks for this selection.</p>
        )}

        {items.length > 0 && (
          <ul className="m-0 flex list-none flex-col gap-[var(--space-2)] p-0">
            {items.map((item) => (
              <li key={item.taskId} className="flex flex-col gap-[2px] border-b border-border pb-[var(--space-2)]">
                <div className="flex items-center gap-[var(--space-2)]">
                  <span className="font-mono text-[length:var(--font-size-xs)] text-text-muted">#{item.taskId}</span>
                  {item.isDeleted ? (
                    <span className="text-text-muted">
                      {item.title} <span className="text-[length:var(--font-size-2xs)]">(deleted)</span>
                    </span>
                  ) : (
                    <Link to={`/tasks/${item.taskId}`} className="text-accent-ink underline">
                      {item.title}
                    </Link>
                  )}
                </div>
                <div className="flex flex-wrap items-center gap-[var(--space-2)] text-[length:var(--font-size-xs)] text-text-muted">
                  <PriorityIcon priority={item.priority} />
                  <span>{item.creditedUser?.displayName ?? item.creditedUser?.userName ?? 'Unassigned'}</span>
                  <time dateTime={item.occurredAt}>{formatDateSafe(dateFormatter, item.occurredAt)}</time>
                  {item.dueDate !== null && <span>Due {item.dueDate}</span>}
                  {item.cycleTimeSeconds !== null && <span>Cycle {formatDuration(item.cycleTimeSeconds)}</span>}
                </div>
              </li>
            ))}
          </ul>
        )}

        {query.hasNextPage === true && (
          <Button type="button" size="sm" className="self-start" loading={query.isFetchingNextPage} onClick={() => void query.fetchNextPage()}>
            {query.isFetchingNextPage ? 'Loading…' : 'Show more'}
          </Button>
        )}
      </aside>
    </div>
  )
}
