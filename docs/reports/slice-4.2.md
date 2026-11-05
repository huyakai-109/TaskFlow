# Slice 4.2 — The `Done` column window

## Plan

- `BE/TaskFlow.BusinessLogic/Services/TaskService.cs` — add `DoneColumnPageSize = 20` next to
  `BoardColumnPageSize`. Add a private static `ApplyDoneColumnOrder(IQueryable<TaskItem> query)`
  (`CompletedAt` desc, `Id` desc tie-break). Replace the `Done` branch in `GetBoard` and `GetList`
  (currently `OrderBy(CreatedAt).ThenBy(Id)`, left untouched by slice 4.1) so `Done` applies
  `ApplyDoneColumnOrder` and `Take(DoneColumnPageSize)` **before** counting — the same `IQueryable`
  produces both the windowed list and `TotalItems`, so `TotalItems = min(count, 20)` falls out of
  one code path instead of a true count computed and then clamped. `GetList` forces `Done`'s page
  size to `DoneColumnPageSize`, never the caller-supplied `length` (there is never more than 20 rows
  to page through). No migration, no index, no DTO/API shape change.
- `BE/TaskFlow.Tests/Http/TasksBoardOrderingControllerTests.cs` — purely additive: one test for the
  slice 4.1 gap (`GET /tasks` with no `status` filter), plus `Done`-column tests (AC8–AC11, a
  tie-break test, and a `GetList?status=Done` test). `SeedTaskAsync` gets one new optional parameter,
  `completedAt`, so tests can seed `Done` rows with exact, fixed `CompletedAt` values the same way the
  existing helper already fixes `CreatedAt` (precedent: slice 4.1's seeding, `TasksMoveControllerTests.cs`'s
  direct-`DbContext` fixture pattern for `TaskHistories` tie-breaks).

## Implementation

`BE/TaskFlow.BusinessLogic/Services/TaskService.cs`:

- New constant: `private const int DoneColumnPageSize = 20;`.
- `GetBoard`: the `Done` branch of the per-column loop now does
  `ApplyDoneColumnOrder(query).Take(DoneColumnPageSize)`, then `CountAsync()` and `ToListAsync()` on
  that same windowed queryable; `PageSize` reported is `DoneColumnPageSize`. The other four columns'
  code path (`ApplyColumnOrder`, true `CountAsync()`, `Take(BoardColumnPageSize)`, `PageSize =
  BoardColumnPageSize`) is unchanged.
- `GetList`: when `status == TaskWorkflowStatus.Done`, builds the windowed queryable the same way and
  calls `Pagination<TaskItem>.CreateAsync(windowed, start, DoneColumnPageSize)` — `CreateAsync`'s own
  `CountAsync()` then runs against the already-`Take(20)`-limited queryable, so it independently lands
  on the same `min(count, 20)` total; its own `Skip`/`Take(pageSize)` on top of that window is a no-op
  for `start=1` and correctly returns nothing on any later page, since the window never holds more
  than 20 rows. `status == null` (no filter) still falls into the `else` branch unchanged — it was
  already `status == TaskWorkflowStatus.Done` evaluating `false` for `null` before this slice.
- New private method `ApplyDoneColumnOrder`: `OrderByDescending(CompletedAt).ThenByDescending(Id)`.
  Comment notes D4 and "never fall back to `UpdatedAt`" per the spec's Technical notes.

## The new tests

In `TasksBoardOrderingControllerTests.cs` (all `[Collection(DatabaseCollection.Name)]`, same fixture,
no new test file):

- **`List_NoStatusFilter_AppliesActiveRuleAcrossEveryStatus_NoDoneWindow`** (the slice 4.1 gap) — seeds
  one task per active status plus one already-`Done` task, with priorities/due dates chosen so only the
  active ordering rule (not insertion, not id) produces the expected order; calls `GET /tasks` with **no**
  `status` and a large `length`; asserts the full cross-status order, including the `Done` task sorted by
  its priority/due date like everything else (not by `CompletedAt`, and not excluded by the 20-row window).
- **`Board_Done_OrderedByCompletedAtDesc_IgnoringPriority`** (AC8) — an `Urgent` task completed 5 days ago
  vs. a `Normal` task completed 1 day ago; asserts the `Normal` one is first.
