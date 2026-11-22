import { fetchAuthenticatedBlob, request } from './client'
import type {
  PaginationResultRes,
  ReportExportDataset,
  ReportGranularity,
  ReportScorecard,
  ReportTask,
  ReportTaskMetric,
  ReportTeam,
  ResultRes,
} from './types'

/**
 * `docs/features/f09-reports.md` "Technical contract" — the one module that
 * talks to `reports/*`, mirroring `api/workflow.ts`'s shape (unwrap the
 * envelope, throw on a missing `result`).
 */

export interface ReportRangeParams {
  from: string
  to: string
}

export interface ScorecardParams extends ReportRangeParams {
  /** Absent = team scope (R1 — only reachable with `ViewReports`; the service itself enforces `SelfOnly` otherwise). */
  userId?: number
  granularity: ReportGranularity
}

export async function getScorecard(params: ScorecardParams, signal?: AbortSignal): Promise<ReportScorecard> {
  const query = new URLSearchParams({ from: params.from, to: params.to, granularity: params.granularity })

  if (params.userId !== undefined) {
    query.set('userId', String(params.userId))
  }

  const envelope = await request<ResultRes<ReportScorecard>>(`/reports/scorecard?${query.toString()}`, { signal })

  if (envelope.result == null) {
    throw new Error('GET /reports/scorecard returned no result')
  }

  return envelope.result
}

export async function getTeamReport(params: ReportRangeParams, signal?: AbortSignal): Promise<ReportTeam> {
  const query = new URLSearchParams({ from: params.from, to: params.to })
  const envelope = await request<ResultRes<ReportTeam>>(`/reports/team?${query.toString()}`, { signal })

  if (envelope.result == null) {
    throw new Error('GET /reports/team returned no result')
  }

  return envelope.result
}

export interface ReportTasksParams extends ReportRangeParams {
  userId?: number
  metric: ReportTaskMetric
}

export async function getReportTasks(
  params: ReportTasksParams,
  start: number,
  length: number,
  signal?: AbortSignal,
): Promise<PaginationResultRes<ReportTask[]>> {
  const query = new URLSearchParams({
    from: params.from,
    to: params.to,
    metric: params.metric,
    start: String(start),
    length: String(length),
  })

  if (params.userId !== undefined) {
    query.set('userId', String(params.userId))
  }

  return request<PaginationResultRes<ReportTask[]>>(`/reports/tasks?${query.toString()}`, { signal })
}

export interface ExportReportParams extends ReportRangeParams {
  userId?: number
  granularity: ReportGranularity
  dataset: ReportExportDataset
}

/**
 * `GET /reports/export` (`frontend.md` §3 — the shared `fetchAuthenticatedBlob`
 * pattern, not a bare `fetch`). Filename taken from the server's own
 * `Content-Disposition` rather than reconstructed client-side, since the
 * contract already sanitises and builds it server-side.
 */
export async function exportReport(params: ExportReportParams, signal?: AbortSignal): Promise<{ blob: Blob; filename: string }> {
  const query = new URLSearchParams({
    from: params.from,
    to: params.to,
    granularity: params.granularity,
    dataset: params.dataset,
  })

  if (params.userId !== undefined) {
    query.set('userId', String(params.userId))
  }

  const response = await fetchAuthenticatedBlob(`/api/v1/reports/export?${query.toString()}`, signal)
  const blob = await response.blob()
  const disposition = response.headers.get('Content-Disposition') ?? ''
  const match = /filename="?([^";]+)"?/.exec(disposition)
  const filename = match?.[1] ?? 'taskflow-report.csv'

  return { blob, filename }
}
