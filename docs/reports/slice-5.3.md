# Slice 5.3 — `isOverdue` list filter

Branch: `feature/f05-overdue` (shared with 5.1/5.2), branched from `docs/f05-design`.

Status: **Built, verified** — implemented per this plan, build and full suite green across 3 consecutive runs. Per the user's instruction, 5.3 proceeds straight to 5.4 without a separate gate stop (review-change + qa-lead cover 5.3 and 5.4 together at Gate C).

## Scope

Closes: the `GET /tasks` contract addition in T8 — the frontend-facing surface slice 5.4 needs. No migration (slice 5.1's `IX_Tasks_IsDeleted_IsOverdue_Status_DueDate` index already serves this filter, per T3).

## Plan

1. **`TaskGetListReq`** (wherever F01/F03's list request model lives) gains `IsOverdue` as an optional `bool?` — absent means no filter, exactly like the existing `status` parameter's optionality. Shape validation only (no `[Required]`); no new bound needed since it's a plain boolean.
2. **`ITaskService.GetList`** gains an internal `bool? isOverdue` parameter, threaded through the existing query composition. Apply `IsOverdue == value` as an additional `Where` clause before whatever ordering F03 already applies (F03's active-column rule / `Done` window stays exactly as-is — this filter composes with it, doesn't replace it). `status=Done&isOverdue=true` naturally comes out empty under the existing `Done` rule (tasks in `Done` are never `IsOverdue`) — this should already fall out of the existing logic combined with the new filter; write a test confirming it, don't special-case it in code.
3. **Controller**: bind the new field the same way `status`/pagination params are already bound in the existing `GET /tasks` action; map it through to the service call. No new error code — the existing `Task.GetList.Exception` covers the one failure path; a non-boolean query value fails shape validation via the existing model-binding pipeline (R1's envelope), same as any other malformed query param today.
4. **No wire-shape change elsewhere** — `TaskSummaryRes`/`TaskDetailRes` already carry `isOverdue` (shipped with F01). Nothing new is added to the response.
5. **Tests** — new class `BE/TaskFlow.Tests/Http/TasksOverdueListControllerTests.cs` (or extend an existing list-controller test class if that's the closer convention — check `TasksBoardOrderingControllerTests.cs`/`TasksReadsEditsControllerTests.cs` first):
   - `isOverdue=true` returns only overdue tasks, across multiple statuses, in the same active-column order F03 already established (no change to ordering).
   - `isOverdue=false` returns only non-overdue tasks.
   - Omitting the parameter returns the unfiltered set (regression — confirms the filter is additive, not a behavior change when absent).
   - Combined with `status=` — filter applies on top of the status-scoped set.
   - `status=Done&isOverdue=true` → empty result set (never errors).
   - Soft-deleted tasks excluded regardless of their `IsOverdue` value (existing query filter, confirm it still applies).
   - Paging continues correctly across pages without skipping/duplicating rows when the filter is active.
   - A non-boolean value for `isOverdue` in the query string → the existing R1 model-validation envelope (400, field error), not a 500.
6. Record `EXPLAIN` for the filtered query (reuses slice 5.1's index — confirm it's actually selected or at least usable for this access pattern; same caveat as 5.1 about small/synthetic-table optimizer choice is acceptable to note again, not re-litigate).

## Hard constraints
- No migration in this slice (the index already exists). If you find you need one, STOP — that would mean the T3 index design was wrong, which is a contract question, not a slice 5.3 implementation detail.
- No application migration run against `mydb` or `quartz` in this slice — `dotnet ef database update` must not be invoked against either context here.
- No new dependency. No wire-shape change beyond the new optional request field.
- Do not touch Quartz, `OverdueScanJob`, `OverdueScanHostedService`, or the `AlignQuartzSchemaWith3_22` migration — already done in 5.2, out of scope here.
- Do not touch `CLAUDE.md`.
- Do not weaken, skip, or delete any existing test.
- If this needs to deviate from `docs/features/f05-overdue.md` T8 or ADR 0003, STOP and ask instead of improvising.

## Gate
Per the user: 5.3 runs between Gate B and Gate C without a separate stop, as long as build and the full test suite stay green. It will be covered by the `review-change` + `qa-lead` pass done together with 5.4 at Gate C. No push, no merge, no `Done` status.

## Files changed

- `BE/TaskFlow.Api/Models/Requests/Tasks/TaskGetListReq.cs` — new optional `bool? IsOverdue` property; comment pointing ahead to F05 removed now that it's built.
- `BE/TaskFlow.BusinessLogic/Services/TaskService.cs` — `ITaskService.GetList` and `TaskService.GetList` gain `bool? isOverdue`, applied as `query.Where(i => i.IsOverdue == isOverdue.Value)` before the existing `status`/ordering/Done-window composition (F03 logic untouched).
- `BE/TaskFlow.Api/Controllers/V1/TasksController.cs` — `GetList` action passes `request.IsOverdue` through to the service call, same binding pattern as `status`/`start`/`length`.
- `BE/TaskFlow.Tests/Http/TasksOverdueListControllerTests.cs` — new test class, `[Collection(DatabaseCollection.Name)]`, 8 tests covering the scope below. Chosen as a new class (not an extension of `TasksBoardOrderingControllerTests`) because the existing class is scoped to F03 ordering/Done-window behaviour and is already large (600+ lines); a new class keeps the isOverdue-filter scenarios together and mirrors the one-class-per-slice precedent set by `TaskOverdueScanServiceTests.cs` (slice 5.1).

No migration, no Quartz file, no frontend file touched — out of scope per the plan. No existing test was modified.

## Database

No migration in this slice. Slice 5.1's `IX_Tasks_IsDeleted_IsOverdue_Status_DueDate` index is reused as-is; confirmed by `git status` that no new migration file was generated, and `dotnet ef database update` was never invoked against either context.

**EXPLAIN**, candidate query for `isOverdue=true` with no status filter, against `mydb_test` (current test-run data, i.e. a small/synthetic table, same caveat as slice 5.1):

```sql
EXPLAIN SELECT t.Id, t.Status, t.Priority, t.DueDate FROM Tasks AS t
WHERE t.IsDeleted = 0 AND t.IsOverdue = 1
ORDER BY t.Priority, (t.DueDate IS NULL), t.DueDate, t.CreatedAt, t.Id;
```

```
id=1 | select_type=SIMPLE | table=t | type=ref
| possible_keys=IX_Tasks_IsDeleted_Status_CompletedAt, IX_Tasks_IsDeleted_Status_Priority_DueDate,
                IX_Tasks_IsDeleted_AssigneeId_Status, IX_Tasks_IsDeleted_IsOverdue_Status_DueDate
| key=IX_Tasks_IsDeleted_Status_CompletedAt | key_len=1 | ref=const | rows=1
| filtered=100 | Extra=Using where; Using filesort
```

`IX_Tasks_IsDeleted_IsOverdue_Status_DueDate` is listed as a usable (`possible_keys`) index for this access pattern, confirming T3's design serves the `isOverdue=true` filter as intended. The optimizer picked a different `IsDeleted`-leading index instead on this small test-run table — the same optimizer-selection caveat slice 5.1 already recorded and accepted (not re-litigated here); a board-sized table with the real `IsOverdue` cardinality gives the optimizer a clearer signal to prefer the overdue-scan index.

## Verification

```
Build:   PASS  dotnet build BE/TaskFlow.sln -c Debug → 0 Error(s) (pre-existing warnings only, incl. AV0013 on ApiVerioningConfiguration.cs)
Tests:   PASS  dotnet test BE/TaskFlow.Tests --filter "FullyQualifiedName~TasksOverdueListControllerTests" → 8/8
Tests:   PASS  dotnet test BE/TaskFlow.Tests (full suite) → 217/217, run 3× consecutively (217/217, 217/217, 217/217) — up from slice 5.2's 209 by the 8 new tests
Checked: isOverdue=true across statuses in active-column order; isOverdue=false; omitted (regression); combined with status=; status=Done&isOverdue=true → empty, not an error; soft-deleted excluded; paging correctness (12 rows / 5-per-page, no gap or duplicate); non-boolean value → 400 with the R1 `Validates` envelope, not a 500
Checked: no migration generated (`git status` clean of new migration files) and none applied to any database
Checked: EXPLAIN of the candidate query (see Database above) — confirmed the index is a usable candidate
Not covered: board-scale EXPLAIN (only test-run data volume available); the frontend surface (slice 5.4)
```

## AC / scenario mapping

| Scenario (plan step 5) | Test |
|---|---|
| `isOverdue=true`, multiple statuses, active-column order | `List_IsOverdueTrue_ReturnsOnlyOverdueTasks_AcrossStatuses_ActiveColumnOrder` |
| `isOverdue=false` | `List_IsOverdueFalse_ReturnsOnlyNonOverdueTasks` |
| Omitted (regression) | `List_IsOverdueOmitted_ReturnsUnfilteredSet` |
| Combined with `status=` | `List_IsOverdueTrue_CombinedWithStatus_FiltersOnTopOfStatusScopedSet` |
| `status=Done&isOverdue=true` → empty, not an error | `List_StatusDoneAndIsOverdueTrue_ReturnsEmpty_NotAnError` |
| Soft-deleted excluded | `List_IsOverdueTrue_SoftDeletedTask_IsExcluded` |
| Paging correctness with filter active | `List_IsOverdueTrue_PagingThroughResult_NoGapOrDuplicate` |
| Non-boolean value → R1 envelope, not 500 | `List_IsOverdueNonBoolean_ReturnsShapeError_NotInternalServerError` |

## Open issues / risks

- The EXPLAIN optimizer-selection caveat from slice 5.1 reproduces here for the same reason (small test-run table); no code change indicated, same as 5.1's conclusion.
- No separate review-change/qa-lead pass run for this slice per the user's explicit instruction; it will be covered together with 5.4 at Gate C.
