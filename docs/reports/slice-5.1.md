# Slice 5.1 — F05 overdue scan service + index (the gate)

Branch: `feature/f05-overdue`, branched from `docs/f05-design`.

Status: **Ready for Review** — implemented per this plan, verified below, `review-change` APPROVE and `qa-lead` independent verification both complete (Gate A conditions met). Awaiting the user's sign-off before 5.2 starts.

## Scope

Closes: F05 AC1–AC6, AC16–AC23, AC25, AC26, and the data side of AC24. Regression tests closing AC7–AC9, AC11, AC13–AC15 (already met in code per T0, untested until now). Fully within the approved contract and ADR 0003 (Accepted) — no new decision needed.

## Plan

1. **`DateTimeHelper.ToDateInZone(this DateTimeOffset, string timezoneName) → DateOnly`** (T4) — new pure extension beside `ToTimezone`. `TaskService.TodayInZone()` becomes a one-line delegate to it; no other call site changes.
2. **`TaskOverdueScanResultDto`** (`BE/TaskFlow.BusinessLogic/Dtos/Tasks/`) — `Examined`, `Flagged`, `Skipped`, `Failed` (all `int`).
3. **`ITaskOverdueScanService` / `TaskOverdueScanService`** (`BE/TaskFlow.BusinessLogic/Services/TaskOverdueScanService.cs`), scoped, registered in `CoreDependencyConfiguration.AddServices`. `Run(DateOnly todayInZone, CancellationToken ct) → Task<TaskOverdueScanResultDto>` per T1: keyset candidate batches of 100 (no tracking, ids only, `!IsOverdue && Status != Done && DueDate != null && DueDate < today`, `ORDER BY Id`, `Id > lastId`), per-candidate tracked load + re-check with `TaskOverdueRule` + one `SaveChanges()` (task + `OverdueMarked` history, `ActorUserId = null`), `ChangeTracker.Clear()` in `finally`. Outcomes: written → `Flagged`; re-check false → `Skipped`; concurrency/deadlock (`DbUpdateConcurrencyException` or `TaskConcurrencyHelper.IsDeadlock`) → `Skipped` + `LogWarning`; any other exception → `Failed` + `LogError`, continue. End-of-run `LogInformation` with all four counters.
4. **`AddTasksOverdueScanIndex` migration** — `IX_Tasks_IsDeleted_IsOverdue_Status_DueDate` on `(IsDeleted, IsOverdue, Status, DueDate)` in `TaskItemConfiguration`. Generate, read the idempotent SQL, confirm additive (`CREATE INDEX` only) — **do not apply** to any database.
5. **D8 fix (T7)** — `TaskService.UpdateDueDate`'s past-date guard becomes `requestDto.DueDate.HasValue && requestDto.DueDate != task.DueDate && requestDto.DueDate < today` (only re-validates on an actual change). Closes F05 AC14.
6. **Tests** — new class `BE/TaskFlow.Tests/Http/TaskOverdueScanServiceTests.cs` (or similar), `[Collection(DatabaseCollection.Name)]`, resolving `ITaskOverdueScanService` from `TestWebApplicationFactory.Services` in a created scope (no `HttpContext`, same as the future job). Fixed fake "today" passed to `Run` per test (precedent `TasksReopenControllerTests.cs:129-134` for direct row seeding). Cases, all from ROADMAP Unit 5 / slice 5.1:
   - AC1/AC5 flagged (InProgress and Backlog, past due)
   - AC2 due == today not flagged
   - AC3 `Done` not flagged; AC4 no due date never flagged
   - AC25 soft-deleted never flagged, no history row
   - AC6 `ToDateInZone` correctness at a UTC/VN day boundary + a task flagged by the derived date
   - AC16/AC22 — several tasks, exactly one `OverdueMarked` entry each, `ActorUserId` is `null` (not `0`)
   - AC17 — re-run same day: `Flagged = 0`, no new history rows
   - AC18 — empty candidate set: all counters `0`, no error
   - AC19/AC26 — 1, 2, 30 days late: one entry each, no per-day duplication
   - AC20 — one task forced to fail (test-scoped MySQL `BEFORE INSERT` trigger on `TaskHistories`, dropped in `finally`) → that task `Failed`, rest `Flagged`
   - AC21 — HTTP move-to-`Done` racing `Run`, ~10 rounds: never a 500; task ends either `Done` with no `OverdueMarked`, or overdue with exactly one and the move returned `Conflict`
   - AC23 — flag → clear via due-date extension (AC7) → pass new date → second `Run` → second `OverdueMarked`
   - AC24 (data side) — two `Run` calls in parallel scopes over shared candidates, 5 rounds: each task exactly one entry, `Failed = 0`
   - One matrix test: every `TaskStatus` × {past, today, future, null} compared directly against `TaskOverdueRule.IsOverdue`
   - Regression: AC7–AC9, AC11, AC13–AC15 (due-date clear/extend, Done, reopen, no-op edit) against current `TaskService` behavior — these are expected to already pass per T0's audit; a failure here is a signal the audit was wrong, not a new rule
