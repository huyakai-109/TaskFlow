import { useId, useState } from 'react'
import type { ReactNode } from 'react'

interface ChartFigureProps {
  title: string
  /** The one-sentence summary generated from the data (`utils/chartSummary.ts`) — both the visible figcaption text and the SVG's `aria-label`. */
  summary: string
  /** The Recharts root (or, for the heatmap/scatter, their own SVG) — never given its own `role`/`aria-label`; this wrapper owns both. */
  chart: ReactNode
  /** A real `<table>` with a `<caption>`, the same numbers as `chart` plus "View tasks" buttons (§11.4) — the keyboard/screen-reader path to drill-down. */
  table: ReactNode
  height: number
}

/**
 * The one accessibility pattern every chart in this feature shares
 * (`docs/design/SPEC.md` §11.4): `<figure>` → `<figcaption>` (title + text
 * summary) → the chart, wrapped so its `role="img"` and `aria-label` are the
 * same summary → a "Show table" toggle revealing the real `<table>`.
 */
export function ChartFigure({ title, summary, chart, table, height }: ChartFigureProps) {
  const [showTable, setShowTable] = useState(false)
  const tableId = useId()

  return (
    <figure className="m-0 flex flex-col gap-[var(--space-3)] rounded-card-lg border border-border bg-surface p-[var(--space-4)]">
      <figcaption className="flex flex-col gap-[2px]">
        <p className="m-0 text-[15px] font-semibold text-text">{title}</p>
        <p className="m-0 text-[length:var(--font-size-sm)] text-text-2">{summary}</p>
      </figcaption>

      <div role="img" aria-label={summary} style={{ height }}>
        {chart}
      </div>

      <button
        type="button"
        aria-expanded={showTable}
        aria-controls={tableId}
        className="w-fit cursor-pointer border-0 bg-transparent p-0 text-[length:var(--font-size-sm)] text-accent-ink underline"
        onClick={() => setShowTable((value) => !value)}
      >
        {showTable ? 'Hide table' : 'Show table'}
      </button>

      {showTable && (
        <div id={tableId} className="overflow-x-auto">
          {table}
        </div>
      )}
    </figure>
  )
}
