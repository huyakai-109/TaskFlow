import { cleanup, render, screen } from '@testing-library/react'
import { MemoryRouter } from 'react-router-dom'
import { afterEach, describe, expect, it } from 'vitest'

import { clearSession, setSession } from '../shared/lib/authSession'
import { RequirePermission } from './RequirePermission'

// No global testing-library setup file in this project (every other test
// uses `renderHook`, not `render`) — cleaned up locally rather than adding
// one for this single test file.
afterEach(() => {
  cleanup()
  clearSession()
})

describe('RequirePermission — account administration gate (item 5)', () => {
  it('renders a 404, not the gated page, for a session without ManageUsers (Member/Manager)', () => {
    setSession(['ViewTasks', 'CreateTasks', 'UpdateTasks', 'MoveTasks', 'AssignTasks', 'PlanTasks', 'AssignAnyone'])

    render(
      <MemoryRouter>
        <RequirePermission permission="ManageUsers">
          <p>Accounts page content</p>
        </RequirePermission>
      </MemoryRouter>,
    )

    expect(screen.queryByText('Accounts page content')).toBeNull()
    expect(screen.getByRole('heading').textContent).toMatch(/not found/i)
  })

  it('renders the gated page for a session with ManageUsers (Admin)', () => {
    setSession(['ViewTasks', 'CreateTasks', 'UpdateTasks', 'MoveTasks', 'AssignTasks', 'PlanTasks', 'AssignAnyone', 'ManageUsers'])

    render(
      <MemoryRouter>
        <RequirePermission permission="ManageUsers">
          <p>Accounts page content</p>
        </RequirePermission>
      </MemoryRouter>,
    )

    expect(screen.getByText('Accounts page content')).not.toBeNull()
  })
})
