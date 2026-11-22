const dateFormatter = new Intl.DateTimeFormat('en', { day: 'numeric', month: 'short', year: 'numeric' })

interface CaveatsPanelProps {
  timeZone: string
  inProgressLimit: number
  dataAvailableSince: string | null
  smallSampleThreshold: number
}

/**
 * §11.5 caveats, copied verbatim (the reopened-work line especially — "can
 * change, including a period you've already looked at"). Always visible,
 * never collapsible: the brief's whole point is that nobody reads a report
 * number as a verdict without seeing this first.
 */
export function CaveatsPanel({ timeZone, inProgressLimit, dataAvailableSince, smallSampleThreshold }: CaveatsPanelProps) {
  return (
    <div className="flex flex-col gap-[var(--space-2)] rounded-lg bg-side-panel p-[var(--space-4)] text-[13px] text-text-2">
      <p className="m-0">
        <strong className="font-semibold">Indicators, not verdicts.</strong> Throughput and speed depend on task size, which
        TaskFlow doesn&rsquo;t record (no estimates or story points). Use these numbers to start a conversation, not to end
        one.
      </p>
      <p className="m-0">
        <strong className="font-semibold">Who gets credit.</strong> A finished task counts for whoever held it when it moved
        to Done — not whoever approved it. If someone took a task over, the person holding it at the end gets the credit;
        take-overs are shown separately.
      </p>
      <p className="m-0">
        <strong className="font-semibold">Reopened work.</strong> A reopened task stops counting as finished until it is
        finished again, then counts once — so earlier periods can change, including a period you&rsquo;ve already looked at.
      </p>
      <p className="m-0">
        <strong className="font-semibold">Time.</strong> Times are wall-clock in {timeZone}, including nights, weekends and
        holidays. Weeks start on Monday.
      </p>
      <p className="m-0">
        <strong className="font-semibold">Capacity.</strong> Utilisation uses today&rsquo;s limit of {inProgressLimit}; if the
        limit changed during this period, older figures are approximate.
      </p>
      <p className="m-0">
        <strong className="font-semibold">
          {dataAvailableSince === null ? 'No data recorded yet.' : `Data available since ${dateFormatter.format(new Date(`${dataAvailableSince}T00:00:00Z`))}.`}
        </strong>{' '}
        {dataAvailableSince !== null && 'Nothing before this date was recorded.'}
      </p>
      <p className="m-0 text-text-muted">
        Small samples: any figure marked &ldquo;Few data&rdquo; is based on fewer than {smallSampleThreshold} tasks — read it
        with care. A p85 needs at least {smallSampleThreshold} tasks; otherwise it shows as a dash.
      </p>
    </div>
  )
}
