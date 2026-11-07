# Slice 5.4 — Overdue view (frontend)

Branch: `feature/f05-overdue` (shared with 5.1–5.3), branched from `docs/f05-design`.

Status: **Built** — implemented per this plan, typecheck/lint/build all pass (below). No live-browser pass performed (no backend/dev server reachable in this environment — see Verification). Awaiting Gate C (`review-change` + `qa-lead`, covering 5.3 and 5.4 together) before any push/merge/Done.

## Scope

Closes: the client side of T9 — "Overdue now" view only, per the user's approved sign-off (no "due today", "flag cleared this week", "last check", or "late by N days"). Board chip fixed to use the real total instead of page-1 count.

## Plan

1. **`getOverdueTasks(start, length, signal)`** in `FE/src/api/tasks.ts` → `GET /tasks?isOverdue=true&start=&length=`, following the exact pattern of the existing `getTaskPage(status, start, length)` from F03 (slice 4.3) — same client, same envelope unwrapping, same error handling via `ApiError`.
2. **`FE/src/shared/hooks/useOverdueTasksQuery.ts`** — `useInfiniteQuery` keyed `['board', 'overdue']` (T9: deliberately under the `'board'` prefix so every existing `invalidateQueries(['board'])` call — move, create, priority, due date, assign, reopen — also refreshes this view, exactly like F03's `useColumnPagesQuery` precedent). `length` fixed at 50, sent explicitly (never a literal duplicated elsewhere — reuse the board's existing pagination constant if one exists, or declare one next to this hook if not). Lives in `shared/` because both `features/board` and the new `features/overdue` read it (oxlint's `shared → features → app` import direction — `shared` must not import from either, so this hook only depends on `api/`).
3. **`FE/src/features/overdue/OverdueListPage.tsx`** — read `docs/ui.md` and the OverdueList wireframe in `docs/ui/screens/` before writing this. Title "Overdue tasks", the wireframe's rule copy, a table: Task (`#<id>` + title, links to `/tasks/:id`) · Status · Priority (icon + word, reuse existing priority rendering from the board/detail page — do not reinvent) · Due · Assignee, in server order (the backend already orders active-column style; do not re-sort client-side). States: loading, error with retry, empty ("Nothing is overdue."), success. "Show more" as a real `<button>` while `loaded < pagination.totalItems`, same pattern as F03's board "Show more". Scrolls inside its own container at 390px width (no horizontal page scroll — repo-wide responsive rule).
4. **Routing and nav** — new route `/overdue` in the router, nav links "Board" and "Overdue" in the app layout/header (check the existing nav component for the established pattern — likely `AppLayout.tsx` or similar; read it before adding).
5. **Board chip fix** — the existing "N overdue" chip on the board currently counts only the loaded/first page of each column (a pre-existing defect noted in the F03 design and ROADMAP). Change it to read `pagination.totalItems` from `useOverdueTasksQuery` (reusing the same hook this slice adds, so the chip and the `/overdue` page share one source of truth) and make the chip a link to `/overdue`.
6. **No new dependency.** No backend/contract change — this slice consumes 5.3's contract as-is.
7. **Verification**: `npm --prefix FE run typecheck`, `npm --prefix FE run lint`, `npm --prefix FE run build` must all pass. If a dev server + backend instance is reachable in your environment, do a live-browser pass (loading/empty/error/success/show-more at 390/768/1440) and say so; if not, say explicitly that this is NOT VERIFIED and why — do not claim a live pass you didn't actually do.

## Hard constraints
- Stay inside `docs/ui.md`'s "Overdue filter"/OverdueList scope — do not add "due today", "flag cleared this week", "last check", or "late by N days" (explicitly out of scope per the signed-off T9).
- Respect the import-direction rule: `features/overdue` may depend on `shared/` and `api/`; it must not import from `features/board` or vice versa. The shared hook belongs in `shared/hooks/`, not duplicated in each feature.
- No new dependency, no CSS framework change — Tailwind v4 + the existing design tokens only, matching the board/detail page's established look.
- Do not touch any backend file — this is a pure consumer of the 5.3 contract.
- Do not modify `CLAUDE.md`.
- Do not weaken, skip, or delete any existing test (if FE tests exist at this point; `n/a` is still the documented test command per `CLAUDE.md`, so this is likely just typecheck/lint/build + manual/browser verification).
- If anything here requires a contract change (a new field, a new query param, different pagination) STOP and ask — that would mean slice 5.3's contract was incomplete, which needs the user's decision.

## Gate C (after this slice)
Per the user: `review-change` + `qa-lead` cover slices 5.3 and 5.4 together at this gate. typecheck + lint + build must all pass; the report must name explicitly what still needs a real browser/backend pass that wasn't checked. No push, no merge, no `Done` status.

## Files changed

- `FE/src/api/tasks.ts` — new `getOverdueTasks(start, length, signal)` → `GET /tasks?isOverdue=true&start=&length=`, same pattern as `getTaskPage`.
- `FE/src/shared/hooks/useOverdueTasksQuery.ts` — new, `useInfiniteQuery` keyed `['board', 'overdue']`, fixed page length via its own `OVERDUE_PAGE_SIZE = 50` constant (no existing pagination constant to reuse — the board's 50/20 page sizes come from the server's `pagination.pageSize`, not a client constant).
- `FE/src/shared/components/PriorityIcon.tsx` — new, extracted from `TaskCard.tsx` at the Overdue view's second use (`frontend.md` §2) so both screens render the same priority icon from one place.
- `FE/src/features/board/components/TaskCard.tsx` — `PriorityIcon` removed (moved to `shared/components`), imports it instead; no behaviour change.
- `FE/src/features/overdue/OverdueListPage.tsx` — new. Title "Overdue tasks", the wireframe's rule copy, a table (Task/Status/Priority/Due/Assignee) in server order, loading/error-with-retry/empty ("Nothing is overdue.")/success states, "Show more" button while `loaded < pagination.totalItems`. Scrolls inside its own container (`overflow-x-auto`) at narrow widths — no page-level horizontal scroll.
- `FE/src/app/router.tsx` — new route `/overdue` → `OverdueListPage`.
- `FE/src/app/AppLayout.tsx` — nav `<nav aria-label="Primary">` with "Board"/"Overdue" `NavLink`s (active-state styling via `clsx`); notification bell and logout wrapped in an `ml-auto` group so the new nav sits next to the title as the wireframe's sidebar nav does, without introducing a new nav mechanism.
- `FE/src/features/board/BoardPage.tsx` — the "N overdue" chip now reads `overdueTasksQuery.data?.pages.at(-1)?.pagination?.totalItems` (via the new shared hook) instead of summing only the loaded page of each column (pre-existing defect, F03/ROADMAP), and is now a `Link` to `/overdue`.
- `FE/.oxlintrc.json` — added a `src/features/overdue/**` override (restricted from importing `board`/`task-detail`/`notifications`/`auth`/`app`) and added `**/overdue/**` to every existing feature override's restricted patterns, so the import-direction rule is enforced symmetrically in both directions.

No backend file touched; no new dependency.

## Verification

```
Typecheck: PASS  npm --prefix FE run typecheck → tsc -b --noEmit, no errors
Lint:      PASS  npm --prefix FE run lint → oxlint, 0 errors (one pre-existing warning,
                 react-hooks/exhaustive-deps in shared/hooks/useDialogA11y.ts, unrelated
                 to this change, not introduced by it)
Build:     PASS  npm --prefix FE run build → tsc -b && vite build, 128 modules, built
                 in ~1.3-1.6s, no errors
Checked:   NOT VERIFIED — no live-browser pass. No backend instance and no Docker/MySQL
           available in this environment (`docker` command not found; `GET /healthz`
           unreachable), so the API cannot be started to serve real overdue data, and
           no browser/Playwright tooling was available to drive `npm run dev` against
           it. Loading/empty/error/success/show-more at 390/768/1440px were not
           rendered or screenshotted — this needs a follow-up manual or qa-lead pass
           with a running backend before Gate C can close.
Not covered: live-browser states and responsive/keyboard checks (see above); FE has no
           test runner yet (`CLAUDE.md`: Vitest is "added later"), so there is no
           automated frontend test suite to run for this slice.
```

## Open issues / risks

- The live-browser pass (loading/empty/error/success/show-more, 390/768/1440px, keyboard operation) is outstanding — flagged for `qa-lead` or the user to run against a live backend before Gate C closes.
- `PriorityIcon` extraction to `shared/components` is a refactor inside `TaskCard.tsx`'s existing render output (no visual or behaviour change) — called out here since the brief's "touch only what the request needs" applies; it was the smallest way to satisfy "reuse existing priority rendering, do not reinvent it" without violating the `features/overdue` → `features/board` import-direction rule.