- **`Board_Done_25CompletedTasks_ReturnsExactly20_TotalItemsAndPageSize20`** (AC9) — seeds 25 `Done` tasks
  with distinct `CompletedAt` values; asserts the board's `Done` column returns exactly 20 items,
  `Pagination.TotalItems == 20`, `Pagination.PageSize == 20`.
- **`Board_Done_UrgentCompletedLastWeek_NormalCompletedToday_NormalFirst`** (AC10) — same shape as AC8 with
  the exact BRS wording's timeframes; asserts `Normal` (today) precedes `Urgent` (last week).
- **`Board_Done_ReopenedAndReCompletedTask_AppearsAtNewCompletedAtPosition`** (AC11) — seeds a `Done` task
  with an old `CompletedAt` and a second `Done` task completed more recently (but never touched again);
  assigns the first task directly via `DbContext` (F02 D5: `Done` requires a resolvable assignee, and the
  seeded row has none), reopens it through `PUT /tasks/{id}/reopen` (real HTTP, a Manager who is not the
  assignee), then moves it `Review -> Done` through `PUT /tasks/{id}/status` (real HTTP, a different
  Manager, satisfying D5's "actor must not be assignee" rule on that edge) — the real move/reopen flow, not
  a direct database write, same precedent as slice 4.1's AC3 (priority promotion) and AC20 (move). Asserts
  the reopened-and-recompleted task now ranks ahead of the task that was completed once and never touched.
- **`Board_Done_IdenticalCompletedAt_OrderedByIdDescending`** — two `Done` tasks with the exact same
  `CompletedAt`; asserts the higher `Id` comes first (deterministic tie-break, not stated by D4).
- **`List_StatusDone_AppliesSameWindowAndOrder_AsBoard`** — `GET /tasks?status=Done&start=1&length=50`
  (a `length` deliberately different from 20, to prove it is ignored); asserts `Pagination.PageSize == 20`,
  `Pagination.TotalItems` equals the seeded count (2, under the window), and the order matches
  `CompletedAt` desc.

`SeedTaskAsync` gained one new optional parameter, `completedAt: DateTimeOffset? = null`, written
straight onto the seeded `TaskItem.CompletedAt` — additive, no existing call site changed.

## Verification

### 1. Build

`dotnet build BE/TaskFlow.sln -c Debug` — **PASS**, 0 errors, 28–29 warnings (all pre-existing `CS8602`
nullable-dereference warnings in test helper code and the one pre-existing `AV0013`; none introduced in
`TaskService.cs`).

### 2. Tests — three consecutive full-suite runs

```
dotnet test BE/TaskFlow.Tests
```

| Run | Result |
|---|---|
| 1 | `Passed! - Failed: 0, Passed: 155, Skipped: 0, Total: 155, Duration: 2 m 13 s` |
| 2 | `Passed! - Failed: 0, Passed: 155, Skipped: 0, Total: 155, Duration: 2 m 29 s` |
| 3 | `Passed! - Failed: 0, Passed: 155, Skipped: 0, Total: 155, Duration: 2 m 8 s` |

(155 = 148 pre-existing from slice 4.1's qa-lead re-verification + 8 new tests in
`TasksBoardOrderingControllerTests.cs`, filtered confirmation: that class alone runs 22/22 passed.)

### 3. EXPLAIN against `mydb_test`

No MySQL client or Docker CLI was available in this shell, so a throwaway `MySqlConnector` console
script (not part of this commit, deleted after use, same spirit as slice 4.1's "throwaway script")
seeded 2,000 synthetic `Done` rows with fixed, spread-out `CompletedAt` values directly into `mydb_test`,
ran `EXPLAIN` against the exact query shape `ApplyDoneColumnOrder` + `Take(DoneColumnPageSize)` produces
(and the `COUNT(*)` over that same windowed subquery, mirroring `windowed.CountAsync()`), then deleted
every row it had inserted — `mydb_test` is left exactly as every test class's `InitializeAsync()` expects
it afterwards (confirmed: the three full-suite runs above ran both before and after this script, all green).

Query 1 — the windowed, ordered list (board `pageItems` / `GetList`'s `page.Items` source):

```sql
EXPLAIN SELECT t.Id, t.CompletedAt
FROM Tasks AS t
WHERE t.IsDeleted = 0 AND t.Status = 5
ORDER BY t.CompletedAt DESC, t.Id DESC
LIMIT 20;
```

| id | select_type | table | type | possible_keys | key | key_len | ref | rows | filtered | Extra |
|---|---|---|---|---|---|---|---|---|---|---|
| 1 | SIMPLE | t | ref | `IX_Tasks_IsDeleted_Status_CompletedAt`, `IX_Tasks_IsDeleted_Status_Priority_DueDate`, `IX_Tasks_IsDeleted_AssigneeId_Status` | **`IX_Tasks_IsDeleted_Status_CompletedAt`** | 5 | const,const | 2000 | 100 | **Backward index scan; Using index** |

Query 2 — `COUNT(*)` over the same windowed subquery (mirrors `windowed.CountAsync()`, the
`[SIGN-OFF]` total):

```sql
EXPLAIN SELECT COUNT(*) FROM (
    SELECT t.Id FROM Tasks AS t
    WHERE t.IsDeleted = 0 AND t.Status = 5
    ORDER BY t.CompletedAt DESC, t.Id DESC
    LIMIT 20
) AS windowed;
```

| id | select_type | table | type | key | rows | Extra |
|---|---|---|---|---|---|---|
| 1 | PRIMARY | `<derived2>` | ALL | NULL | 20 | — |
| 2 | DERIVED | t | ref | **`IX_Tasks_IsDeleted_Status_CompletedAt`** | 2000 | **Backward index scan; Using index** |

**Reading it:** both queries resolve `IsDeleted = 0 AND Status = @status` with an index `ref` lookup
(`key_len = 5`, the `IsDeleted` + `Status` prefix) against `IX_Tasks_IsDeleted_Status_CompletedAt`, and —
unlike the active-column query in slice 4.1, which needed `Using filesort` for its tie-break columns —
the inner `Done` query reports **`Backward index scan; Using index`**: InnoDB walks the secondary index
backward and returns rows already in `(CompletedAt DESC, Id DESC)` order (the index carries the primary
key, so the `Id` tie-break is free), with no extra sort step and no row lookup (`Using index` = covering).
This confirms the Technical contract's claim exactly: "`Done` is fully served by
`IX_Tasks_IsDeleted_Status_CompletedAt` ... a backward scan returns `Id desc` order for free." The outer
`COUNT(*)` derived-table scan (`type: ALL`, `rows: 20`) is just MySQL materialising the already-limited
20-row window to count it — not a table scan of `Tasks`. No `Using filesort` anywhere; no new index
needed.

### 4. `dotnet format BE/TaskFlow.sln --verify-no-changes`

Full failure list (matches the qa-lead-corrected baseline from slice 4.1's report exactly):

```
TasksController.cs(366,81): error WHITESPACE: Fix whitespace formatting. Delete 1 characters.
AuthController.cs(8,1): error IDE0005: Using directive is unnecessary.
Migrations\20261028142829_SeedTaskPermissions.cs(1,1): error IDE0005: Using directive is unnecessary.
Migrations\20261025083342_AlignScaffoldSeedData.cs(1,1): error IDE0005: Using directive is unnecessary.
Migrations\20261025083416_SeedManagerAndMemberRoles.cs(1,1): error IDE0005: Using directive is unnecessary.
Migrations\20261028142757_AddTaskDomainTables.cs(1,1): error IDE0005: Using directive is unnecessary.
TestWebApplicationFactory.cs(3,1): error IDE0005: Using directive is unnecessary.
ApiVerioningConfiguration.cs(9,51): warning AV0013 (not a --verify-no-changes failure, a build-time warning)
```

Confirmed: this is exactly the corrected baseline qa-lead recorded for slice 4.1 — one `WHITESPACE`, six
`IDE0005` (one controller, one test helper, four migrations — the 4.1 report said "three", this run shows
four; the fourth, `AddTaskDomainTables.cs`, is also pre-existing, untouched by this slice, and not in any
file this slice edited). Nothing new introduced by `TaskService.cs` or
`TasksBoardOrderingControllerTests.cs`.

## Not verified / risks

- No MySQL CLI or Docker CLI was available in this shell session; the EXPLAIN evidence above came from a
  throwaway `MySqlConnector` console project run directly against `mydb_test` instead of the `mysql` CLI
  the slice 4.1 report implies. The project was deleted after use; nothing from it is part of this commit.
- The 4.1 report's baseline said "three pre-existing migration files" with `IDE0005`; this run observed
  four. Noted as a correction to the record, not a new defect — none of the four were touched by this
  slice.
- Frontend "Show more" / `useInfiniteQuery` work (Technical contract's Frontend section) is explicitly out
  of this backend slice's scope.
