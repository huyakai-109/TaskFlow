# Slice 4.1 — Active-column order (the gate)

## Plan

- `BE/TaskFlow.BusinessLogic/Services/TaskService.cs` — add one private static method
  `ApplyColumnOrder(IQueryable<TaskItem> query)` implementing the F03 active-column rule
  (`Priority` asc → "has no due date" asc → `DueDate` asc → `CreatedAt` asc → `Id` asc).
  Replace the `OrderBy(CreatedAt).ThenBy(Id)` call in `GetBoard` (around line 737) and in
  `GetList` (around line 768) with a conditional: `Done` keeps its current `CreatedAt`/`Id`
  ordering unchanged in this slice (`ApplyColumnOrder` is not called for `Done`); every other
  status calls the new `ApplyColumnOrder` method. Slice 4.2 replaces `Done`'s branch with its
  own `CompletedAt`-desc window.
- `BE/TaskFlow.Tests/Http/TasksBoardOrderingControllerTests.cs` — new test class, `[Collection(DatabaseCollection.Name)]`,
  resets `TaskNotifications`/`TaskHistories`/`Tasks` per test, seeds fixed-timestamp rows via `_dbFixture.CreateContext()`
  for ordering/tie-break/pagination cases, and exercises through HTTP for permission/priority-update/move cases.

## Verification

1. `dotnet build BE/TaskFlow.sln -c Debug`
2. `dotnet test BE/TaskFlow.Tests` — 3 consecutive runs, report pass/fail counts each time.
3. `EXPLAIN` the active-column query against `mydb_test` with the new `ORDER BY` — recorded below.
4. `dotnet format BE/TaskFlow.sln --verify-no-changes` — expect only the pre-existing AV0013 failure.

No migration, no index, no API/DTO change.

## EXPLAIN

Run against `mydb_test` with 1,500 synthetic rows spread evenly across all five statuses (so `Backlog` is a realistic ~280-row minority slice, not virtually the whole table) and across all three priorities, half with a `DueDate`, half `NULL`. Rows were inserted and deleted by a throwaway script (not part of this commit); `mydb_test` is left exactly as every test class's `InitializeAsync()` expects it afterwards.

Query shape (same as `ApplyColumnOrder` + the board's `Take(50)`):

```sql
EXPLAIN SELECT t.Id, t.Priority, t.DueDate, t.CreatedAt
FROM Tasks AS t
WHERE t.IsDeleted = 0 AND t.Status = 1  -- Backlog
ORDER BY t.Priority, (t.DueDate IS NULL), t.DueDate, t.CreatedAt, t.Id
LIMIT 50;
```

| id | select_type | table | type | possible_keys | key | key_len | ref | rows | filtered | Extra |
|---|---|---|---|---|---|---|---|---|---|---|
| 1 | SIMPLE | t | ref | IX_Tasks_IsDeleted_Status_CompletedAt, IX_Tasks_IsDeleted_Status_Priority_DueDate, IX_Tasks_IsDeleted_AssigneeId_Status | **IX_Tasks_IsDeleted_Status_CompletedAt** | 5 | const,const | 280 | 100 | **Using filesort** |

Count query (`TotalItems`, same `WHERE`, no `ORDER BY`):

| id | select_type | table | type | possible_keys | key | key_len | ref | rows | filtered | Extra |
|---|---|---|---|---|---|---|---|---|---|---|
| 1 | SIMPLE | t | ref | IX_Tasks_IsDeleted_Status_CompletedAt, IX_Tasks_IsDeleted_Status_Priority_DueDate, IX_Tasks_IsDeleted_AssigneeId_Status | **IX_Tasks_IsDeleted_Status_Priority_DueDate** | 5 | const,const | 280 | 100 | Using index |

**Reading it:** both queries resolve `IsDeleted = 0 AND Status = @status` with an index `ref` lookup (`key_len = 5` = the `IsDeleted` + `Status` prefix), not a full table scan — the equality half of the design's claim holds. The ordered query then does `Using filesort` for the remaining `Priority`/`DueDate`/`CreatedAt`/`Id` tie-break, exactly as the Technical contract accepts ("the tie-break columns sort in memory, accepted at board scale"). The optimizer picked `IX_Tasks_IsDeleted_Status_CompletedAt` rather than `IX_Tasks_IsDeleted_Status_Priority_DueDate` for the ordered query — both indexes share the same `(IsDeleted, Status)` prefix and `key_len`, so MySQL's cost estimate treats them as equivalent for this equality-only access pattern and picks one more or less arbitrarily; the count query (which can also use `Priority`/`DueDate` as a covering projection) does pick `IX_Tasks_IsDeleted_Status_Priority_DueDate` and reports `Using index` (covering, no row lookup). No `Using filesort` regression beyond what the design already expected; no new index needed.
