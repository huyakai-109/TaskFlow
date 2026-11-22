import { useState } from 'react'
import { Navigate, useLocation, useParams } from 'react-router-dom'

import { describeError } from '../../api/errorMessages'
import type { ReportTaskMetric } from '../../api/types'
import { useAuthSession } from '../../shared/hooks/useAuthSession'
import { useHasPermission } from '../../shared/hooks/useHasPermission'
import { useUsersQuery } from '../../shared/hooks/useUsersQuery'

import { CaveatsPanel } from './components/CaveatsPanel'
import { CycleTimeTrendChart } from './components/CycleTimeTrendChart'
import { DrillDownDialog } from './components/DrillDownDialog'
import { ExportMenu } from './components/ExportMenu'
import { MemberVsTeamChart } from './components/MemberVsTeamChart'
import { OnTimeStackedBarChart } from './components/OnTimeStackedBarChart'
import { ReportFilterBar } from './components/ReportFilterBar'
import { ScorecardGrid } from './components/ScorecardGrid'
import { WorkloadHeatmap } from './components/WorkloadHeatmap'
import { WorkloadReworkScatter } from './components/WorkloadReworkScatter'
import { useReportRange } from './hooks/useReportRange'
import { useReportScorecardQuery } from './hooks/useReportScorecardQuery'
import { useReportTeamQuery } from './hooks/useReportTeamQuery'

const granularityLabel: Record<string, string> = { Day: 'Daily', Week: 'Weekly', Month: 'Monthly', Quarter: 'Quarterly' }

interface DrillDownState {
  title: string
  userId?: number
  metric: ReportTaskMetric
  from: string
  to: string
}

/**
 * `docs/design/SPEC.md` §11 — the Reports page that replaces Activity.
 * Scope is the route itself (`/reports/me`, `/reports/team`,
 * `/reports/members/:userId`), never component state, so the URL is always
 * shareable (§11.1). `canViewReports` and `canExport` are presentation-only
 * gates on the permission string (`frontend.md` §9) — the server's own
 * `ViewReports`/`ExportReports` policies are the real boundary;
 * `RequirePermission` (router.tsx) already keeps a Member from reaching
 * `/reports/team` or someone else's `/reports/members/:id` at all.
 *
 * Team scope reads **two** endpoints: `GET /reports/scorecard` with no
 * `userId` (f09-reports.md endpoint 1 — "userId absent = team"), whose
 * `buckets[]` drive the (a) on-time/late and (b) cycle-time-trend charts
 * exactly like the member scope's own buckets (§11.2 point 5 lists both
 * charts "for the pooled team" too — there was never a contract gap, only a
 * missed call); and `GET /reports/team`, kept for the members table, pooled
 * team totals/teamMedian and the heatmap, none of which the scorecard
 * endpoint returns. `scorecardQuery.data.subject` is `null` at team scope
 * (documented on the contract) and is never read for it.
 */
