import { useRef, useState } from 'react'

import { describeError } from '../../../api/errorMessages'
import type { ReportExportDataset, ReportGranularity } from '../../../api/types'
import { Button } from '../../../shared/components/Button'
import { useDialogA11y } from '../../../shared/hooks/useDialogA11y'

import { useReportExport } from '../hooks/useReportExport'

interface ExportMenuProps {
  userId?: number
  from: string
  to: string
  granularity: ReportGranularity
  isTeamScope: boolean
}

/** §11.8 — visible only with `ExportReports` (gated by the caller); Buckets · Members (team only) · Tasks. */
export function ExportMenu({ userId, from, to, granularity, isTeamScope }: ExportMenuProps) {
  const [open, setOpen] = useState(false)
  const [error, setError] = useState<string | null>(null)
  const firstItemRef = useRef<HTMLButtonElement>(null)
  const exportMutation = useReportExport()
  useDialogA11y(firstItemRef, () => setOpen(false), exportMutation.isPending)

  function handleExport(dataset: ReportExportDataset) {
    setError(null)
    exportMutation.mutate(
      { userId, from, to, granularity, dataset },
      {
        onSuccess: () => setOpen(false),
        onError: (mutationError) => setError(describeError(mutationError)),
      },
    )
  }

  return (
    <div className="relative">
      <Button
        type="button"
        size="sm"
        aria-expanded={open}
        aria-haspopup="menu"
        loading={exportMutation.isPending}
        onClick={() => setOpen((value) => !value)}
      >
        {exportMutation.isPending ? 'Exporting…' : 'Export CSV'}
      </Button>

      {open && (
        <div role="menu" aria-label="Export CSV" className="absolute right-0 z-10 mt-[var(--space-1)] flex w-[180px] flex-col gap-[2px] rounded-md border border-border bg-surface-raised p-[var(--space-1)] shadow-raised">
          <button
            ref={firstItemRef}
            type="button"
            role="menuitem"
            className="cursor-pointer rounded-sm border-0 bg-transparent px-[var(--space-2)] py-[var(--space-1)] text-left text-[length:var(--font-size-sm)] text-text hover:bg-accent-soft"
            onClick={() => handleExport('Buckets')}
          >
            Buckets
          </button>
          {isTeamScope && (
            <button
              type="button"
              role="menuitem"
              className="cursor-pointer rounded-sm border-0 bg-transparent px-[var(--space-2)] py-[var(--space-1)] text-left text-[length:var(--font-size-sm)] text-text hover:bg-accent-soft"
              onClick={() => handleExport('Members')}
            >
              Members
            </button>
          )}
          <button
            type="button"
            role="menuitem"
            className="cursor-pointer rounded-sm border-0 bg-transparent px-[var(--space-2)] py-[var(--space-1)] text-left text-[length:var(--font-size-sm)] text-text hover:bg-accent-soft"
            onClick={() => handleExport('Tasks')}
          >
            Tasks
          </button>
        </div>
      )}

      {error !== null && (
        <p role="alert" className="absolute right-0 mt-[var(--space-1)] w-[240px] text-[length:var(--font-size-xs)] text-danger">
          {error}
        </p>
      )}
    </div>
  )
}
