import { useState } from 'react'
import { Navigate, useLocation, useParams } from 'react-router-dom'

import { describeError } from '../../api/errorMessages'
import type { ReportTaskMetric, ReportTeam } from '../../api/types'
import { useAuthSession } from '../../shared/hooks/useAuthSession'
import { useHasPermission } from '../../shared/hooks/useHasPermission'
import { useUsersQuery } from '../../shared/hooks/useUsersQuery'

import { CaveatsPanel } from './components/CaveatsPanel'
import { DrillDownDialog } from './components/DrillDownDialog'
import { ExportMenu } from './components/ExportMenu'
import { OnTimeStackedBarChart } from './components/OnTimeStackedBarChart'
import { ReportFilterBar } from './components/ReportFilterBar'
import { ScorecardGrid } from './components/ScorecardGrid'
import type { ScorecardTeamComparison } from './components/ScorecardGrid'
import { TeamComparisonChart } from './components/TeamComparisonChart'
import { TicketsByPriorityChart } from './components/TicketsByPriorityChart'
import { WorkloadHeatmap } from './components/WorkloadHeatmap'
import { WorkloadReworkScatter } from './components/WorkloadReworkScatter'
import { useReportRange } from './hooks/useReportRange'
import { useReportScorecardQuery } from './hooks/useReportScorecardQuery'
import { useReportTeamQuery } from './hooks/useReportTeamQuery'
import { percentVsTeam } from './utils/reportComparison'

const granularityLabel: Record<string, string> = { Day: 'Daily', Week: 'Weekly', Month: 'Monthly', Quarter: 'Quarterly' }

interface DrillDownState {
  title: string
  userId?: number
  metric: ReportTaskMetric
  from: string
  to: string
}

/** §E — count of roster members with `ticketsWorked >= 1` (the equivalent of R12's `contributors`, which the contract only defines for `done`). */
function ticketsContributors(members: ReportTeam['members']): number {
  return members.filter((member) => (member.totals.load?.ticketsWorked ?? 0) >= 1).length
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
 * `buckets[]` drive the on-time/late chart exactly like the member scope's
 * own buckets; and `GET /reports/team`, kept for the members table, pooled
 * team totals/teamMedian and the heatmap, none of which the scorecard
 * endpoint returns. A member scope with `ViewReports` also reads
 * `GET /reports/team` a second time (same range) purely for the "% vs team
 * average" comparison (§E) — fetched opportunistically, never gating the
 * page's own loading/error state, since the scorecard alone is enough to
 * show the report.
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
  // `buckets[]`, in either scope.
  const scorecardQuery = useReportScorecardQuery({
    userId: targetUserId,
    from: range.from,
    to: range.to,
    granularity: range.granularity,
  })

  // Runs at team scope (for the roster/heatmap/pooled totals) and, with
  // `ViewReports`, at member scope too (for the "% vs team" comparison) —
  // never for a Member without the permission (§E: "none for a Member
  // without ViewReports").
  const teamQuery = useReportTeamQuery({ from: canViewReports ? range.from : '', to: canViewReports ? range.to : '' })

  if (isOwnIdInUrl) {
    return <Navigate to="/reports/me" replace />
  }

  // At team scope both queries must resolve before the page is "ready" —
  // the scorecard for the charts, the team report for everything else. At
  // member scope the team query is opportunistic only (see above).
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

  const teamComparison: ScorecardTeamComparison | null =
    !isTeamScope && canViewReports && scorecardQuery.data !== undefined && teamQuery.data !== undefined
      ? {
          done: percentVsTeam(scorecardQuery.data.totals.delivery.done, teamQuery.data.team.totals.delivery.done, teamQuery.data.team.contributors),
          ticketsWorked: percentVsTeam(
            scorecardQuery.data.totals.load?.ticketsWorked ?? 0,
            teamQuery.data.team.totals.load?.ticketsWorked ?? 0,
            ticketsContributors(teamQuery.data.members),
          ),
        }
      : null

  return (
    <section className="mx-auto flex w-full max-w-[1680px] flex-col gap-[var(--space-4)]">
      <div className="flex flex-wrap items-center justify-between gap-[var(--space-3)]">
        <div>
          <h1 className="m-0 text-[length:var(--font-size-xl)] font-semibold">Reports</h1>
          <p className="m-0 mt-[var(--space-1)] text-[length:var(--font-size-sm)] text-text-muted">
            {subjectName} · {range.from}–{range.to} · {granularityLabel[range.granularity] ?? range.granularity}
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
        dataAvailableSince={meta?.dataAvailableSince ?? null}
        generatedAt={meta?.generatedAt ?? null}
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
              smallSampleThreshold={scorecardQuery.data.meta.smallSampleThreshold}
              teamComparison={teamComparison}
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
              <TicketsByPriorityChart
                urgent={scorecardQuery.data.totals.delivery.byPriority.urgent}
                high={scorecardQuery.data.totals.delivery.byPriority.high}
                normal={scorecardQuery.data.totals.delivery.byPriority.normal}
                height={280}
              />
            </div>

            {/* §E — a team's own report has no single "you" to compare, so this never renders at team scope; a Member without ViewReports never gets `teamComparison` at all. */}
            {teamComparison !== null && (
              <>
                <TeamComparisonChart done={teamComparison.done} ticketsWorked={teamComparison.ticketsWorked} height={220} />
                <p className="m-0 text-[length:var(--font-size-2xs)] text-text-muted">
                  Compared with the average member who delivered in this period.
                </p>
              </>
            )}
          </>
        )}

        {isSuccess && !noHistory && !rangeBeforeData && isTeamScope && teamQuery.data !== undefined && scorecardQuery.data !== undefined && (
          <>
            <ScorecardGrid
              totals={teamQuery.data.team.totals}
              teamMedian={teamQuery.data.teamMedian}
              smallSampleThreshold={teamQuery.data.meta.smallSampleThreshold}
              teamComparison={null}
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
              <TicketsByPriorityChart
                urgent={teamQuery.data.team.totals.delivery.byPriority.urgent}
                high={teamQuery.data.team.totals.delivery.byPriority.high}
                normal={teamQuery.data.team.totals.delivery.byPriority.normal}
                height={280}
              />
            </div>

            <WorkloadHeatmap heatmap={teamQuery.data.heatmap} members={teamQuery.data.members} />

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