7. Record `EXPLAIN` for the candidate query against the new index.

## Out of scope for this slice

Quartz job/hosted service, the `QuartzContext` migration, scheduler-level AC24 (slice 5.2); the `isOverdue` list filter (5.3); the FE overdue view (5.4).

## Gate A (after this slice)

Per the user's hard-stop: scan tests green 3× consecutive, full `dotnet test` green, the scan-vs-user-write race test green on real MySQL, the double-run test green, `review-change` APPROVE, independent `qa-lead` verification. No push, no merge, no `Done` status (user only). Migration generated and read, not applied.

## Files changed

- `BE/TaskFlow.Common/Helpers/DateTimeHelper.cs` — new `ToDateInZone(this DateTimeOffset, string timezoneName) → DateOnly`, beside `ToTimezone`.
- `BE/TaskFlow.BusinessLogic/Services/TaskService.cs` — `TodayInZone()` delegates to `ToDateInZone` (one-line body); `UpdateDueDate`'s past-date guard now fires only when `requestDto.DueDate != task.DueDate` (D8/T7/AC14).
- `BE/TaskFlow.BusinessLogic/Dtos/Tasks/TaskOverdueScanResultDto.cs` — new, `Examined`/`Flagged`/`Skipped`/`Failed`.
- `BE/TaskFlow.BusinessLogic/Services/TaskOverdueScanService.cs` — new, `ITaskOverdueScanService.Run(DateOnly, CancellationToken)` per T1/T2.
- `BE/TaskFlow.Api/Configurations/CoreDependencyConfiguration.cs` — registers `ITaskOverdueScanService` scoped.
- `BE/TaskFlow.DataAccess/Configurations/TaskItemConfiguration.cs` — new index `IX_Tasks_IsDeleted_IsOverdue_Status_DueDate`.
- `BE/TaskFlow.DataAccess/Migrations/20261105145820_AddTasksOverdueScanIndex.cs` (+ `.Designer.cs`), `MyDbContextModelSnapshot.cs` — generated migration, not applied to any database.
- `BE/TaskFlow.Tests/Http/TaskOverdueScanServiceTests.cs` — new test class, `[Collection(DatabaseCollection.Name)]`, 45 tests covering the scope below.

No Quartz, `QuartzContext`, `isOverdue` list filter or frontend file was touched — out of scope per the plan.

## Database

