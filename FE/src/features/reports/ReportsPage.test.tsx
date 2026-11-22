import { QueryClient, QueryClientProvider } from '@tanstack/react-query'
import { cleanup, render, screen } from '@testing-library/react'
import { MemoryRouter, Route, Routes } from 'react-router-dom'
import { afterEach, describe, expect, it, vi } from 'vitest'

import { ApiError } from '../../api/client'
import type { ReportMetricSet, ReportScorecard, ReportTeam, ReportTeamMedian } from '../../api/types'

import { ReportsPage } from './ReportsPage'

const {
  useHasPermission,
  useAuthSession,
  useUsersQuery,
  useWorkflowQuery,
  useReportScorecardQuery,
  useReportTeamQuery,
} = vi.hoisted(() => ({
  useHasPermission: vi.fn(),
  useAuthSession: vi.fn(),
  useUsersQuery: vi.fn(),
  useWorkflowQuery: vi.fn(),
  useReportScorecardQuery: vi.fn(),
  useReportTeamQuery: vi.fn(),
}))

vi.mock('../../shared/hooks/useHasPermission', () => ({ useHasPermission }))
vi.mock('../../shared/hooks/useAuthSession', () => ({ useAuthSession }))
vi.mock('../../shared/hooks/useUsersQuery', () => ({ useUsersQuery }))
vi.mock('../../shared/hooks/useWorkflowQuery', () => ({ useWorkflowQuery }))
vi.mock('./hooks/useReportScorecardQuery', () => ({ useReportScorecardQuery }))
vi.mock('./hooks/useReportTeamQuery', () => ({ useReportTeamQuery }))

afterEach(() => {
  cleanup()
  vi.clearAllMocks()
})

function emptyTotals(): ReportMetricSet {
  return {
    delivery: { done: 0, byPriority: { urgent: 0, high: 0, normal: 0 } },
    speed: {
      cycleTime: { sampleSize: 0, medianSeconds: null, p85Seconds: null, lowSample: false },
      leadTime: { sampleSize: 0, medianSeconds: null, p85Seconds: null, lowSample: false },
      reviewWait: { sampleSize: 0, medianSeconds: null, p85Seconds: null, lowSample: false },
    },
    reliability: { onTime: 0, late: 0, noDueDate: 0, onTimeRate: { value: null, denominator: 0, lowSample: false }, overdueMarked: 0 },
    quality: { submissionsDecided: 0, sentBack: 0, reworkRate: { value: null, denominator: 0, lowSample: false }, reopenedAfterDone: 0 },
    load: null,
    attribution: { takeOvers: 0, takenFrom: 0 },
  }
}

function emptyTeamMedian(): ReportTeamMedian {
  return {
    contributors: 0,
    done: null,
    cycleTimeMedianSeconds: null,
    leadTimeMedianSeconds: null,
    reviewWaitMedianSeconds: null,
    onTimeRate: null,
    overdueMarked: null,
    reworkRate: null,
  }
}

function baseMeta() {
  return {
    scope: 'Member' as const,
    userId: 1,
    from: '2026-03-02',
    to: '2026-03-08',
    granularity: 'Week' as const,
    timeZone: 'Asia/Ho_Chi_Minh',
    weekStartsOn: 'Monday' as const,
    dataAvailableSince: '2026-02-20',
    generatedAt: '2026-11-21T03:00:00+00:00',
    smallSampleThreshold: 5,
    inProgressLimit: 3,
  }
}

function fixtureScorecard(overrides: Partial<ReportScorecard> = {}): ReportScorecard {
  return {
    meta: baseMeta(),
    subject: { id: 1, userName: 'member1', displayName: 'Lan', avatarUrl: null, isActive: true },
    totals: emptyTotals(),
    teamMedian: null,
    buckets: [{ start: '2026-03-02', end: '2026-03-08', isPartial: false, hasData: true, metrics: emptyTotals() }],
    ...overrides,
  }
}

