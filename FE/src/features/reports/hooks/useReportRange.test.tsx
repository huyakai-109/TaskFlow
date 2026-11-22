import { act, renderHook } from '@testing-library/react'
import type { ReactNode } from 'react'
import { MemoryRouter } from 'react-router-dom'
import { afterEach, describe, expect, it, vi } from 'vitest'

import { useReportRange } from './useReportRange'

const { useWorkflowQuery } = vi.hoisted(() => ({ useWorkflowQuery: vi.fn() }))

vi.mock('../../../shared/hooks/useWorkflowQuery', () => ({ useWorkflowQuery }))

afterEach(() => {
  vi.clearAllMocks()
})

function wrapper(initialEntry = '/reports/me') {
  return ({ children }: { children: ReactNode }) => <MemoryRouter initialEntries={[initialEntry]}>{children}</MemoryRouter>
}

describe('useReportRange — URL state (§11.2), never storage, never the browser clock', () => {
  it('defaults to "This week" computed from GET /workflow\'s today, before any URL param is set, with the auto granularity for that short a range (§B.5)', () => {
    useWorkflowQuery.mockReturnValue({ data: { today: '2026-03-11', timeZone: 'Asia/Ho_Chi_Minh', maxInProgressPerAssignee: 3, overdueScan: { lastFiredAt: null, nextFireAt: null } } })

    const { result } = renderHook(() => useReportRange(), { wrapper: wrapper() })

    expect(result.current.from).toBe('2026-03-09')
    expect(result.current.to).toBe('2026-03-11')
    expect(result.current.granularity).toBe('Day')
    expect(result.current.today).toBe('2026-03-11')
  })

  it('leaves from/to empty until today has resolved (never falls back to a browser-clock guess)', () => {
    useWorkflowQuery.mockReturnValue({ data: undefined })

    const { result } = renderHook(() => useReportRange(), { wrapper: wrapper() })

    expect(result.current.from).toBe('')
    expect(result.current.to).toBe('')
    expect(result.current.today).toBeNull()
  })

  it('setRange updates from/to, and setGranularity updates granularity, both through the URL', () => {
    useWorkflowQuery.mockReturnValue({ data: { today: '2026-03-11', timeZone: 'Asia/Ho_Chi_Minh', maxInProgressPerAssignee: 3, overdueScan: { lastFiredAt: null, nextFireAt: null } } })

    const { result } = renderHook(() => useReportRange(), { wrapper: wrapper() })

    act(() => result.current.setRange({ from: '2026-01-01', to: '2026-01-31' }))
    expect(result.current.from).toBe('2026-01-01')
    expect(result.current.to).toBe('2026-01-31')

    act(() => result.current.setGranularity('Month'))
    expect(result.current.granularity).toBe('Month')
  })

  it('reads an existing ?from=&to=&g= from the URL instead of the default preset', () => {
    useWorkflowQuery.mockReturnValue({ data: { today: '2026-03-11', timeZone: 'Asia/Ho_Chi_Minh', maxInProgressPerAssignee: 3, overdueScan: { lastFiredAt: null, nextFireAt: null } } })

    const { result } = renderHook(() => useReportRange(), { wrapper: wrapper('/reports/me?from=2026-02-01&to=2026-02-28&g=Month') })

    expect(result.current.from).toBe('2026-02-01')
    expect(result.current.to).toBe('2026-02-28')
    expect(result.current.granularity).toBe('Month')
  })

  it('disables Day granularity once the range exceeds 92 days (R14)', () => {
    useWorkflowQuery.mockReturnValue({ data: { today: '2026-04-03', timeZone: 'Asia/Ho_Chi_Minh', maxInProgressPerAssignee: 3, overdueScan: { lastFiredAt: null, nextFireAt: null } } })

    const { result } = renderHook(() => useReportRange(), { wrapper: wrapper('/reports/me?from=2026-01-01&to=2026-04-03') })

    expect(result.current.dayGranularityAllowed).toBe(false)
  })

  it('a new range re-derives its own automatic granularity instead of keeping the previous range\'s default (§B.5)', () => {
    useWorkflowQuery.mockReturnValue({ data: { today: '2026-12-31', timeZone: 'Asia/Ho_Chi_Minh', maxInProgressPerAssignee: 3, overdueScan: { lastFiredAt: null, nextFireAt: null } } })

    const { result } = renderHook(() => useReportRange(), { wrapper: wrapper() })

    act(() => result.current.setRange({ from: '2026-01-01', to: '2026-01-31' }))
    expect(result.current.granularity).toBe('Day')

    act(() => result.current.setRange({ from: '2026-01-01', to: '2026-12-31' }))
    expect(result.current.granularity).toBe('Month')
  })

  it('clamps `to` to the server today, never sending a range the API would reject with RangeInFuture (§B.6)', () => {
    useWorkflowQuery.mockReturnValue({ data: { today: '2026-03-11', timeZone: 'Asia/Ho_Chi_Minh', maxInProgressPerAssignee: 3, overdueScan: { lastFiredAt: null, nextFireAt: null } } })

    const { result } = renderHook(() => useReportRange(), { wrapper: wrapper() })

    act(() => result.current.setRange({ from: '2026-03-01', to: '2026-03-20' }))

    expect(result.current.to).toBe('2026-03-11')
  })

  it('clamps a range over 366 days down to the limit (§B.6)', () => {
    useWorkflowQuery.mockReturnValue({ data: { today: '2027-01-01', timeZone: 'Asia/Ho_Chi_Minh', maxInProgressPerAssignee: 3, overdueScan: { lastFiredAt: null, nextFireAt: null } } })

    const { result } = renderHook(() => useReportRange(), { wrapper: wrapper() })

    act(() => result.current.setRange({ from: '2025-01-01', to: '2026-06-01' }))

    expect(result.current.from).toBe('2025-06-01')
    expect(result.current.to).toBe('2026-06-01')
  })
})

