import type { ReactNode } from 'react'

import { PriorityIcon } from '../../../shared/components/PriorityIcon'
import type { ReportMetricSet, ReportTeamMedian } from '../../../api/types'
import { formatDuration } from '../utils/formatDuration'
import { formatRate } from '../utils/formatRate'
import { formatComparisonPercent } from '../utils/reportComparison'
import type { TeamComparison } from '../utils/reportComparison'
import { summarizeDelivery, summarizeQuality, summarizeReliability, summarizeSpeed } from '../utils/scorecardSummary'

import { LowSampleChip } from './LowSampleChip'

/** §E — only for `done` and `ticketsWorked` (count metrics); `null` at team scope or for a caller without `ViewReports`. */
export interface ScorecardTeamComparison {
  done: TeamComparison | null
  ticketsWorked: TeamComparison | null
}

interface ScorecardGridProps {
  totals: ReportMetricSet
  /** `null` for team scope, or when the caller has no `ViewReports` (f09 Q3) — no "Team median" line is shown then. */
  teamMedian: ReportTeamMedian | null
  smallSampleThreshold: number
  teamComparison?: ScorecardTeamComparison | null
}

function ComparisonBadge({ comparison }: { comparison: TeamComparison | null | undefined }) {
  if (comparison == null || comparison.percent === null) {
    return null
  }

  const { percent, direction } = comparison
  const colourClass = direction === 'above' ? 'text-accent-ink' : direction === 'below' ? 'text-danger' : 'text-text-muted'
  const arrow = direction === 'above' ? '▲' : direction === 'below' ? '▼' : '●'

  return (
    <span className={`inline-flex items-center gap-[2px] text-[length:var(--font-size-xs)] font-medium ${colourClass}`}>
      <span aria-hidden="true">{arrow}</span>
      {formatComparisonPercent(percent)} vs team
    </span>
  )
}

function Card({ label, children }: { label: string; children: ReactNode }) {
  return (
    <div className="flex flex-col gap-[var(--space-2)] rounded-card-lg border border-border bg-surface p-[var(--space-4)]">
      <h2 className="m-0 text-[length:var(--font-size-2xs)] font-semibold tracking-[0.5px] text-text-muted uppercase">{label}</h2>
      {children}
    </div>
  )
}

function Headline({ value, caption, summary }: { value: string; caption: string; summary: string }) {
  return (
    <div className="flex flex-col gap-[2px]">
      <span className="flex items-baseline gap-[var(--space-2)]">
        <span className="text-[length:var(--font-size-display)] font-semibold text-text">{value}</span>
        <span className="text-[length:var(--font-size-xs)] text-text-muted">{caption}</span>
      </span>
      <span className="text-[length:var(--font-size-sm)] text-text-2">{summary}</span>
    </div>
  )
}

function Metric({
  label,
  value,
  caption,
  lowSample,
  sampleSize,
  smallSampleThreshold,
}: {
  label: string
  value: string
  caption?: string
  lowSample?: boolean
  sampleSize?: number
  smallSampleThreshold?: number
}) {
  return (
    <div className="flex flex-col gap-[1px]">
      <span className="text-[length:var(--font-size-xs)] text-text-muted">{label}</span>
      <span className="flex items-center gap-[var(--space-2)] text-[length:var(--font-size-base)] font-medium text-text">
        {value}
        {lowSample === true && sampleSize !== undefined && smallSampleThreshold !== undefined && (
          <LowSampleChip sampleSize={sampleSize} smallSampleThreshold={smallSampleThreshold} />
        )}
      </span>
      {caption !== undefined && <span className="text-[length:var(--font-size-2xs)] text-text-muted">{caption}</span>}
    </div>
  )
}

/**
 * §C/§D/§E (2026-11-22 brief) — four cards grouped under small uppercase
 * section labels, each with a headline number, a one-line plain-English
 * summary (`utils/scorecardSummary.ts`), and the supporting figures below.
 * No colour judgement beyond the one explicit "vs team" comparison badge
 * (§E — teal above, muted red below, an arrow icon so it never relies on
 * colour alone); no composite number anywhere.
 */