export function ReportsPage() {
  const { userId: routeUserIdParam } = useParams<{ userId?: string }>()
  const location = useLocation()
  const { userId: ownUserId } = useAuthSession()
  const canViewReports = useHasPermission('ViewReports')
  const canExport = useHasPermission('ExportReports')
  const usersQuery = useUsersQuery()
  const range = useReportRange()
  const [drillDown, setDrillDown] = useState<DrillDownState | null>(null)

  const isTeamScope = location.pathname === '/reports/team'
  const routeUserId = routeUserIdParam !== undefined ? Number(routeUserIdParam) : undefined
  const isOwnIdInUrl = !isTeamScope && routeUserId !== undefined && ownUserId !== null && routeUserId === ownUserId
  const targetUserId = isTeamScope ? undefined : (routeUserId ?? ownUserId ?? undefined)

  // The scorecard query always runs — `userId` absent is itself the team
  // scope (f09 endpoint 1), so this is the one query that ever supplies
  // `buckets[]`, in either scope. The team-report query additionally runs
  // only at team scope, for the data the scorecard doesn't carry (roster,
  // pooled totals/teamMedian, heatmap).
  const scorecardQuery = useReportScorecardQuery({
    userId: targetUserId,
    from: range.from,
    to: range.to,
    granularity: range.granularity,
  })

  const teamQuery = useReportTeamQuery({ from: isTeamScope ? range.from : '', to: isTeamScope ? range.to : '' })

  if (isOwnIdInUrl) {
    return <Navigate to="/reports/me" replace />
  }

  // At team scope both queries must resolve before the page is "ready" —
  // the scorecard for the charts, the team report for everything else.
  const isPending = isTeamScope ? scorecardQuery.isPending || teamQuery.isPending : scorecardQuery.isPending
  const isError = isTeamScope ? scorecardQuery.isError || teamQuery.isError : scorecardQuery.isError
  const error = isTeamScope ? (scorecardQuery.error ?? teamQuery.error) : scorecardQuery.error
  const isSuccess = isTeamScope ? scorecardQuery.isSuccess && teamQuery.isSuccess : scorecardQuery.isSuccess
  const isFetching = isTeamScope ? scorecardQuery.isFetching || teamQuery.isFetching : scorecardQuery.isFetching

  function refetchAll() {
    void scorecardQuery.refetch()
    if (isTeamScope) {
      void teamQuery.refetch()
    }
  }

  const meta = isTeamScope ? teamQuery.data?.meta : scorecardQuery.data?.meta
  const subjectName = isTeamScope
    ? 'Team'
    : (scorecardQuery.data?.subject?.displayName ?? scorecardQuery.data?.subject?.userName ?? 'Me')

  function openDrillDown(title: string, metric: ReportTaskMetric, from: string, to: string, userId?: number) {
    setDrillDown({ title, metric, userId, from, to })
  }

  const buckets = scorecardQuery.data?.buckets ?? []
  const noHistory = meta !== undefined && meta.dataAvailableSince === null
  const rangeBeforeData = buckets.length > 0 && buckets.every((bucket) => !bucket.hasData)
  const memberNoActivity =
    !isTeamScope &&
    scorecardQuery.isSuccess &&
    scorecardQuery.data.totals.delivery.done === 0 &&
    scorecardQuery.data.totals.reliability.onTime + scorecardQuery.data.totals.reliability.late + scorecardQuery.data.totals.reliability.noDueDate === 0 &&
    scorecardQuery.data.totals.attribution.takeOvers === 0 &&
    scorecardQuery.data.totals.attribution.takenFrom === 0

  return (
    <section className="mx-auto flex w-full max-w-[1680px] flex-col gap-[var(--space-4)]">
      <div className="flex flex-wrap items-center justify-between gap-[var(--space-3)]">
        <div>
          <h1 className="m-0 text-[length:var(--font-size-xl)] font-semibold">Reports</h1>
          <p className="m-0 mt-[var(--space-1)] text-[length:var(--font-size-sm)] text-text-muted">
            {subjectName} · {range.from}–{range.to} · {isTeamScope ? 'Weekly' : (granularityLabel[range.granularity] ?? range.granularity)}
          </p>
        </div>
        {canExport && meta !== undefined && (
          <ExportMenu userId={targetUserId} from={range.from} to={range.to} granularity={range.granularity} isTeamScope={isTeamScope} />
        )}
      </div>

      <ReportFilterBar
        canViewReports={canViewReports}
        members={usersQuery.data ?? []}
        selectedUserId={targetUserId}
        ownUserId={ownUserId ?? 0}
        range={{ from: range.from, to: range.to }}
        today={range.today}
        granularity={range.granularity}
        isTeamScope={isTeamScope}
        dayGranularityAllowed={range.dayGranularityAllowed}
        onPresetSelect={range.setPreset}
        onCustomRange={range.setRange}
        onGranularityChange={range.setGranularity}
      />

      {meta !== undefined && (
        <CaveatsPanel
          timeZone={meta.timeZone}
          inProgressLimit={meta.inProgressLimit}
          dataAvailableSince={meta.dataAvailableSince}
          smallSampleThreshold={meta.smallSampleThreshold}
        />
      )}

      {isPending && (
        <div className="flex flex-col gap-[var(--space-3)]" aria-busy="true">
          <p role="status" className="text-[length:var(--font-size-sm)] text-text-muted">
            Loading report…
          </p>
          <div className="grid grid-cols-1 gap-[var(--space-3)] min-[768px]:grid-cols-2 min-[1440px]:grid-cols-4">
            {[0, 1, 2, 3].map((index) => (
              <div key={index} className="h-[160px] animate-pulse rounded-card-lg border border-border bg-column" />
            ))}
          </div>
        </div>
      )}

      {isError && (
        <div role="alert" className="flex flex-col items-start gap-[var(--space-3)] rounded-md border border-danger-border bg-danger-surface p-[var(--space-4)] text-danger">
          <p className="m-0">{describeError(error)}</p>
          <button
            type="button"
            className="min-h-9 cursor-pointer rounded-md border border-border bg-surface px-[var(--space-3)] text-text"
            onClick={refetchAll}
          >
            Try again
          </button>
        </div>
      )}

      {/* §11.6 "Refetch" — a filter change refetches with data already on screen; dimmed, not re-skeletoned. */}
      <div
        className="flex flex-col gap-[var(--space-4)] transition-opacity"
        aria-busy={isFetching && !isPending}
        style={isFetching && !isPending ? { opacity: 0.6 } : undefined}
      >
        {isSuccess && noHistory && (
          <p className="rounded-card-lg border border-border bg-surface p-[var(--space-4)] text-text-muted">
            No task history recorded yet. Reports appear once tasks start moving.
          </p>
        )}

        {isSuccess && !noHistory && rangeBeforeData && meta?.dataAvailableSince != null && (
          <div className="flex flex-col items-start gap-[var(--space-2)] rounded-card-lg border border-border bg-surface p-[var(--space-4)]">
            <p className="m-0 text-text-muted">No data in this range. Data is available from {meta.dataAvailableSince}.</p>
            <button
              type="button"
              className="cursor-pointer rounded-md border border-border bg-column px-[var(--space-3)] py-[var(--space-2)] text-text"
              onClick={() => range.setRange({ from: meta.dataAvailableSince!, to: range.to })}
            >
              Start from {meta.dataAvailableSince}
            </button>
          </div>
        )}

        {isSuccess && !noHistory && !rangeBeforeData && memberNoActivity && (
          <p className="rounded-card-lg border border-border bg-surface p-[var(--space-4)] text-text-muted">
            No recorded activity for {subjectName} in this period.
          </p>
        )}

        {isSuccess && !noHistory && !rangeBeforeData && !isTeamScope && scorecardQuery.data !== undefined && (
          <>
            <ScorecardGrid
              totals={scorecardQuery.data.totals}
              teamMedian={scorecardQuery.data.teamMedian}
              inProgressLimit={scorecardQuery.data.meta.inProgressLimit}
              smallSampleThreshold={scorecardQuery.data.meta.smallSampleThreshold}
            />

            <div className="grid grid-cols-1 gap-[var(--space-3)] min-[1440px]:grid-cols-2">
              <OnTimeStackedBarChart
                buckets={scorecardQuery.data.buckets}
                height={280}
                onViewTasks={(bucketStart, bucketEnd, metric) => {
                  openDrillDown(
                    `${metric === 'OnTime' ? 'On time' : 'Late'} · ${subjectName} · ${bucketStart}–${bucketEnd}`,
                    metric,
                    bucketStart,
                    bucketEnd,
                    targetUserId,
                  )
                }}
              />
              <CycleTimeTrendChart buckets={scorecardQuery.data.buckets} height={280} />
            </div>

            {/* §11.2 point 5 lists "You vs team median" for the member view only — never shown at team scope. */}
            {scorecardQuery.data.teamMedian !== null && (
              <MemberVsTeamChart totals={scorecardQuery.data.totals} teamMedian={scorecardQuery.data.teamMedian} height={280} />
            )}
          </>
        )}

        {isSuccess && !noHistory && !rangeBeforeData && isTeamScope && teamQuery.data !== undefined && scorecardQuery.data !== undefined && (
          <>
            <ScorecardGrid
              totals={teamQuery.data.team.totals}
              teamMedian={teamQuery.data.teamMedian}
              inProgressLimit={teamQuery.data.meta.inProgressLimit}
              smallSampleThreshold={teamQuery.data.meta.smallSampleThreshold}
            />

            <div className="grid grid-cols-1 gap-[var(--space-3)] min-[1440px]:grid-cols-2">
              <OnTimeStackedBarChart
                buckets={scorecardQuery.data.buckets}
                height={280}
                onViewTasks={(bucketStart, bucketEnd, metric) => {
                  openDrillDown(
                    `${metric === 'OnTime' ? 'On time' : 'Late'} · Team · ${bucketStart}–${bucketEnd}`,
                    metric,
                    bucketStart,
                    bucketEnd,
                    undefined,
                  )
                }}
              />
              <CycleTimeTrendChart buckets={scorecardQuery.data.buckets} height={280} />
            </div>

            <WorkloadHeatmap heatmap={teamQuery.data.heatmap} members={teamQuery.data.members} inProgressLimit={teamQuery.data.meta.inProgressLimit} />

            <WorkloadReworkScatter members={teamQuery.data.members} height={280} />
          </>
        )}
      </div>

      {drillDown !== null && (
        <DrillDownDialog
          title={drillDown.title}
          userId={drillDown.userId}
          from={drillDown.from}
          to={drillDown.to}
          metric={drillDown.metric}
          onClose={() => setDrillDown(null)}
        />
      )}
    </section>
  )
}