describe('useReportRange — invalid URL query params fall back to the default range instead of crashing (bug fix, 2026-11-22)', () => {
  const todayData = { today: '2026-11-22', timeZone: 'Asia/Ho_Chi_Minh', maxInProgressPerAssignee: 3, overdueScan: { lastFiredAt: null, nextFireAt: null } }

  it('falls back when from/to are not well-formed dates', () => {
    useWorkflowQuery.mockReturnValue({ data: todayData })

    const { result } = renderHook(() => useReportRange(), { wrapper: wrapper('/reports/me?from=garbage&to=garbage') })

    expect(result.current.from).not.toBe('garbage')
    expect(result.current.to).not.toBe('garbage')
    expect(result.current.from).not.toBe('')
    expect(result.current.to).not.toBe('')
  })

  it('falls back when from/to describe a date that does not exist (e.g. 2026-02-30)', () => {
    useWorkflowQuery.mockReturnValue({ data: todayData })

    const { result } = renderHook(() => useReportRange(), { wrapper: wrapper('/reports/me?from=2026-02-30&to=2026-03-01') })

    expect(result.current.from).not.toBe('2026-02-30')
  })

  it('falls back when from is after to', () => {
    useWorkflowQuery.mockReturnValue({ data: todayData })

    const { result } = renderHook(() => useReportRange(), { wrapper: wrapper('/reports/me?from=2026-11-01&to=2026-01-01') })

    expect(result.current.from).not.toBe('2026-11-01')
    expect(result.current.to).not.toBe('2026-01-01')
  })

  it('falls back when to is in the future relative to the server\'s today', () => {
    useWorkflowQuery.mockReturnValue({ data: todayData })

    const { result } = renderHook(() => useReportRange(), { wrapper: wrapper('/reports/me?from=2026-11-01&to=2027-01-01') })

    expect(result.current.to).not.toBe('2027-01-01')
  })

  it('falls back when the range spans more than 366 days', () => {
    useWorkflowQuery.mockReturnValue({ data: todayData })

    const { result } = renderHook(() => useReportRange(), { wrapper: wrapper('/reports/me?from=2024-01-01&to=2026-11-22') })

    expect(result.current.from).not.toBe('2024-01-01')
  })
})
