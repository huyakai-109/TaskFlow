interface LowSampleChipProps {
  sampleSize: number
  smallSampleThreshold: number
}

/** §11.5 "Small samples" — "Few data · {n}", meta 12, `warning-surface`/`warning`, with the title text spelled out for a screen reader/tooltip. */
export function LowSampleChip({ sampleSize, smallSampleThreshold }: LowSampleChipProps) {
  return (
    <span
      className="inline-flex w-fit items-center rounded-pill bg-warning-surface px-[var(--space-2)] py-[1px] text-[length:var(--font-size-2xs)] font-medium text-warning"
      title={`Based on only ${sampleSize} — fewer than ${smallSampleThreshold}; read with care.`}
    >
      Few data · {sampleSize}
    </span>
  )
}
