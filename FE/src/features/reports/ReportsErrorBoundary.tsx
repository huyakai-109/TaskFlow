import { useRouteError } from 'react-router-dom'

/**
 * Route-level `errorElement` (bug fix, 2026-11-22) for the three Reports
 * routes (`router.tsx`) — any render error that still manages to escape the
 * defensive date handling in this feature shows this instead of the
 * router's bare default error screen. Nested under `AppLayout` like
 * `NotFoundPage`, so the sidebar/topbar stay visible, with the same "Reload"
 * recovery action `fix-bug` expects for an unexpected error: a full reload,
 * not an in-place retry, since the error may have left query/router state
 * inconsistent.
 */
export function ReportsErrorBoundary() {
  useRouteError()

  return (
    <section className="flex h-full min-h-0 flex-col items-center justify-center gap-[var(--space-3)] text-center">
      <h1 className="m-0 text-[length:var(--font-size-display)] font-semibold tracking-[-0.02em]">Something went wrong</h1>
      <p className="m-0 max-w-[480px] text-[length:var(--font-size-md)] text-text-muted">
        This report couldn&rsquo;t be displayed. Reloading the page usually fixes it.
      </p>
      <button
        type="button"
        className="mt-[var(--space-2)] inline-flex min-h-9 cursor-pointer items-center gap-[var(--space-2)] rounded-control-lg border-0 bg-accent px-[var(--space-4)] font-semibold text-on-accent hover:bg-accent-hover"
        onClick={() => window.location.reload()}
      >
        Reload
      </button>
    </section>
  )
}
