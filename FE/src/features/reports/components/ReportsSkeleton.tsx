/** §11.6 "Loading" — 4 scorecard cards + chart frames at final size, no layout shift. Reused by both the Suspense fallback (route-level code-split chunk loading, §11.1) and the page's own query-pending state. */
export function ReportsSkeleton() {
  return (
    <section className="mx-auto flex w-full max-w-[1680px] flex-col gap-[var(--space-4)]" aria-busy="true">
      <p role="status" className="text-[length:var(--font-size-sm)] text-text-muted">
        Loading report…
      </p>
      <div className="grid grid-cols-1 gap-[var(--space-3)] min-[768px]:grid-cols-2 min-[1440px]:grid-cols-4">
        {[0, 1, 2, 3].map((index) => (
          <div key={index} className="h-[160px] animate-pulse rounded-card-lg border border-border bg-column" />
        ))}
      </div>
      <div className="grid grid-cols-1 gap-[var(--space-3)] min-[1440px]:grid-cols-2">
        <div className="h-[280px] animate-pulse rounded-card-lg border border-border bg-column min-[768px]:h-[260px] max-[390px]:h-[220px]" />
        <div className="h-[280px] animate-pulse rounded-card-lg border border-border bg-column min-[768px]:h-[260px] max-[390px]:h-[220px]" />
      </div>
    </section>
  )
}
