export const NO_DATA_HATCH_PATTERN_ID = 'reports-no-data-hatch'
export const NO_DATA_HATCH_FILL = `url(#${NO_DATA_HATCH_PATTERN_ID})`

/**
 * §11.3 "Heatmap cell" row / §11.6 "Partially before data" — a bucket with
 * `hasData:false` inside an otherwise-available range is hatched
 * (`--color-dashed-border` stripes), not drawn as a real zero. Defined once
 * and reused by every chart that can show one, instead of a `<defs>` block
 * per chart.
 */
export function NoDataHatchPattern() {
  return (
    <defs>
      <pattern id={NO_DATA_HATCH_PATTERN_ID} patternUnits="userSpaceOnUse" width="6" height="6" patternTransform="rotate(45)">
        <rect width="6" height="6" fill="var(--color-surface)" />
        <line x1="0" y1="0" x2="0" y2="6" stroke="var(--color-dashed-border)" strokeWidth="3" />
      </pattern>
    </defs>
  )
}