function fixtureTeam(overrides: Partial<ReportTeam> = {}): ReportTeam {
  return {
    meta: { ...baseMeta(), scope: 'Team', userId: null },
    team: { totals: emptyTotals(), contributors: 0, unassignedOverdueMarked: 0 },
    teamMedian: emptyTeamMedian(),
    members: [],
    heatmap: { weeks: [], rows: [] },
    ...overrides,
  }
}

function setCommonMocks({ viewReports, exportReports }: { viewReports: boolean; exportReports: boolean }) {
  useHasPermission.mockImplementation((permission: string) => (permission === 'ViewReports' ? viewReports : permission === 'ExportReports' ? exportReports : false))
  useAuthSession.mockReturnValue({ userId: 1 })
  useUsersQuery.mockReturnValue({ data: [{ id: 1, userName: 'member1', displayName: 'Lan', role: 'Member', avatarUrl: null, isActive: true }] })
  useWorkflowQuery.mockReturnValue({ data: { maxInProgressPerAssignee: 3, timeZone: 'Asia/Ho_Chi_Minh', today: '2026-03-11', overdueScan: { lastFiredAt: null, nextFireAt: null } } })
}

function renderAt(path: string) {
  const queryClient = new QueryClient()
  render(
    <QueryClientProvider client={queryClient}>
      <MemoryRouter initialEntries={[path]}>
        <Routes>
          <Route path="/reports/me" element={<ReportsPage />} />
          <Route path="/reports/team" element={<ReportsPage />} />
          <Route path="/reports/members/:userId" element={<ReportsPage />} />
        </Routes>
      </MemoryRouter>
    </QueryClientProvider>,
  )
}


describe('ReportsPage — permission gating (Member vs Manager), per RequirePermission.test.tsx pattern', () => {
  it('Member: no scope picker, no Export button, no team-median line', () => {
    setCommonMocks({ viewReports: false, exportReports: false })
    useReportScorecardQuery.mockReturnValue({ isPending: false, isError: false, isSuccess: true, isFetching: false, data: fixtureScorecard(), refetch: vi.fn() })
    useReportTeamQuery.mockReturnValue({ isPending: true, isError: false, isSuccess: false, isFetching: true, data: undefined, refetch: vi.fn() })

    renderAt('/reports/me')

    expect(screen.queryByLabelText('Scope')).toBeNull()
    expect(screen.queryByRole('button', { name: /export csv/i })).toBeNull()
    expect(screen.queryByText(/Team median/)).toBeNull()
  })

  it('Manager/Admin: scope picker and Export button are visible on the team scope', () => {
    setCommonMocks({ viewReports: true, exportReports: true })
    useReportTeamQuery.mockReturnValue({ isPending: false, isError: false, isSuccess: true, isFetching: false, data: fixtureTeam(), refetch: vi.fn() })
    useReportScorecardQuery.mockReturnValue({
      isPending: false,
      isError: false,
      isSuccess: true,
      isFetching: false,
      data: fixtureScorecard({ meta: { ...baseMeta(), scope: 'Team', userId: null }, subject: null, teamMedian: null }),
      refetch: vi.fn(),
    })

    renderAt('/reports/team')

    expect(screen.getByLabelText('Scope')).not.toBeNull()
    expect(screen.getByRole('button', { name: /export csv/i })).not.toBeNull()
  })
})

