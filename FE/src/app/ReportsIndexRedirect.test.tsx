import { cleanup, render, screen } from '@testing-library/react'
import { MemoryRouter, Route, Routes } from 'react-router-dom'
import { afterEach, describe, expect, it, vi } from 'vitest'

import { ReportsIndexRedirect } from './ReportsIndexRedirect'

const { useAuthSession, useHasPermission } = vi.hoisted(() => ({
  useAuthSession: vi.fn(),
  useHasPermission: vi.fn(),
}))

vi.mock('../shared/hooks/useAuthSession', () => ({ useAuthSession }))
vi.mock('../shared/hooks/useHasPermission', () => ({ useHasPermission }))

afterEach(() => {
  cleanup()
  vi.clearAllMocks()
})

function renderRedirect() {
  render(
    <MemoryRouter initialEntries={['/reports']}>
      <Routes>
        <Route path="/reports" element={<ReportsIndexRedirect />} />
        <Route path="/reports/team" element={<p>team screen</p>} />
        <Route path="/reports/me" element={<p>me screen</p>} />
      </Routes>
    </MemoryRouter>,
  )
}

describe('ReportsIndexRedirect — waits for permissions before deciding (live dark-theme check fix)', () => {
  it('does not redirect while the session is still unknown', () => {
    useAuthSession.mockReturnValue({ status: 'unknown', permissions: [] })
    useHasPermission.mockReturnValue(false)

    renderRedirect()

    expect(screen.queryByText('team screen')).toBeNull()
    expect(screen.queryByText('me screen')).toBeNull()
    expect(screen.getByRole('status').textContent).toMatch(/checking your session/i)
  })

  it('does not redirect while authenticated but permissions have not arrived yet', () => {
    useAuthSession.mockReturnValue({ status: 'authenticated', permissions: [] })
    useHasPermission.mockReturnValue(false)

    renderRedirect()

    expect(screen.queryByText('team screen')).toBeNull()
    expect(screen.queryByText('me screen')).toBeNull()
  })

  it('redirects a Manager/Admin (ViewReports) to /reports/team once permissions are loaded', () => {
    useAuthSession.mockReturnValue({ status: 'authenticated', permissions: ['ViewTasks', 'ViewReports', 'ExportReports'] })
    useHasPermission.mockReturnValue(true)

    renderRedirect()

    expect(screen.getByText('team screen')).not.toBeNull()
  })

  it('redirects a Member (no ViewReports) to /reports/me once permissions are loaded', () => {
    useAuthSession.mockReturnValue({ status: 'authenticated', permissions: ['ViewTasks'] })
    useHasPermission.mockReturnValue(false)

    renderRedirect()

    expect(screen.getByText('me screen')).not.toBeNull()
  })
})
