import { useMemo } from 'react'
import { useSearchParams } from 'react-router-dom'

import type { ReportGranularity } from '../../../api/types'
import { useWorkflowQuery } from '../../../shared/hooks/useWorkflowQuery'
import { autoGranularity, isDayGranularityAllowed, rangeLengthDays, rangePresets } from '../utils/rangePresets'
import type { DateRange, RangePresetKey } from '../utils/rangePresets'

const DEFAULT_PRESET: RangePresetKey = 'thisWeek'
const MAX_RANGE_DAYS = 366

export interface ReportRangeState extends DateRange {
  granularity: ReportGranularity
  /** `null` while `GET /workflow`'s `today` hasn't resolved yet — every preset/limit depends on it, never the browser clock. */
  today: string | null
  dayGranularityAllowed: boolean
  setRange: (range: DateRange) => void
  setPreset: (preset: RangePresetKey) => void
  setGranularity: (granularity: ReportGranularity) => void
}

function addDaysIso(iso: string, days: number): string {
  const [year, month, day] = iso.split('-').map(Number)
  const date = new Date(Date.UTC(year, month - 1, day))
  date.setUTCDate(date.getUTCDate() + days)
  return date.toISOString().slice(0, 10)
}

/** `yyyy-MM-dd` and a real calendar date (rejects e.g. `2026-02-30`, which `Date.UTC` would otherwise silently roll into March). */
function isValidIsoDate(value: string): boolean {
  if (!/^\d{4}-\d{2}-\d{2}$/.test(value)) {
    return false
  }

  const [year, month, day] = value.split('-').map(Number)
  const date = new Date(Date.UTC(year, month - 1, day))
  return date.getUTCFullYear() === year && date.getUTCMonth() === month - 1 && date.getUTCDate() === day
}

/**
 * Bug fix (2026-11-22): a typed URL's `?from=&to=` is untrusted input — a
 * malformed date, `from` after `to`, `to` in the future (server `today`,
 * never the browser clock) or a span over `MAX_RANGE_DAYS` must never reach
 * a formatter or a request; this falls back to the computed default range
 * (the caller's `presets[DEFAULT_PRESET]`) instead of clamping bad input, so
 * the server never sees it. Returns `null` either param is absent so the
 * caller's own default applies.
 */
function validatedRangeFromParams(searchParams: URLSearchParams, today: string | null): DateRange | null {
  const from = searchParams.get('from')
  const to = searchParams.get('to')

  if (from === null || to === null || !isValidIsoDate(from) || !isValidIsoDate(to)) {
    return null
  }

  if (from > to) {
    return null
  }

  if (today !== null && to > today) {
    return null
  }

  if (rangeLengthDays({ from, to }) > MAX_RANGE_DAYS) {
    return null
  }

  return { from, to }
}

/**
 * §B.6/B.8 (2026-11-22 brief) — the server rejects `to` after its own
 * "today" (`RangeInFuture`) and a range over 366 days (`RangeTooLarge`);
 * this clamps both before the request is ever sent, rather than letting the
 * API reject it. `today` is the server's own value (`GET /workflow`), never
 * the browser clock.
 */
function clampRange(range: DateRange, today: string | null): DateRange {
  let { from, to } = range

  if (today !== null && to > today) {
    to = today
  }

  if (from > to) {
    from = to
  }

  if (rangeLengthDays({ from, to }) > MAX_RANGE_DAYS) {
    from = addDaysIso(to, -(MAX_RANGE_DAYS - 1))
  }

  return { from, to }
}

/**
 * All filter state lives in the URL (`?from=&to=&g=`, §11.2/§B.8), never in
 * storage — a reload or a shared link reproduces the same report. `fy` and
 * `month` are never stored here; they are derived from `from`/`to` on read
 * (`utils/fiscalYear.ts`'s `detectFySelection`, used by `ReportFilterBar`).
 * Falls back to "This week" (computed from the server's `today`) only once
 * `today` is known; until then `from`/`to` are `null` and callers should
 * treat the range as not ready (`ReportsPage`'s loading state already
 * covers this, since the scorecard/team queries are disabled without a
 * `from`/`to`). Granularity defaults to the automatic choice for the
 * current range (§B.5) rather than a fixed value, unless the URL already
 * carries an explicit `g` (the user's own "Group by" override).
 */
export function useReportRange(): ReportRangeState {
  const [searchParams, setSearchParams] = useSearchParams()
  const workflowQuery = useWorkflowQuery()
  const today = workflowQuery.data?.today ?? null

  const presets = useMemo(() => (today === null ? null : rangePresets(today)), [today])
  const paramsRange = validatedRangeFromParams(searchParams, today)

  const from = paramsRange?.from ?? presets?.[DEFAULT_PRESET].from ?? null
  const to = paramsRange?.to ?? presets?.[DEFAULT_PRESET].to ?? null
  const granularityParam = searchParams.get('g')
  const isValidGranularity = (value: string | null): value is ReportGranularity =>
    value === 'Day' || value === 'Week' || value === 'Month' || value === 'Quarter'

  const granularity: ReportGranularity = isValidGranularity(granularityParam)
    ? granularityParam
    : from !== null && to !== null
      ? autoGranularity({ from, to })
      : 'Week'

  const dayGranularityAllowed = from !== null && to !== null ? isDayGranularityAllowed({ from, to }) : true

  function setRange(range: DateRange) {
    const clamped = clampRange(range, today)

    setSearchParams(
      (previous) => {
        const next = new URLSearchParams(previous)
        next.set('from', clamped.from)
        next.set('to', clamped.to)
        // A new range re-derives its own automatic granularity (§B.5) —
        // clearing `g` here, rather than setting it explicitly, keeps a
        // later range change free to pick its own default again instead of
        // being stuck on whatever this range's default happened to be.
        next.delete('g')
        return next
      },
      { replace: true },
    )
  }

  function setPreset(preset: RangePresetKey) {
    if (presets === null) {
      return
    }

    setRange(presets[preset])
  }

  function setGranularity(nextGranularity: ReportGranularity) {
    setSearchParams(
      (previous) => {
        const next = new URLSearchParams(previous)
        next.set('g', nextGranularity)
        return next
      },
      { replace: true },
    )
  }

  return { from: from ?? '', to: to ?? '', granularity, today, dayGranularityAllowed, setRange, setPreset, setGranularity }
}
