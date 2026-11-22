import { useRef, useState } from 'react'
import { useNavigate } from 'react-router-dom'

import type { ReportGranularity, UserListItem } from '../../../api/types'
import { useDialogA11y } from '../../../shared/hooks/useDialogA11y'
import { userLabel } from '../../../shared/lib/userLabel'

import { detectFySelection, fiscalMonthsInOrder, fiscalYearOf, fiscalYearRange, listFiscalYears } from '../utils/fiscalYear'
import type { DateRange, RangePresetKey } from '../utils/rangePresets'
import { formatDateSafe } from '../utils/safeDate'

const PRESET_LABELS: Record<RangePresetKey, string> = {
  thisWeek: 'This week',
  lastWeek: 'Last week',
  thisMonth: 'This month',
  lastMonth: 'Last month',
  thisQuarter: 'This quarter',
  lastQuarter: 'Last quarter',
}

const GRANULARITIES: ReportGranularity[] = ['Day', 'Week', 'Month', 'Quarter']

const controlClass =
  'min-h-9 rounded-md border border-border bg-surface px-[var(--space-2)] text-text [font:inherit] disabled:cursor-not-allowed disabled:opacity-60'

const dayFormatter = new Intl.DateTimeFormat('en', { day: 'numeric', month: 'short', year: 'numeric' })
const timeFormatter = new Intl.DateTimeFormat('en', { hour: '2-digit', minute: '2-digit' })

/** Defensive (bug fix, 2026-11-22): `range.from`/`range.to` can be empty before `GET /workflow`'s `today` resolves — never throw, show an em dash instead. */
function resolvedRangeLabel(range: DateRange, today: string | null): string {
  const from = formatDateSafe(dayFormatter, range.from)
  const to = range.to !== '' && range.to === today ? 'Today' : formatDateSafe(dayFormatter, range.to)
  return `${from} - ${to}`
}

interface DateRangePopoverProps {
  range: DateRange
  today: string | null
  onPresetSelect: (preset: RangePresetKey) => void
  onCustomRange: (range: DateRange) => void
  onClose: () => void
}

/** Mounted only while open (`ReportFilterBar`'s `popoverOpen` state) so `useDialogA11y`'s mount effect — focus in, Esc closes, focus returns — runs fresh every time it opens, the same pattern every other dialog in this app already uses. */
function DateRangePopover({ range, today, onPresetSelect, onCustomRange, onClose }: DateRangePopoverProps) {
  const fromInputRef = useRef<HTMLInputElement>(null)
  useDialogA11y(fromInputRef, onClose)

  return (
    <div
      role="dialog"
      aria-modal="false"
      aria-labelledby="reports-range-label"
      className="absolute top-full left-0 z-20 mt-[var(--space-1)] flex w-[min(360px,calc(100vw-32px))] flex-col gap-[var(--space-3)] rounded-md border border-border bg-surface-raised p-[var(--space-3)] shadow-raised"
    >
      <div className="flex flex-wrap gap-[var(--space-1)]">
        {(Object.keys(PRESET_LABELS) as RangePresetKey[]).map((preset) => (
          <button
            key={preset}
            type="button"
            className="cursor-pointer rounded-sm border border-border bg-surface px-[var(--space-2)] py-[4px] text-[length:var(--font-size-xs)] text-text-muted hover:bg-column hover:text-text"
            onClick={() => {
              onPresetSelect(preset)
              onClose()
            }}
          >
            {PRESET_LABELS[preset]}
          </button>
        ))}
      </div>

      <div className="flex flex-wrap items-end gap-[var(--space-2)]">
        <div className="flex flex-col gap-[var(--space-1)]">
          <label htmlFor="reports-from" className="text-[length:var(--font-size-xs)] text-text-muted">
            From
          </label>
          <input
            id="reports-from"
            ref={fromInputRef}
            type="date"
            className={controlClass}
            value={range.from}
            max={today ?? undefined}
            onChange={(event) => onCustomRange({ from: event.target.value, to: range.to })}
          />
        </div>

        <div className="flex flex-col gap-[var(--space-1)]">
          <label htmlFor="reports-to" className="text-[length:var(--font-size-xs)] text-text-muted">
            To
          </label>
          <input
            id="reports-to"
            type="date"
            className={controlClass}
            value={range.to}
            max={today ?? undefined}
            onChange={(event) => onCustomRange({ from: range.from, to: event.target.value })}
          />
        </div>
      </div>
    </div>
  )
}