describe('ReportsPage — insufficient-data states (§11.6)', () => {
  it('shows "No task history recorded yet" when dataAvailableSince is null', () => {
    setCommonMocks({ viewReports: false, exportReports: false })
    useReportScorecardQuery.mockReturnValue({
      isPending: false,
      isError: false,
      isSuccess: true,
      isFetching: false,
      data: fixtureScorecard({ meta: { ...baseMeta(), dataAvailableSince: null }, buckets: [{ start: '2026-03-02', end: '2026-03-08', isPartial: false, hasData: false, metrics: emptyTotals() }] }),
      refetch: vi.fn(),
    })
    useReportTeamQuery.mockReturnValue({ isPending: true, isError: false, isSuccess: false, isFetching: true, data: undefined, refetch: vi.fn() })

    renderAt('/reports/me')

    expect(screen.getByText(/No task history recorded yet/)).not.toBeNull()
  })

  it('shows the range-before-data state with its "Start from" action when every bucket lacks data', () => {
    setCommonMocks({ viewReports: false, exportReports: false })
    useReportScorecardQuery.mockReturnValue({
      isPending: false,
      isError: false,
      isSuccess: true,
      isFetching: false,
      data: fixtureScorecard({ buckets: [{ start: '2026-03-02', end: '2026-03-08', isPartial: false, hasData: false, metrics: emptyTotals() }] }),
      refetch: vi.fn(),
    })
    useReportTeamQuery.mockReturnValue({ isPending: true, isError: false, isSuccess: false, isFetching: true, data: undefined, refetch: vi.fn() })

    renderAt('/reports/me')

    expect(screen.getByText(/No data in this range\. Data is available from 2026-02-20\./)).not.toBeNull()
    expect(screen.getByRole('button', { name: 'Start from 2026-02-20' })).not.toBeNull()
  })

  it('renders the low-sample chip and a p85 em dash from a low-sample Speed distribution', () => {
    setCommonMocks({ viewReports: false, exportReports: false })
    const totals = emptyTotals()
    totals.speed.cycleTime = { sampleSize: 3, medianSeconds: 248400, p85Seconds: null, lowSample: true }
    useReportScorecardQuery.mockReturnValue({ isPending: false, isError: false, isSuccess: true, isFetching: false, data: fixtureScorecard({ totals }), refetch: vi.fn() })
    useReportTeamQuery.mockReturnValue({ isPending: true, isError: false, isSuccess: false, isFetching: true, data: undefined, refetch: vi.fn() })

    renderAt('/reports/me')

    expect(screen.getByText('Few data · 3')).not.toBeNull()
    // medianSeconds 248400 -> formatDuration -> "2d 21h"; p85 null -> em dash.
    expect(screen.getByText('2d 21h / —')).not.toBeNull()
  })
})

describe('ReportsPage — error state', () => {
  it('shows the mapped copy for Report.GetScorecard.SelfOnly', () => {
    setCommonMocks({ viewReports: false, exportReports: false })
    useReportScorecardQuery.mockReturnValue({
      isPending: false,
      isError: true,
      isSuccess: false,
      isFetching: false,
      error: new ApiError(400, { success: false, errorCode: 'Report.GetScorecard.SelfOnly', error: 'raw' }, 'failed'),
      refetch: vi.fn(),
    })
    useReportTeamQuery.mockReturnValue({ isPending: true, isError: false, isSuccess: false, isFetching: true, data: undefined, refetch: vi.fn() })

    renderAt('/reports/me')

    expect(screen.getByRole('alert').textContent).toContain('You can only view your own report.')
  })
})

describe('ReportsPage — team scope reads the scorecard endpoint for its charts (f09 endpoint 1: userId absent = team)', () => {
  it('renders On-time vs late and Tickets by priority from a team-scope scorecard fixture (subject null); the removed cycle-time chart and the "You vs team" comparison never show', () => {
    setCommonMocks({ viewReports: true, exportReports: false })

    const teamScorecard = fixtureScorecard({
      meta: { ...baseMeta(), scope: 'Team', userId: null },
      subject: null,
      teamMedian: null,
    })
    useReportScorecardQuery.mockReturnValue({ isPending: false, isError: false, isSuccess: true, isFetching: false, data: teamScorecard, refetch: vi.fn() })
    useReportTeamQuery.mockReturnValue({ isPending: false, isError: false, isSuccess: true, isFetching: false, data: fixtureTeam(), refetch: vi.fn() })

    renderAt('/reports/team')

    expect(screen.getByText('On-time vs late')).not.toBeNull()
    expect(screen.getByText('Tickets by priority')).not.toBeNull()
    expect(screen.queryByText('Cycle time trend')).toBeNull()
    expect(screen.queryByText(/vs team average/)).toBeNull()
  })

  it('stays in the loading state until both the scorecard and the team-report query have resolved', () => {
    setCommonMocks({ viewReports: true, exportReports: false })
    useReportScorecardQuery.mockReturnValue({ isPending: true, isError: false, isSuccess: false, isFetching: true, data: undefined, refetch: vi.fn() })
    useReportTeamQuery.mockReturnValue({ isPending: false, isError: false, isSuccess: true, isFetching: false, data: undefined, refetch: vi.fn() })

    renderAt('/reports/team')

    expect(screen.getByRole('status').textContent).toMatch(/loading report/i)
    expect(screen.queryByText('On-time vs late')).toBeNull()
  })
})

