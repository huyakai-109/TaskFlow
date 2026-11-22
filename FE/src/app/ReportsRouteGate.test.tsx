import { QueryClient, QueryClientProvider } from '@tanstack/react-query'
import { cleanup, render, screen } from '@testing-library/react'
import { MemoryRouter, Route, Routes } from 'react-router-dom'
import { afterEach, describe, expect, it, vi } from 'vitest'

import { ReportsPage } from '../features/reports/ReportsPage'
import { RequirePermission } from './RequirePermission'

const { useHasPermission, useAuthSession, useUsersQuery, useWorkflowQuery, useReportScorecardQuery, useReportTeamQuery } = vi.hoisted(() => ({
  useHasPermission: vi.fn(),
  useAuthSession: vi.fn(),
  useUsersQuery: vi.fn(),
  useWorkflowQuery: vi.fn(),
  useReportScorecardQuery: vi.fn(),
  useReportTeamQuery: vi.fn(),
}))

vi.mock('../shared/hooks/useHasPermission', () => ({ useHasPermission }))
vi.mock('../shared/hooks/useAuthSession', () => ({ useAuthSession }))
vi.mock('../shared/hooks/useUsersQuery', () => ({ useUsersQuery }))
vi.mock('../shared/hooks/useWorkflowQuery', () => ({ useWorkflowQuery }))
vi.mock('../features/reports/hooks/useReportScorecardQuery', () => ({ useReportScorecardQuery }))
vi.mock('../features/reports/hooks/useReportTeamQuery', () => ({ useReportTeamQuery }))

afterEach(() => {
  cleanup()
  vi.clearAllMocks()
})

/** Mirrors `router.tsx`'s actual wiring: `RequirePermission` wraps `ReportsPage` on `/reports/team` and `/reports/members/:userId` — the composition a typed URL really goes through, and the only place that composition can be exercised without a feature importing `app/` (`frontend.md`/oxlint `no-restricted-imports`: `shared → features → app`). */
function renderGatedRoute(path: string) {
  const queryClient = new QueryClient()
  render(
    <QueryClientProvider client={queryClient}>
      <MemoryRouter initialEntries={[path]}>
        <Routes>
          <Route
            path="/reports/team"
            element={
              <RequirePermission permission="ViewReports">
                <ReportsPage />
              </RequirePermission>
            }
          />
        </Routes>
      </MemoryRouter>
    </QueryClientProvider>,
  )
}

describe('Reports routing — a Member without ViewReports typing /reports/team directly (bug fix, 2026-11-22)', () => {
  it('renders NotFound and never mounts (or flashes) the filter bar', () => {
    useHasPermission.mockImplementation(() => false)
    useAuthSession.mockReturnValue({ userId: 1 })
    useUsersQuery.mockReturnValue({ data: [] })
    useWorkflowQuery.mockReturnValue({ data: undefined })
    useReportScorecardQuery.mockReturnValue({ isPending: true, isError: false, isSuccess: false, isFetching: true, data: undefined, refetch: vi.fn() })
    useReportTeamQuery.mockReturnValue({ isPending: true, isError: false, isSuccess: false, isFetching: true, data: undefined, refetch: vi.fn() })

    renderGatedRoute('/reports/team')

    expect(screen.getByRole('heading').textContent).toMatch(/not found/i)
    expect(screen.queryByLabelText('Fiscal year')).toBeNull()
    expect(screen.queryByRole('status')).toBeNull()
  })
})
