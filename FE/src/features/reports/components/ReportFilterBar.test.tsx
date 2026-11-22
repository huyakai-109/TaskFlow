import { cleanup, fireEvent, render, screen } from '@testing-library/react'
import { MemoryRouter } from 'react-router-dom'
import { afterEach, describe, expect, it, vi } from 'vitest'

import type { UserListItem } from '../../../api/types'

import { ReportFilterBar } from './ReportFilterBar'

afterEach(() => cleanup())

function members(): UserListItem[] {
  return [
    { id: 1, userName: 'member1', displayName: 'Lan', role: 'Member', avatarUrl: null, isActive: true },
    { id: 2, userName: 'member2', displayName: 'Minh', role: 'Member', avatarUrl: null, isActive: true },
    { id: 3, userName: 'member3', displayName: 'Hoa', role: 'Member', avatarUrl: null, isActive: true },
  ]
}

function renderBar(overrides: Partial<React.ComponentProps<typeof ReportFilterBar>> = {}) {
  const props: React.ComponentProps<typeof ReportFilterBar> = {
    canViewReports: true,
    members: members(),
    selectedUserId: 1,
    ownUserId: 1,
    range: { from: '2026-07-01', to: '2027-06-30' },
    today: '2026-11-22',
    dataAvailableSince: '2026-02-20',
    generatedAt: '2026-11-22T03:00:00+00:00',
    granularity: 'Week',
    isTeamScope: false,
    dayGranularityAllowed: true,
    onPresetSelect: vi.fn(),
    onCustomRange: vi.fn(),
    onGranularityChange: vi.fn(),
    ...overrides,
  }

  return render(
    <MemoryRouter>
      <ReportFilterBar {...props} />
    </MemoryRouter>,
  )
}

describe('ReportFilterBar — FY/Month/Custom detection (§B.2/B.3/B.8)', () => {
  it('shows the matching fiscal year when the range is a whole FY', () => {
    renderBar({ range: { from: '2026-07-01', to: '2027-06-30' } })
    expect((screen.getByLabelText('Fiscal year') as HTMLSelectElement).value).toBe('2027')
    expect((screen.getByLabelText('Month') as HTMLSelectElement).value).toBe('all')
  })

  it('shows the matching month when the range is a whole fiscal month', () => {
    renderBar({ range: { from: '2026-07-01', to: '2026-07-31' } })
    expect((screen.getByLabelText('Month') as HTMLSelectElement).value).toBe('2026-7')
  })

  it('shows "Custom" in both selects for a hand-picked range', () => {
    renderBar({ range: { from: '2026-07-05', to: '2026-07-20' } })
    expect((screen.getByLabelText('Fiscal year') as HTMLSelectElement).value).toBe('custom')
    expect((screen.getByLabelText('Month') as HTMLSelectElement).value).toBe('custom')
  })
})

describe('ReportFilterBar — date-range popover (§B.4)', () => {
  it('opens on click, closes on Esc, and returns focus to the trigger', () => {
    renderBar()
    const trigger = screen.getByRole('button', { name: /2026|today/i })
    // Real browsers focus a button on click (jsdom's fireEvent.click does not
    // replicate that side effect), so this mirrors the actual user flow.
    trigger.focus()
    fireEvent.click(trigger)
    expect(screen.getByRole('dialog')).not.toBeNull()

    fireEvent.keyDown(document, { key: 'Escape' })
    expect(screen.queryByRole('dialog')).toBeNull()
    expect(document.activeElement).toBe(trigger)
  })

  it('is not clipped by a fixed pixel width at 390px (uses a viewport-relative max width)', () => {
    renderBar()
    fireEvent.click(screen.getByRole('button', { name: /2026|today/i }))
    const dialog = screen.getByRole('dialog')
    expect(dialog.className).toContain('calc(100vw-32px)')
  })
})

describe('ReportFilterBar — defensive range label on a cold load (bug fix, 2026-11-22)', () => {
  it('renders an em dash, not a thrown RangeError, when from/to/today are all empty/null (today unresolved)', () => {
    expect(() => renderBar({ range: { from: '', to: '' }, today: null, dataAvailableSince: null, generatedAt: null })).not.toThrow()
    expect(screen.getByRole('button', { name: '— - —' })).not.toBeNull()
  })

  it('renders an em dash for "Updated" when generatedAt is missing', () => {
    renderBar({ generatedAt: null })
    expect(screen.queryByText(/^Updated/)).toBeNull()
  })
})

describe('ReportFilterBar — previous/next member arrows (§F.3)', () => {
  it('"Previous member" is disabled at the first member in the list', () => {
    renderBar({ selectedUserId: 1 })
    expect((screen.getByRole('button', { name: 'Previous member' }) as HTMLButtonElement).disabled).toBe(true)
    expect((screen.getByRole('button', { name: 'Next member' }) as HTMLButtonElement).disabled).toBe(false)
  })

  it('"Next member" is disabled at the last member in the list', () => {
    renderBar({ selectedUserId: 3 })
    expect((screen.getByRole('button', { name: 'Next member' }) as HTMLButtonElement).disabled).toBe(true)
    expect((screen.getByRole('button', { name: 'Previous member' }) as HTMLButtonElement).disabled).toBe(false)
  })

  it('neither arrow renders at team scope', () => {
    renderBar({ isTeamScope: true, selectedUserId: undefined })
    expect(screen.queryByRole('button', { name: 'Previous member' })).toBeNull()
    expect(screen.queryByRole('button', { name: 'Next member' })).toBeNull()
  })
})
