import { cleanup, fireEvent, render, screen } from '@testing-library/react'
import { afterEach, describe, expect, it } from 'vitest'

import { summarizeOnTimeVsLate } from '../utils/chartSummary'

import { ChartFigure } from './ChartFigure'

afterEach(() => cleanup())

describe('ChartFigure — §11.4 accessibility pattern', () => {
  it('shows the figcaption summary as the chart role="img" aria-label, and reveals the same numbers in the table on toggle', () => {
    const summary = summarizeOnTimeVsLate({ onTime: 12, late: 2, noDueDate: 1 })

    render(
      <ChartFigure
        title="On-time vs late"
        summary={summary}
        height={200}
        chart={<div data-testid="chart-body">12 / 2 / 1</div>}
        table={
          <table>
            <caption>On-time vs late</caption>
            <tbody>
              <tr>
                <td>12</td>
                <td>2</td>
                <td>1</td>
              </tr>
            </tbody>
          </table>
        }
      />,
    )

    expect(screen.getByText(summary)).not.toBeNull()
    expect(screen.getByRole('img', { name: summary })).not.toBeNull()

    // The table is not shown until the toggle is used.
    expect(screen.queryByRole('table')).toBeNull()

    const toggle = screen.getByRole('button', { name: 'Show table' })
    expect(toggle.getAttribute('aria-expanded')).toBe('false')

    fireEvent.click(toggle)

    expect(toggle.getAttribute('aria-expanded')).toBe('true')
    const table = screen.getByRole('table')
    // Same numbers as the chart ("12 / 2 / 1" above).
    expect(table.textContent).toContain('12')
    expect(table.textContent).toContain('2')
    expect(table.textContent).toContain('1')
  })
})
