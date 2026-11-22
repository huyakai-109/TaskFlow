import { useMemo } from 'react'
import { useSearchParams } from 'react-router-dom'

import type { ReportGranularity } from '../../../api/types'
import { useWorkflowQuery } from '../../../shared/hooks/useWorkflowQuery'
import { isDayGranularityAllowed, rangePresets } from '../utils/rangePresets'
import type { DateRange, RangePresetKey } from '../utils/rangePresets'

const DEFAULT_PRESET: RangePresetKey = 'thisWeek'
const DEFAULT_GRANULARITY: ReportGranularity = 'Week'

export interface ReportRangeState extends DateRange {
  granularity: ReportGranularity
  /** `null` while `GET /workflow`'s `today` hasn't resolved yet — every preset/limit depends on it, never the browser clock. */
  today: string | null
  dayGranularityAllowed: boolean
  setRange: (range: DateRange) => void
  setPreset: (preset: RangePresetKey) => void
  setGranularity: (granularity: ReportGranularity) => void
}

/**
 * All filter state lives in the URL (`?from=&to=&g=`, §11.2), never in
 * storage — a reload or a shared link reproduces the same report. Falls
 * back to "This week" (computed from the server's `today`) only once
 * `today` is known; until then `from`/`to` are `null` and callers should
 * treat the range as not ready (`ReportsPage`'s loading state already
 * covers this, since the scorecard/team queries are disabled without a
 * `from`/`to`).
 */
export function useReportRange(): ReportRangeState {
  const [searchParams, setSearchParams] = useSearchParams()
  const workflowQuery = useWorkflowQuery()
  const today = workflowQuery.data?.today ?? null

  const presets = useMemo(() => (today === null ? null : rangePresets(today)), [today])

  const from = searchParams.get('from') ?? presets?.[DEFAULT_PRESET].from ?? null
  const to = searchParams.get('to') ?? presets?.[DEFAULT_PRESET].to ?? null
  const granularityParam = searchParams.get('g')
  const granularity: ReportGranularity =
    granularityParam === 'Day' || granularityParam === 'Week' || granularityParam === 'Month' || granularityParam === 'Quarter'
      ? granularityParam
      : DEFAULT_GRANULARITY

  const dayGranularityAllowed = from !== null && to !== null ? isDayGranularityAllowed({ from, to }) : true

  function setRange(range: DateRange) {
    setSearchParams(
      (previous) => {
        const next = new URLSearchParams(previous)
        next.set('from', range.from)
        next.set('to', range.to)
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
