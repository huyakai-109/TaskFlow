# 0007 — Keyset paging for the Activity feed

- **Context:** The Activity feed ([f04-activity-feed.md](../features/f04-activity-feed.md)) reads an append-only table (F04 history) newest-first while rows are being written concurrently. The spec requires that paging never repeat or skip a row across reads (R8/AC8). Every existing list in the codebase uses 1-based offset `start`/`length` with `PaginationRes` (F01 T5, F03 D5 "no second paging style").
- **Decision:** `GET /activity` uses keyset paging on `(CreatedAt, Id)` descending, with an opaque cursor and a new `CursorPageRes { nextCursor, hasMore, pageSize }` envelope (`CursorResultRes<T>`). There is no total count. Offset paging remains the default for every other list.
- **Rule for future lists:** offset `start`/`length` stays the default for bounded, mutable lists (board columns, tasks, notifications, per-task history). Keyset paging is used only for an append-only stream read newest-first where new rows are expected during the read. The F05 overdue scan already uses keyset internally for its candidate batches; this ADR extends the same idea to a wire contract for the first time.
- **Consequences:**
  - A second paging style and a second response envelope type exist in the API. The frontend needs a second `useInfiniteQuery` shape (cursor-based) alongside the existing offset-based one.
  - No "N results" total is available; AC7/AC18 are stated in terms of `hasMore`, not a count.
  - No jumping to an arbitrary page number; only "next page from here."
- **Alternatives rejected:**
  - Plain offset `start`/`length`: a row written between two page reads shifts every row after it, producing duplicates on the next page — violates R8/AC8 directly.
  - Offset plus a pinned upper-bound id (e.g., "page 1's first id"): `CreatedAt` is stamped by the application at `SaveChanges`, `Id` is assigned at INSERT, so two concurrent commits can give a later id to an earlier timestamp; pinning the wrong id can still hide or repeat a row, and a transaction that commits late relative to when its id was reserved can still shift offsets under the pin.
  - Ordering by `Id` alone with offset: the same late-commit problem, since `CreatedAt` (the thing users actually sort by) can disagree with insertion order under concurrency.