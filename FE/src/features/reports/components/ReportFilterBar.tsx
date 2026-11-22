import { useNavigate } from 'react-router-dom'

import type { ReportGranularity, UserListItem } from '../../../api/types'
import type { DateRange, RangePresetKey } from '../utils/rangePresets'

const PRESET_LABELS: Record<RangePresetKey, string> = {
  thisWeek: 'This week',
  lastWeek: 'Last week',
  thisMonth: 'This month',
  lastMonth: 'Last month',
  thisQuarter: 'This quarter',
  lastQuarter: 'Last quarter',
}

const GRANULARITIES: ReportGranularity[] = ['Day', 'Week', 'Month', 'Quarter']

interface ReportFilterBarProps {
  canViewReports: boolean
  members: UserListItem[]
  selectedUserId: number | undefined
  ownUserId: number
  range: DateRange
  today: string | null
  granularity: ReportGranularity
  isTeamScope: boolean
  dayGranularityAllowed: boolean
  onPresetSelect: (preset: RangePresetKey) => void
  onCustomRange: (range: DateRange) => void
  onGranularityChange: (granularity: ReportGranularity) => void
}

const controlClass =
  'min-h-9 rounded-md border border-border bg-surface px-[var(--space-2)] text-text [font:inherit] disabled:cursor-not-allowed disabled:opacity-60'

/**
 * §11.2 point 2 — scope picker (Manager/Admin only), range presets +
 * custom, granularity (hidden in team scope: §11.2 "The team view has no
 * granularity control — always weekly"). All state round-trips through
 * `useReportRange`'s URL params, never component state.
 */
export function ReportFilterBar({
  canViewReports,
  members,
  selectedUserId,
  ownUserId,
  range,
  today,
  granularity,
  isTeamScope,
  dayGranularityAllowed,
  onPresetSelect,
  onCustomRange,
  onGranularityChange,
}: ReportFilterBarProps) {
  const navigate = useNavigate()

  return (
    <div className="flex flex-wrap items-end gap-[var(--space-3)]">
      {canViewReports && (
        <div className="flex flex-col gap-[var(--space-1)]">
          <label htmlFor="reports-scope" className="text-[length:var(--font-size-xs)] text-text-muted">
            Scope
          </label>
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
                {member.displayName ?? member.userName}
                {!member.isActive && ' · deactivated'}
              </option>
            ))}
          </select>
        </div>
      )}

      <div className="flex flex-col gap-[var(--space-1)]">
        <span id="reports-range-label" className="text-[length:var(--font-size-xs)] text-text-muted">
          Range
        </span>
        <div role="group" aria-labelledby="reports-range-label" className="flex flex-wrap gap-[2px] rounded-md border border-border bg-column p-[2px] max-[600px]:hidden">
          {(Object.keys(PRESET_LABELS) as RangePresetKey[]).map((preset) => (
            <button
              key={preset}
              type="button"
              className="cursor-pointer rounded-sm px-[var(--space-2)] py-[4px] text-[length:var(--font-size-xs)] text-text-muted hover:bg-surface hover:text-text"
              onClick={() => onPresetSelect(preset)}
            >
              {PRESET_LABELS[preset]}
            </button>
          ))}
        </div>
        <select
          aria-labelledby="reports-range-label"
          className={`${controlClass} min-[601px]:hidden`}
          onChange={(event) => onPresetSelect(event.target.value as RangePresetKey)}
          defaultValue=""
        >
          <option value="" disabled>
            Choose a range…
          </option>
          {(Object.keys(PRESET_LABELS) as RangePresetKey[]).map((preset) => (
            <option key={preset} value={preset}>
              {PRESET_LABELS[preset]}
            </option>
          ))}
        </select>
      </div>

      <div className="flex flex-col gap-[var(--space-1)]">
        <label htmlFor="reports-from" className="text-[length:var(--font-size-xs)] text-text-muted">
          From
        </label>
        <input
          id="reports-from"
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

      {!isTeamScope && (
        <div className="flex flex-col gap-[var(--space-1)]">
          <span id="reports-granularity-label" className="text-[length:var(--font-size-xs)] text-text-muted">
            Granularity
          </span>
          <div role="group" aria-labelledby="reports-granularity-label" className="flex gap-[2px] rounded-md border border-border bg-column p-[2px]">
            {GRANULARITIES.map((option) => {
              const disabled = option === 'Day' && !dayGranularityAllowed

              return (
                <button
                  key={option}
                  type="button"
                  disabled={disabled}
                  title={disabled ? 'Day view is limited to 92 days' : undefined}
                  aria-pressed={granularity === option}
                  className="cursor-pointer rounded-sm px-[var(--space-2)] py-[4px] text-[length:var(--font-size-xs)] text-text-muted hover:bg-surface hover:text-text aria-pressed:bg-surface aria-pressed:text-text aria-pressed:font-medium disabled:cursor-not-allowed disabled:opacity-50"
                  onClick={() => onGranularityChange(option)}
                >
                  {option}
                </button>
              )
            })}
          </div>
        </div>
      )}
    </div>
  )
}