describe('ReportsPage — "% vs team" comparison (§E)', () => {
  it('a Member without ViewReports sees no team comparison chart', () => {
    setCommonMocks({ viewReports: false, exportReports: false })
    useReportScorecardQuery.mockReturnValue({ isPending: false, isError: false, isSuccess: true, isFetching: false, data: fixtureScorecard(), refetch: vi.fn() })
    useReportTeamQuery.mockReturnValue({ isPending: true, isError: false, isSuccess: false, isFetching: true, data: undefined, refetch: vi.fn() })

    renderAt('/reports/me')

    expect(screen.queryByText('You vs team average')).toBeNull()
  })

  it('a Manager/Admin viewing their own report sees the comparison once both queries resolve', () => {
    setCommonMocks({ viewReports: true, exportReports: false })
    const totals = emptyTotals()
    totals.delivery.done = 5
    useReportScorecardQuery.mockReturnValue({ isPending: false, isError: false, isSuccess: true, isFetching: false, data: fixtureScorecard({ totals }), refetch: vi.fn() })

    const teamTotals = emptyTotals()
    teamTotals.delivery.done = 20
    useReportTeamQuery.mockReturnValue({
      isPending: false,
      isError: false,
      isSuccess: true,
      isFetching: false,
      data: fixtureTeam({ team: { totals: teamTotals, contributors: 5, unassignedOverdueMarked: 0 } }),
      refetch: vi.fn(),
    })

    renderAt('/reports/me')

    expect(screen.getByText('You vs team average')).not.toBeNull()
    expect(screen.getByText('Compared with the average member who delivered in this period.')).not.toBeNull()
  })
})

describe('ReportsPage — cold load, GET /workflow\'s "today" still pending (bug fix, 2026-11-22)', () => {
  function setColdLoadMocks(viewReports: boolean) {
    useHasPermission.mockImplementation((permission: string) => (permission === 'ViewReports' ? viewReports : permission === 'ExportReports' ? viewReports : false))
    useAuthSession.mockReturnValue({ userId: 1 })
    useUsersQuery.mockReturnValue({ data: [] })
    useWorkflowQuery.mockReturnValue({ data: undefined })
    useReportScorecardQuery.mockReturnValue({ isPending: true, isError: false, isSuccess: false, isFetching: true, data: undefined, refetch: vi.fn() })
    useReportTeamQuery.mockReturnValue({ isPending: true, isError: false, isSuccess: false, isFetching: true, data: undefined, refetch: vi.fn() })
  }

  it('Member visiting /reports/me renders the loading skeleton and never throws', () => {
    setColdLoadMocks(false)

    expect(() => renderAt('/reports/me')).not.toThrow()
    expect(screen.getByRole('status').textContent).toMatch(/loading report/i)
    expect(screen.queryByLabelText('Fiscal year')).toBeNull()
  })

  it('Manager visiting /reports/team renders the loading skeleton and never throws', () => {
    setColdLoadMocks(true)

    expect(() => renderAt('/reports/team')).not.toThrow()
    expect(screen.getByRole('status').textContent).toMatch(/loading report/i)
    expect(screen.queryByLabelText('Fiscal year')).toBeNull()
  })

  it('Manager visiting /reports/me renders the loading skeleton and never throws', () => {
    setColdLoadMocks(true)

    expect(() => renderAt('/reports/me')).not.toThrow()
    expect(screen.getByRole('status').textContent).toMatch(/loading report/i)
    expect(screen.queryByLabelText('Fiscal year')).toBeNull()
  })
})

