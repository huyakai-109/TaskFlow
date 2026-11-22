import type { CSSProperties } from 'react'

/**
 * Recharts' `<Tooltip>` ships inline white/black defaults (`contentStyle`/
 * `labelStyle`/`itemStyle`) and an unthemed grey `cursor` rectangle — both
 * invisible-on-dark bugs found in the live dark-theme check (white box,
 * near-invisible title, a light-grey rectangle covering the plot on hover).
 * One token-only theme, reused by every chart in this feature instead of
 * repeating the same four props per chart.
 */
export const tooltipContentStyle: CSSProperties = {
  backgroundColor: 'var(--color-surface-raised)',
  border: '1px solid var(--color-border)',
  borderRadius: 'var(--radius-md)',
  color: 'var(--color-text)',
  fontSize: 'var(--font-size-xs)',
}

export const tooltipLabelStyle: CSSProperties = { color: 'var(--color-text-muted)' }
export const tooltipItemStyle: CSSProperties = { color: 'var(--color-text)' }

/** The hover-highlight behind the point/bar/line being inspected — `--color-column` reads as a subtle tint in both themes, never a hardcoded grey. */
export const tooltipCursor = { fill: 'var(--color-column)', stroke: 'var(--color-border-strong)', strokeDasharray: '3 3' }
