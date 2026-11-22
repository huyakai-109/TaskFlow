import { useState } from 'react'

const dateFormatter = new Intl.DateTimeFormat('en', { day: 'numeric', month: 'short', year: 'numeric' })

function ChevronIcon({ expanded }: { expanded: boolean }) {
  return (
    <svg
      aria-hidden="true"
      width="12"
      height="12"
      viewBox="0 0 16 16"
      fill="none"
      stroke="currentColor"
      strokeWidth="1.8"
      strokeLinecap="round"
      strokeLinejoin="round"
      style={{ transform: expanded ? 'rotate(180deg)' : undefined, transition: 'transform 120ms ease' }}
    >
      <path d="m4 6 4 4 4-4" />
    </svg>
  )
}

interface CaveatsPanelProps {
  timeZone: string
  inProgressLimit: number
  dataAvailableSince: string | null
  smallSampleThreshold: number
}

/**
 * §11.5 caveats, copied verbatim (the reopened-work line especially — "can
 * change, including a period you've already looked at"). §C.4 (2026-11-22
 * brief) — collapsed by default on every visit (no longer "always visible
 * the first time": the 7 paragraphs pushed the scorecard below the fold),
 * behind the same "Show table" disclosure pattern `ChartFigure` already
 * uses — not a reason to delete or reword a single sentence.
 * `dataAvailableSince` is shown separately, outside this panel, under the
 * filters (`ReportsPage`), so it stays visible without expanding anything.
 */
export function CaveatsPanel({ timeZone, inProgressLimit, dataAvailableSince, smallSampleThreshold }: CaveatsPanelProps) {
  const [expanded, setExpanded] = useState(false)

  function toggle() {
    setExpanded((previous) => !previous)
  }

  return (
    <div className="rounded-lg bg-side-panel p-[var(--space-3)] text-[13px] text-text-2">
      <button
        type="button"
        aria-expanded={expanded}
        aria-controls="reports-caveats-body"
        className="flex w-full cursor-pointer items-center justify-between gap-[var(--space-2)] border-0 bg-transparent p-0 text-left font-medium text-text"
        onClick={toggle}
      >
        <span>Read the caveats before using these figures — indicators, not verdicts, credit, reopened work, time, capacity, data since, small samples.</span>
        <ChevronIcon expanded={expanded} />
      </button>

      {expanded && (
        <div id="reports-caveats-body" className="mt-[var(--space-2)] flex flex-col gap-[var(--space-1)]">
          <p className="m-0">
            <strong className="font-semibold">Indicators, not verdicts.</strong> Throughput and speed depend on task size,
            which TaskFlow doesn&rsquo;t record (no estimates or story points). Use these numbers to start a conversation,
            not to end one.
          </p>
          <p className="m-0">
            <strong className="font-semibold">Who gets credit.</strong> A finished task counts for whoever held it when it
            moved to Done — not whoever approved it. If someone took a task over, the person holding it at the end gets the
            credit; take-overs are shown separately.
          </p>
          <p className="m-0">
            <strong className="font-semibold">Reopened work.</strong> A reopened task stops counting as finished until it is
            finished again, then counts once — so earlier periods can change, including a period you&rsquo;ve already looked
            at.
          </p>
          <p className="m-0">
            <strong className="font-semibold">Time.</strong> Times are wall-clock in {timeZone}, including nights, weekends
            and holidays. Weeks start on Monday.
          </p>
          <p className="m-0">
            <strong className="font-semibold">Capacity.</strong> Utilisation uses today&rsquo;s limit of {inProgressLimit}; if
            the limit changed during this period, older figures are approximate.
          </p>
          <p className="m-0">
            <strong className="font-semibold">
              {dataAvailableSince === null ? 'No data recorded yet.' : `Data available since ${dateFormatter.format(new Date(`${dataAvailableSince}T00:00:00Z`))}.`}
            </strong>{' '}
            {dataAvailableSince !== null && 'Nothing before this date was recorded.'}
          </p>
          <p className="m-0 text-text-muted">
            Small samples: any figure marked &ldquo;Few data&rdquo; is based on fewer than {smallSampleThreshold} tasks — read
            it with care. A p85 needs at least {smallSampleThreshold} tasks; otherwise it shows as a dash.
          </p>
        </div>
      )}
    </div>
  )
}
