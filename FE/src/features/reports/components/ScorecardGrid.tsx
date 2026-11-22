import type { ReactNode } from 'react'

import { PriorityIcon } from '../../../shared/components/PriorityIcon'
import type { ReportMetricSet, ReportTeamMedian } from '../../../api/types'
import { formatDuration } from '../utils/formatDuration'
import { formatRate } from '../utils/formatRate'

import { LowSampleChip } from './LowSampleChip'

interface ScorecardGridProps {
  totals: ReportMetricSet
  /** `null` for team scope, or when the caller has no `ViewReports` (f09 Q3) — no "Team median" line is shown then. */
  teamMedian: ReportTeamMedian | null
  inProgressLimit: number
  smallSampleThreshold: number
}

function Card({ title, children }: { title: string; children: ReactNode }) {
  return (
    <div className="flex flex-col gap-[var(--space-3)] rounded-card-lg border border-border bg-surface p-[var(--space-4)]">
      <h2 className="m-0 text-[length:var(--font-size-md)] font-semibold text-text">{title}</h2>
      {children}
    </div>
  )
}

function Metric({
  label,
  value,
  teamValue,
  caption,
  lowSample,
  sampleSize,
  smallSampleThreshold,
}: {
  label: string
  value: string
  teamValue?: string | null
  caption?: string
  lowSample?: boolean
  sampleSize?: number
  smallSampleThreshold?: number
}) {
  return (
    <div className="flex flex-col gap-[2px]">
      <span className="text-[length:var(--font-size-xs)] text-text-muted">{label}</span>
      <span className="flex items-center gap-[var(--space-2)] text-[length:var(--font-size-lg)] font-semibold text-text">
        {value}
        {lowSample === true && sampleSize !== undefined && smallSampleThreshold !== undefined && (
          <LowSampleChip sampleSize={sampleSize} smallSampleThreshold={smallSampleThreshold} />
        )}
      </span>
      {caption !== undefined && <span className="text-[length:var(--font-size-2xs)] text-text-muted">{caption}</span>}
      {teamValue != null && <span className="text-[length:var(--font-size-2xs)] text-text-muted">Team median {teamValue}</span>}
    </div>
  )
}

/**
 * §11.2 point 4 — four cards, no colour judgement (no red/green deltas, no
 * arrows, no "above/below" words) and no composite number anywhere. The
 * member view's "Team median {x}" line only renders when `teamMedian` is
 * present (team scope, and callers without `ViewReports`, never get one).
 */
export function ScorecardGrid({ totals, teamMedian, inProgressLimit, smallSampleThreshold }: ScorecardGridProps) {
  const onTimeDenominator = totals.reliability.onTime + totals.reliability.late

  return (
    <div className="grid grid-cols-1 gap-[var(--space-3)] min-[768px]:grid-cols-2 min-[1440px]:grid-cols-4">
      <Card title="Delivery">
        <Metric label="Done" value={String(totals.delivery.done)} teamValue={teamMedian?.done != null ? String(teamMedian.done) : null} />
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
      </Card>

      <Card title="Speed">
        <Metric
          label="Cycle time — median / p85"
          value={`${formatDuration(totals.speed.cycleTime.medianSeconds)} / ${formatDuration(totals.speed.cycleTime.p85Seconds)}`}
          teamValue={teamMedian?.cycleTimeMedianSeconds != null ? formatDuration(teamMedian.cycleTimeMedianSeconds) : null}
          lowSample={totals.speed.cycleTime.lowSample}
          sampleSize={totals.speed.cycleTime.sampleSize}
          smallSampleThreshold={smallSampleThreshold}
        />
        <Metric
          label="Lead time — median / p85"
          value={`${formatDuration(totals.speed.leadTime.medianSeconds)} / ${formatDuration(totals.speed.leadTime.p85Seconds)}`}
          teamValue={teamMedian?.leadTimeMedianSeconds != null ? formatDuration(teamMedian.leadTimeMedianSeconds) : null}
          lowSample={totals.speed.leadTime.lowSample}
          sampleSize={totals.speed.leadTime.sampleSize}
          smallSampleThreshold={smallSampleThreshold}
        />
        <Metric
          label="Review wait — median / p85"
          value={`${formatDuration(totals.speed.reviewWait.medianSeconds)} / ${formatDuration(totals.speed.reviewWait.p85Seconds)}`}
          teamValue={teamMedian?.reviewWaitMedianSeconds != null ? formatDuration(teamMedian.reviewWaitMedianSeconds) : null}
          lowSample={totals.speed.reviewWait.lowSample}
          sampleSize={totals.speed.reviewWait.sampleSize}
          smallSampleThreshold={smallSampleThreshold}
          caption="Time in the final review pass — not counted against cycle time"
        />
      </Card>

      <Card title="Reliability">
        <Metric
          label="On-time rate"
          value={`${formatRate(totals.reliability.onTimeRate.value)} · ${totals.reliability.onTime} of ${onTimeDenominator} on time`}
          teamValue={teamMedian?.onTimeRate != null ? formatRate(teamMedian.onTimeRate) : null}
          lowSample={totals.reliability.onTimeRate.lowSample}
          sampleSize={totals.reliability.onTimeRate.denominator}
          smallSampleThreshold={smallSampleThreshold}
        />
        <Metric label="Late" value={String(totals.reliability.late)} />
        <Metric label="No due date" value={String(totals.reliability.noDueDate)} />
        <Metric
          label="Overdue marks"
          value={String(totals.reliability.overdueMarked)}
          teamValue={teamMedian?.overdueMarked != null ? String(teamMedian.overdueMarked) : null}
        />
      </Card>

      <Card title="Quality & load">
        <Metric
          label="Rework rate"
          value={`${formatRate(totals.quality.reworkRate.value)} · ${totals.quality.sentBack} of ${totals.quality.submissionsDecided} sent back`}
          teamValue={teamMedian?.reworkRate != null ? formatRate(teamMedian.reworkRate) : null}
          lowSample={totals.quality.reworkRate.lowSample}
          sampleSize={totals.quality.reworkRate.denominator}
          smallSampleThreshold={smallSampleThreshold}
        />
        <Metric label="Reopened after Done" value={String(totals.quality.reopenedAfterDone)} />
        <Metric
          label="Average in progress"
          value={totals.load === null ? '—' : totals.load.avgInProgress.toFixed(1)}
          teamValue={teamMedian?.avgInProgress != null ? teamMedian.avgInProgress.toFixed(1) : null}
        />
        <Metric
          label="Capacity utilisation"
          value={totals.load === null ? '—' : `${formatRate(totals.load.capacityUtilization)} of ${inProgressLimit}`}
          teamValue={teamMedian?.capacityUtilization != null ? formatRate(teamMedian.capacityUtilization) : null}
        />
        <Metric
          label="Take-overs"
          value={`${totals.attribution.takeOvers} taken · ${totals.attribution.takenFrom} lost`}
        />
      </Card>
    </div>
  )
}