export function ScorecardGrid({ totals, teamMedian, smallSampleThreshold, teamComparison }: ScorecardGridProps) {
  const onTimeDenominator = totals.reliability.onTime + totals.reliability.late
  const hasPriorityBreakdown = totals.delivery.byPriority.urgent + totals.delivery.byPriority.high + totals.delivery.byPriority.normal > 0

  return (
    <div className="grid grid-cols-1 gap-[var(--space-3)] min-[768px]:grid-cols-2 min-[1440px]:grid-cols-4">
      <Card label="Delivery">
        <Headline value={String(totals.delivery.done)} caption="tasks" summary={summarizeDelivery(totals.delivery.done)} />
        <ComparisonBadge comparison={teamComparison?.done} />
        {hasPriorityBreakdown && (
          <div className="flex flex-col gap-[2px] text-[length:var(--font-size-sm)] text-text-2">
            <span className="flex items-center gap-[var(--space-1)]">
              <PriorityIcon priority="Urgent" /> Urgent {totals.delivery.byPriority.urgent}
            </span>
            <span className="flex items-center gap-[var(--space-1)]">
              <PriorityIcon priority="High" /> High {totals.delivery.byPriority.high}
            </span>
            <span className="flex items-center gap-[var(--space-1)]">
              <PriorityIcon priority="Normal" /> Normal {totals.delivery.byPriority.normal}
            </span>
          </div>
        )}
        {teamMedian?.done != null && <span className="text-[length:var(--font-size-2xs)] text-text-muted">Team median {teamMedian.done}</span>}
      </Card>

      <Card label="Speed">
        <Headline
          value={formatDuration(totals.speed.cycleTime.medianSeconds)}
          caption="typical cycle time"
          summary={summarizeSpeed(totals.speed.cycleTime)}
        />
        <Metric
          label="Cycle time — Typical / Most within"
          value={`${formatDuration(totals.speed.cycleTime.medianSeconds)} / ${formatDuration(totals.speed.cycleTime.p85Seconds)}`}
          lowSample={totals.speed.cycleTime.lowSample}
          sampleSize={totals.speed.cycleTime.sampleSize}
          smallSampleThreshold={smallSampleThreshold}
        />
        <Metric
          label="Lead time — Typical / Most within"
          value={`${formatDuration(totals.speed.leadTime.medianSeconds)} / ${formatDuration(totals.speed.leadTime.p85Seconds)}`}
          lowSample={totals.speed.leadTime.lowSample}
          sampleSize={totals.speed.leadTime.sampleSize}
          smallSampleThreshold={smallSampleThreshold}
        />
        <Metric
          label="Review wait — Typical / Most within"
          value={`${formatDuration(totals.speed.reviewWait.medianSeconds)} / ${formatDuration(totals.speed.reviewWait.p85Seconds)}`}
          lowSample={totals.speed.reviewWait.lowSample}
          sampleSize={totals.speed.reviewWait.sampleSize}
          smallSampleThreshold={smallSampleThreshold}
          caption="Time in the final review pass — not counted against cycle time. Cycle time itself can include review wait."
        />
        {teamMedian?.cycleTimeMedianSeconds != null && (
          <span className="text-[length:var(--font-size-2xs)] text-text-muted">Team median {formatDuration(teamMedian.cycleTimeMedianSeconds)}</span>
        )}
      </Card>

      <Card label="Reliability">
        <Headline
          value={formatRate(totals.reliability.onTimeRate.value)}
          caption="on time"
          summary={summarizeReliability(totals.reliability.onTime, totals.reliability.late)}
        />
        <Metric label="On time" value={`${totals.reliability.onTime} of ${onTimeDenominator}`} lowSample={totals.reliability.onTimeRate.lowSample} sampleSize={totals.reliability.onTimeRate.denominator} smallSampleThreshold={smallSampleThreshold} />
        <Metric label="Late" value={String(totals.reliability.late)} />
        <Metric label="No due date" value={String(totals.reliability.noDueDate)} />
        <Metric label="Overdue marks" value={String(totals.reliability.overdueMarked)} />
        {teamMedian?.onTimeRate != null && <span className="text-[length:var(--font-size-2xs)] text-text-muted">Team median {formatRate(teamMedian.onTimeRate)}</span>}
      </Card>

      <Card label="Quality & load">
        <Headline
          value={totals.load === null ? '—' : String(totals.load.ticketsWorked)}
          caption="tickets"
          summary={summarizeQuality(totals.load?.ticketsWorked ?? 0, totals.quality.sentBack, totals.quality.submissionsDecided, totals.quality.reworkRate.lowSample)}
        />
        <ComparisonBadge comparison={teamComparison?.ticketsWorked} />
        <Metric
          label="Rework rate"
          value={`${formatRate(totals.quality.reworkRate.value)} · ${totals.quality.sentBack} of ${totals.quality.submissionsDecided} sent back`}
          lowSample={totals.quality.reworkRate.lowSample}
          sampleSize={totals.quality.reworkRate.denominator}
          smallSampleThreshold={smallSampleThreshold}
        />
        <Metric label="Reopened after Done" value={String(totals.quality.reopenedAfterDone)} />
        <Metric label="Take-overs" value={`${totals.attribution.takeOvers} taken · ${totals.attribution.takenFrom} lost`} />
        {teamMedian?.reworkRate != null && <span className="text-[length:var(--font-size-2xs)] text-text-muted">Team median {formatRate(teamMedian.reworkRate)}</span>}
      </Card>
    </div>
  )
}
