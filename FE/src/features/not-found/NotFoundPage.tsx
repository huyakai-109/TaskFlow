import { Link } from 'react-router-dom'

/**
 * Catch-all for any URL that matches no route (`router.tsx`'s trailing `*`),
 * including the removed `/inbox` path. Nested under `AppLayout` like every
 * other page (`docs/design/SPEC.md` §3) — sidebar and topbar stay visible —
 * rather than a bare full-page error screen, since `RequireAuth` already
 * gates this whole subtree: an unauthenticated visitor is redirected to
 * `/login` before this page is ever reached.
 */
export function NotFoundPage() {
  return (
    <section className="flex h-full min-h-0 flex-col items-center justify-center gap-[var(--space-3)] text-center">
      <h1 className="m-0 text-[length:var(--font-size-display)] font-semibold tracking-[-0.02em]">Page not found</h1>
      <p className="m-0 max-w-[480px] text-[length:var(--font-size-md)] text-text-muted">
        The page you&rsquo;re looking for doesn&rsquo;t exist or has moved.
      </p>
      {/*
       * A navigation link, not an action — stays an `<a>` (`frontend.md` §8
       * "`a` for navigation"), not the shared `Button` (native `<button>`
       * only). Classes match `Button`'s `primary`/`sm` output by hand so it
       * still looks identical to every other primary action.
       */}
      <Link
        to="/board"
        className="mt-[var(--space-2)] inline-flex min-h-9 cursor-pointer items-center justify-center gap-[var(--space-2)] rounded-md border-0 bg-accent px-[var(--space-3)] text-[length:var(--font-size-sm)] font-semibold text-on-accent no-underline hover:bg-accent-hover"
      >
        Back to board
      </Link>
    </section>
  )
}