- **Migration:** `AddTasksOverdueScanIndex` (`MyDbContext`). **Safety class: Additive.** The generated `Up()` is exactly one statement: `CREATE INDEX IX_Tasks_IsDeleted_IsOverdue_Status_DueDate ON Tasks (IsDeleted, IsOverdue, Status, DueDate)`. `Down()` drops the same index — correct and reversible.
- The idempotent script (`dotnet ef migrations script --idempotent`) was generated and read: the only new statement guarded by this migration's id is that one `CREATE INDEX`; nothing else changed. **Not applied to any database** — `mydb_test` received it automatically via `DatabaseFixture`'s own migration on test startup (the fixture's normal behaviour for every slice); `mydb` was never touched, and the API was not run with `AutoMigration` against this migration.
- **EXPLAIN**, candidate query, against `mydb_test` seeded with 500 synthetic rows (mixed `Status`/`DueDate`/`IsOverdue`), after `ANALYZE TABLE Tasks`:
  - **Optimizer's own choice** (no hint): `type=ref; key=IX_Tasks_IsDeleted_AssigneeId_Status; key_len=1; rows=500; filtered=2.7; Extra=Using index condition; Using where; Using filesort`. On this small, synthetically uniform table MySQL picked a different existing index that also leads with `IsDeleted` and costed the same at `ref=const`; it does not narrow further than the leading column either way.
  - **`FORCE INDEX (IX_Tasks_IsDeleted_IsOverdue_Status_DueDate)`**: `type=range; key_len=6; rows=365; Extra=Using where; Using index; Using filesort` — a genuine range scan on the new index, covering (`Using index`, no table lookup), examining fewer rows than the optimizer's own pick.
  - **Read as:** the new index does serve the query as designed (T3) once used, but the optimizer did not select it automatically on this small/synthetic dataset where several `IsDeleted`-leading indexes tie on cost. This is expected index-selection behaviour on a near-empty table, not a defect in the migration; a board-sized table with the real `IsOverdue`/`Status`/`DueDate` cardinality distribution gives the optimizer a clearer signal. No action taken beyond recording this — out of scope to force index selection in application code.

## Verification

```
Build:   PASS  dotnet build BE/TaskFlow.sln -c Debug → 0 Error(s) (pre-existing warnings only, incl. AV0013 on ApiVerioningConfiguration.cs)
Tests:   PASS  dotnet test BE/TaskFlow.Tests --filter "FullyQualifiedName~TaskOverdueScanServiceTests" → 45/45, run 3× consecutively (45/45, 45/45, 45/45)
Tests:   PASS  dotnet test BE/TaskFlow.Tests (full suite) → 200/200, run 3× consecutively (200/200, 200/200, 200/200)
Checked: AC21 race (scan vs HTTP move-to-Done, 10 rounds/run) and AC24 race (two parallel Run() calls over 20 shared candidates, 5 rounds/run) isolated and re-run 5× consecutively beyond the 3 full-suite passes above (so AC21's race executed ≈80 rounds total, AC24's ≈40 rounds total, across 8 independent process runs) — 0 failures
Checked: migration SQL read (dotnet ef migrations script --idempotent) — confirmed the only new statement is the CREATE INDEX; not applied to any database
Checked: EXPLAIN of the candidate query against the new index (see Database above) — confirmed the index serves the query (range scan, covering) when selected
Not covered: real-world-scale EXPLAIN (production-sized Tasks table) — only a 500-row synthetic seed was available; Quartz/job scheduling (slice 5.2, untouched here); the isOverdue list filter (5.3); the frontend (5.4)
```

## AC checklist

| AC | Evidence |
|---|---|
| AC1/AC5 | `Run_FlagsPastDueTask_InProgressAndBacklog` |
| AC2 | `Run_DoesNotFlag_WhenDueDateIsToday` |
| AC3 | `Run_DoesNotFlag_DoneTask_EvenWhenPastDue` |
| AC4 | `Run_DoesNotFlag_TaskWithNoDueDate` |
| AC6 | `ToDateInZone_ResolvesVietnamDate_NotUtcDate`, `Run_FlagsTask_UsingDateDerivedFromTheConfiguredZone` |
| AC7 (regression) | `Regression_ExtendingDueDateIntoTheFuture_ClearsOverdue_WithinTheSameRequest` |
| AC8 (regression) | `Regression_ApprovingIntoDone_ClearsOverdue_RegardlessOfHowLate` |
| AC9 (regression) | `Regression_RemovingDueDateEntirely_ClearsOverdue` |
| AC11 (regression) | `Regression_Reopen_RecomputesOverdue_FromCurrentDueDate_RatherThanCarryingItOver` |
| AC13 (regression) | `Regression_SettingDueDateToToday_IsAccepted_AndNotOverdue` |
| AC14 (D8 fix) | `UpdateDueDate_AllowsSameValuePastDate_BecauseItHasNotChanged`, `Update_OfUnrelatedField_Succeeds_OnATaskWithAnUntouchedPastDueDate` |
| AC15 (regression) | `Regression_CallerWithoutUpdateTasksPermission_CannotChangeDueDate` |
| AC16/AC22 | `Run_WritesExactlyOneOverdueMarkedEntryPerTask_WithNullActor` |
| AC17 | `Run_SecondRunSameDay_FlagsNothing_AndWritesNoNewHistory` |
| AC18 | `Run_WithNoCandidates_CompletesSuccessfully_WithAllCountersZero` |
| AC19/AC26 | `Run_FlagsTaskOnce_RegardlessOfHowManyDaysLate` (1/2/30 days) |
| AC20 | `Run_OneTaskFails_ViaInjectedTrigger_RestStillFlagged_AndFailureLogged` |
| AC21 | `Run_RacingHttpMoveToDone_NeverFails_AndEndsConsistent` (10 rounds/run) |
| AC23 | `Run_FlagsASecondTime_AfterDueDateWasExtendedAndHasPassedAgain` |
| AC24 (data side) | `Run_TwoParallelScans_OverSharedCandidates_EachTaskExactlyOneEntry_NoFailures` (5 rounds/run) |
| AC25 | `Run_DoesNotFlag_SoftDeletedTask_AndWritesNoHistory` |
| Status × date matrix | `IsOverdue_MatchesTheSharedRule_ForEveryStatusAndDateCombination` (5 statuses × 4 date cases) |

## Invariants held

- One shared predicate (`TaskOverdueRule.IsOverdue`) used by both the synchronous write paths and the scan — no duplicated rule.
- D1 (stored flag), D2 (synchronous recompute elsewhere, unchanged), D3 (per-task isolation, proven by AC20), D6 (Backlog included, AC5), D7 (re-marking after re-lapse, AC23) all hold.
- F04 AC8/AC22: `ActorUserId` is explicitly `null` for every scan-written entry, never inferred from `CreatedBy` (which `UnitOfWork` stamps `0` outside an `HttpContext`).
- T2's concurrency story: `DbUpdateConcurrencyException` and the MySQL 1213 deadlock (`TaskConcurrencyHelper.IsDeadlock`, shared with Move/Reopen/Assign) both resolve to `Skipped`, never a crash — exercised directly by the AC21 race.
- `ChangeTracker.Clear()` in a `finally` after every candidate — one bad row never poisons the next load.

## Review

`Review: APPROVE on d100bc6, 2026-11-05`. Re-verified build (0 errors), `TaskOverdueScanServiceTests` 45/45, full suite 200/200; read the scan service, the D8 fix, the index migration and its idempotent SQL, and confirmed no pre-existing test file was modified. No Must-fix, no Recommendations beyond the EXPLAIN observation already noted above.

## Independent QA verification (qa-lead, 2026-11-05)

**Verdict: READY, no Must-fix.** Independently re-ran everything from commit `d100bc6` rather than trusting the implementer's or reviewer's numbers: build 0 errors/0 warnings; `TaskOverdueScanServiceTests` 3× consecutive, 45/45 each; full suite once, 200/200 (2m59s); the AC21 race test isolated and re-run across 5 separate process invocations (≈50 rounds total) with 0 failures; the AC24 race test isolated and re-run across 5 separate process invocations (≈25 rounds total) with 0 failures. Read `TaskOverdueScanService.cs`, the D8 guard, `DateTimeHelper.ToDateInZone`, and the migration file directly (not just the diff) to confirm the idempotency re-check, the concurrency/deadlock catch order, the explicit `ActorUserId = null`, and the `ChangeTracker.Clear()` coverage in `finally` are all correct as claimed. Independently generated and read the idempotent migration script (read-only, nothing applied) — confirmed the tail for this migration id is exactly one `CREATE INDEX`. Confirmed via `git diff --stat` that only the new test class was added (no pre-existing test modified) and that no Quartz/`QuartzContext`/`isOverdue` filter/frontend file is present in this commit.

AC1–AC6, AC16–AC23, AC25, AC26, the data side of AC24, and the AC7–AC9/AC11/AC13–AC15 regressions — independently confirmed MET with evidence, not taken on the implementer's word.

One Observation carried forward (not a finding): the EXPLAIN optimizer-selection note above is expected on a near-empty synthetic table and correctly not treated as a blocker.

Not independently tested, and why: keyset batch-boundary behavior at >100 candidates (no test seeds that many rows; the pagination logic itself was read and matches the standard pattern used elsewhere in this codebase — low risk, worth a cheap follow-up test before 5.2 if convenient, not a Gate A blocker); real Quartz clustered-scheduling behavior (correctly out of scope for 5.1, deferred to 5.2); board-scale EXPLAIN (no production-sized table available in this environment).

## Open issues / risks

- The EXPLAIN result shows MySQL's optimizer did not pick the new index unprompted on a small synthetic dataset, even after `ANALYZE TABLE`; it does serve the query correctly when forced. Worth re-confirming with `EXPLAIN` against a board-sized table once real data volume exists (flagged for `qa-lead`/the user, not a code change).
- AC10 and AC12 are already covered by F01's own test suite (T0) and were not re-tested here, per the plan's scope.
- Quartz wiring, the `isOverdue` filter and the overdue view remain for slices 5.2–5.4 as planned.
