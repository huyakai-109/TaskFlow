# Slice 4.3 — Board "show more" per column

## Plan

- `FE/src/api/tasks.ts` — add `getTaskPage(status, start, length, signal?)` calling
  `GET /tasks?status=&start=&length=` (F01 T5 #4, already built, no backend change), following
  `getBoard`'s envelope-unwrapping style.
- `FE/src/features/board/hooks/useColumnPagesQuery.ts` — new `useInfiniteQuery` per column, keyed
  `['board', 'column', status]` so it's invalidated by every existing `invalidateQueries({ queryKey:
  ['board'] })` call (move, create, priority, due date, assign, reopen) with no mutation-hook change.
  `initialPageParam: 2`, `enabled: false` (manual `fetchNextPage`), `length` always equal to the
  caller-supplied `pageSize` (the board's own `pagination.pageSize` for that column), never a literal.
- `FE/src/features/board/utils/mergeColumnItems.ts` — board page-1 items + loaded extra pages,
  de-duplicated by `id`, first occurrence wins.
- `FE/src/features/board/components/BoardColumn.tsx` — fix the header count to read
  `pagination.totalItems` instead of `items.length`; add a real `<button>` "Show more" shown only
  while `items.length < totalItems`.
- `FE/src/features/board/BoardPage.tsx` — call `useColumnPagesQuery` once per fixed status (five
  explicit calls, not a loop, since `BOARD_COLUMNS` never changes shape — keeps the hook-call order
  stable across renders), merge each column's pages, pass the merged items + board pagination +
  `fetchNextPage`/`isFetchingNextPage` down to `BoardColumn`; extend `findTask` (used by the
  drag-and-drop handlers) to search the merged items of every column, not just board page 1.

## Implementation notes

- `getTaskPage`'s `start` is computed in the hook as `(pageNumber - 1) * pageSize + 1`, matching the
  1-based `start`/`length` vocabulary F01 T5 already finalised — page 2 of a 50-per-page column reads
  `start=51`.
- `Done`'s `pagination.totalItems` is `min(count, 20)` (F03 sign-off) and its `pageSize` is 20, so
  `items.length < totalItems` is never true once the board's own page 1 already holds all 20 rows —
  `Done` never shows "Show more", with no special case in the client.
- No `IntersectionObserver` sentinel was added — the design calls it optional, and the button alone
  satisfies AC15/accessibility; can be added later without touching the data layer.
- `mergeColumnItems` is a plain function, not a hook, so it can be called freely inside the render body
  and inside `findTask` without violating hook-call rules.

## Known limitation (accepted per the design, not fixed here)

`useMoveTaskMutation`'s optimistic update (`onMutate`) only touches the `['board']` query's own cache
(board page 1). A card that exists only in a loaded extra page (`['board', 'column', status]`) is found
correctly by the now-extended `findTask` for the purpose of *starting* and *completing* the drag (its
full `TaskSummary`, including `allowedTransitions` and `version`, is read from the merged list), and the
server-side move itself is unaffected — but the optimistic in-flight visual move only applies to page-1
cards; a page-2+ card does not visually jump to its new column until `onSettled`'s unconditional
`invalidateQueries(['board'])` refetch completes. This matches the brief's stated accepted limitation
and was not touched, per "no change to the optimistic move rollback logic beyond what's needed for the
query-key prefix invalidation to 'just work'".

## Verification

### 1. Typecheck

```
npm --prefix FE run typecheck
> tsc -b --noEmit
```

PASS, no output (no errors).

### 2. Lint

```
npm --prefix FE run lint
> oxlint
```

One warning, pre-existing and unrelated to this slice:

```
src/shared/hooks/useDialogA11y.ts:27:5: warning react-hooks(exhaustive-deps): React Hook useEffect has a missing dependency: 'initialFocusRef.current'
```

No new warnings or errors from any file touched in this slice.

### 3. Build

```
npm --prefix FE run build
> tsc -b && vite build
```

PASS — `✓ 125 modules transformed`, `✓ built in 1.68s`, no errors.

### 4. Real-browser pass

**NOT VERIFIED.** No backend instance (API + MySQL) was available in this session, so no dev server /
live-browser check was performed — no column with 55+ tasks was seeded or observed, "Show more" was not
clicked against a real response, and a page-2 card's draggability was not exercised live. This mirrors
slice 3.4's verification note (static checks only, explicitly stated rather than implied).

## Files changed

- `FE/src/api/tasks.ts` — added `getTaskPage`.
- `FE/src/features/board/hooks/useColumnPagesQuery.ts` — new.
- `FE/src/features/board/utils/mergeColumnItems.ts` — new.
- `FE/src/features/board/components/BoardColumn.tsx` — header count fix, "Show more" button, new props.
- `FE/src/features/board/BoardPage.tsx` — per-column infinite queries, merged items, extended `findTask`.

## Open issues / questions

None requiring a human decision. No backend or contract change was needed.