interface ReportFilterBarProps {
  canViewReports: boolean
  members: UserListItem[]
  selectedUserId: number | undefined
  ownUserId: number
  range: DateRange
  today: string | null
  dataAvailableSince: string | null
  generatedAt: string | null
  granularity: ReportGranularity
  isTeamScope: boolean
  dayGranularityAllowed: boolean
  onPresetSelect: (preset: RangePresetKey) => void
  onCustomRange: (range: DateRange) => void
  onGranularityChange: (granularity: ReportGranularity) => void
}

/**
 * §B (2026-11-22 brief) — replaces the old range-chips/From-To/Granularity
 * row: scope (+ previous/next member arrows, §F.3), fiscal year, month,
 * a date-range popover (quick links + hand-editable dates), an automatic
 * "Group by" granularity with manual override, and "Updated HH:mm".
 */
export function ReportFilterBar({
  canViewReports,
  members,
  selectedUserId,
  ownUserId,
  range,
  today,
  dataAvailableSince,
  generatedAt,
  granularity,
  isTeamScope,
  dayGranularityAllowed,
  onPresetSelect,
  onCustomRange,
  onGranularityChange,
}: ReportFilterBarProps) {
  const navigate = useNavigate()
  const [popoverOpen, setPopoverOpen] = useState(false)

  const selection = detectFySelection(range)
  const earliest = dataAvailableSince ?? today ?? range.from
  const fyOptions = today !== null ? listFiscalYears(earliest, today) : []
  const effectiveFy = selection.kind === 'custom' ? (today !== null ? fiscalYearOf(today) : fyOptions.at(-1)) : selection.fy
  const months = effectiveFy !== undefined ? fiscalMonthsInOrder(effectiveFy) : []

  function handleFySelect(value: string) {
    if (value === 'custom') {
      return
    }

    onCustomRange(fiscalYearRange(Number(value)))
  }

  function handleMonthSelect(value: string) {
    if (value === 'all' || value === 'custom') {
      if (value === 'all' && effectiveFy !== undefined) {
        onCustomRange(fiscalYearRange(effectiveFy))
      }
      return
    }

    const month = months.find((m) => `${m.year}-${m.month}` === value)

    if (month !== undefined) {
      onCustomRange(month.range)
    }
  }

  const memberIds = members.map((m) => m.id)
  const currentIndex = selectedUserId !== undefined ? memberIds.indexOf(selectedUserId) : -1

  function stepMember(direction: -1 | 1) {
    if (currentIndex === -1) {
      return
    }

    const nextIndex = currentIndex + direction
    const nextId = memberIds[nextIndex]

    if (nextId === undefined) {
      return
    }

    navigate(nextId === ownUserId ? '/reports/me' : `/reports/members/${nextId}`)
  }

  return (
    <div className="flex flex-col gap-[var(--space-2)]">
      <div className="flex flex-wrap items-end gap-[var(--space-3)]">
        {canViewReports && (
          <div className="flex flex-col gap-[var(--space-1)]">
            <label htmlFor="reports-scope" className="text-[length:var(--font-size-xs)] text-text-muted">
              Scope
            </label>
            <div className="flex items-center gap-[var(--space-1)]">
              {!isTeamScope && (
                <button
                  type="button"
                  aria-label="Previous member"
                  disabled={currentIndex <= 0}
                  className="inline-flex size-9 shrink-0 cursor-pointer items-center justify-center rounded-md border border-border bg-surface text-text-muted disabled:cursor-not-allowed disabled:opacity-40"
                  onClick={() => stepMember(-1)}
                >
                  ‹
                </button>
              )}
              <select
                id="reports-scope"
                className={controlClass}
                value={isTeamScope ? 'team' : String(selectedUserId ?? ownUserId)}
                onChange={(event) => {
                  const value = event.target.value
                  navigate(value === 'team' ? '/reports/team' : value === String(ownUserId) ? '/reports/me' : `/reports/members/${value}`)
                }}
              >
                <option value="team">Team</option>
                {members.map((member) => (
                  <option key={member.id} value={member.id}>
                    {userLabel(member)}
                    {!member.isActive && ' · deactivated'}
                  </option>
                ))}
              </select>
              {!isTeamScope && (
                <button
                  type="button"
                  aria-label="Next member"
                  disabled={currentIndex === -1 || currentIndex >= memberIds.length - 1}
                  className="inline-flex size-9 shrink-0 cursor-pointer items-center justify-center rounded-md border border-border bg-surface text-text-muted disabled:cursor-not-allowed disabled:opacity-40"
                  onClick={() => stepMember(1)}
                >
                  ›
                </button>
              )}
            </div>
          </div>
        )}

        <div className="flex flex-col gap-[var(--space-1)]">
          <label htmlFor="reports-fy" className="text-[length:var(--font-size-xs)] text-text-muted">
            Fiscal year
          </label>
          <select id="reports-fy" className={controlClass} value={selection.kind === 'custom' ? 'custom' : selection.fy} onChange={(event) => handleFySelect(event.target.value)}>
            {selection.kind === 'custom' && (
              <option value="custom" disabled>
                Custom
              </option>
            )}
            {fyOptions.map((fy) => (
              <option key={fy} value={fy}>
                FY {fy}
              </option>
            ))}
          </select>
        </div>

        <div className="flex flex-col gap-[var(--space-1)]">
          <label htmlFor="reports-month" className="text-[length:var(--font-size-xs)] text-text-muted">
            Month
          </label>
          <select
            id="reports-month"
            className={controlClass}
            value={selection.kind === 'month' ? `${selection.year}-${selection.month}` : selection.kind === 'fy' ? 'all' : 'custom'}
            onChange={(event) => handleMonthSelect(event.target.value)}
          >
            {selection.kind === 'custom' && (
              <option value="custom" disabled>
                Custom
              </option>
            )}
            <option value="all">All months</option>
            {months.map((month) => (
              <option key={`${month.year}-${month.month}`} value={`${month.year}-${month.month}`}>
                {month.label}
              </option>
            ))}
          </select>
        </div>

        <div className="relative flex flex-col gap-[var(--space-1)]">
          <span id="reports-range-label" className="text-[length:var(--font-size-xs)] text-text-muted">
            Range
          </span>
          <button
            type="button"
            aria-haspopup="dialog"
            aria-expanded={popoverOpen}
            className={`${controlClass} cursor-pointer text-left`}
            onClick={() => setPopoverOpen((value) => !value)}
          >
            {resolvedRangeLabel(range, today)}
          </button>

          {popoverOpen && (
            <DateRangePopover
              range={range}
              today={today}
              onPresetSelect={onPresetSelect}
              onCustomRange={onCustomRange}
              onClose={() => setPopoverOpen(false)}
            />
          )}
        </div>

        <div className="flex flex-col gap-[var(--space-1)]">
          <span id="reports-granularity-label" className="text-[length:var(--font-size-xs)] text-text-muted">
            Group by
          </span>
          <select
            aria-labelledby="reports-granularity-label"
            className={controlClass}
            value={granularity}
            onChange={(event) => onGranularityChange(event.target.value as ReportGranularity)}
          >
            {GRANULARITIES.map((option) => {
              const disabled = option === 'Day' && !dayGranularityAllowed

              return (
                <option key={option} value={option} disabled={disabled}>
                  {option}
                </option>
              )
            })}
          </select>
        </div>
      </div>

      <div className="flex flex-wrap items-center gap-[var(--space-3)] text-[length:var(--font-size-2xs)] text-text-muted">
        {dataAvailableSince !== null && <span>Data available since {dataAvailableSince}</span>}
        {generatedAt !== null && <span>Updated {formatDateSafe(timeFormatter, generatedAt)}</span>}
      </div>
    </div>
  )
}
