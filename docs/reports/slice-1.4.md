# Slice 1.4 — Reads, edits, delete, history read

Branch: `feature/f01-reads-edits`, stacked on `feature/f01-reopen-notify` (merged into this branch's history by being its parent — not yet merged to `main`).

## Scope

Closes: F04 AC3–AC5, AC9, AC15–AC21, AC23; F03's shape (not its order — F03 owns `ORDER BY`/page sizes later, additive); the remaining F01 primitives: `GET /tasks/board`, `GET /tasks` (list), `PUT /tasks/{id}` (title/desc, no history per T5 row 5), `PUT /tasks/{id}/priority`, `PUT /tasks/{id}/due-date`, `PUT /tasks/{id}/assignee`, `DELETE /tasks/{id}`, `GET /tasks/{taskId}/history`. Fully within the signed-off contract (`docs/features/f01-workflow.md` T1–T5, `docs/features/f04-audit.md` T1–T4) — self-approved, no new decision needed.

### Endpoints (T5 table, rows 3, 4, 5, 6, 7, 8, 11, 12)

- `GET /tasks/board` — always five columns in lifecycle order, empty ones present with zero totals; F01 ships `CreatedAt asc, Id asc` and 50/column (F03 changes the order-by and page sizes later, not the shape).
- `GET /tasks?status=&start=&length=` — page 2+ of one column.
- `PUT /tasks/{id}` — Title/Description, `Version`, **no history entry** (contract says so explicitly — out of F04 scope).
- `PUT /tasks/{id}/priority` — `Priority [Required][EnumDataType]`, `Version` → `PriorityChanged` entry (F04 AC4).
- `PUT /tasks/{id}/due-date` — `DueDate DateOnly?` (explicit null clears), `Version` → `DueDateChanged` entry (F04 AC5, null represented explicitly) + `IsOverdue` recomputed in the same commit (reuse `TaskOverdueRule.IsOverdue`).
- `PUT /tasks/{id}/assignee` — `AssigneeId long?`, `Version` → `AssigneeChanged` entry (F04 AC3); validate the new assignee exists when non-null (`Task.Assign.AssigneeNotFound`, same check as `Create`). F02's capacity/AssigneeRequired codes are reserved, not implemented here — do not add capacity logic.
- `DELETE /tasks/{id}?version={guid}` — policy `DeleteTasks`, soft delete + `Deleted` entry in the same commit (F04 AC9, D6 — trail survives, untouched).
- `GET /tasks/{taskId}/history?start=&length=` — policy `ViewTasks` (D5, not a separate permission), oldest first `ORDER BY CreatedAt, Id` (AC18 tie-break). Existence check uses `IgnoreQueryFilters()` so a soft-deleted task's trail still reads (AC20) while a genuinely missing id still 400s `TaskHistory.GetList.TaskNotFound` (AC19). Actor name join also `IgnoreQueryFilters()` on `Users`. `message` built on read from `TaskHistoryMessages.cs` templates per F04 T3's `TaskHistoryRes` shape — carries no raw exception text/internal id/secret (AC24).

### Concurrency / history pattern for all four mutating endpoints (Update, UpdatePriority, UpdateDueDate, Assign, Delete)

Same shape as `Move`/`Reopen`: load tracked → existence → version check → mutate → bump `Version` → append the one history entry that applies (none for plain `Update`) → one `SaveChanges()` wrapped in the same two-catch concurrency pattern, reusing `TaskConcurrencyHelper.IsDeadlock` (R13 — every slice-1.4 write path must use the same two-catch shape as Move/Reopen, not `DbUpdateConcurrencyException` alone, per ROADMAP R13's explicit instruction to assign). Delete is the one exception noted in `CLAUDE.md`: soft delete resets the entry to `Unchanged` and flips `IsDeleted` via `UnitOfWork.SaveChangesSoftDelete` — the `Deleted` history row is a separate `Add`, still inside the same `SaveChanges()`.

### Error codes (already named in `docs/features/f01-workflow.md` T3 — add the structs)

`Task.Update.*`, `Task.UpdatePriority.*`, `Task.UpdateDueDate.*` (+ `.DueDateInPast` via `TaskOverdueRule`/F05 AC12 — actually DueDateInPast only applies on create per T3; for due-date edit just set `IsOverdue`, no past-date rejection is specified for edits — confirm against T3's table literally: `Task.UpdateDueDate.NotFound/.Conflict/.DueDateInPast/.Exception` IS listed, so edits DO reject a past due date the same way create does), `Task.Assign.*` (`.AssigneeNotFound` reserved code already named), `Task.Delete.*`, `TaskHistory.GetList.*`, `Task.GetList.Exception`, `Task.GetBoard.Exception`.

### F04 "nothing recorded on failure" (AC11–AC14)

Already structurally guaranteed by every service method validating before mutating and writing history only after all guards pass — no extra code, but worth one regression test per mutating endpoint confirming a refused request leaves history untouched.

## Verification plan

- `dotnet build BE/TaskFlow.sln -c Debug`
- `dotnet test BE/TaskFlow.Tests` × 3 consecutive runs (flake check)
- New tests per endpoint: happy path + AC-specific cases (AC3/4/5 entry contents, AC9 delete trail survives, AC15/17/18/19/20/21 history read shape and ordering, AC16 403, AC11–14 nothing-recorded-on-failure spot checks), board always-five-columns, list pagination.

## Mandatory QA gate

Per the batch instructions, `qa-lead` runs an independent check after this slice (first mandatory gate in the batch) — permission, concurrency and regression defects across the full F01+F04 surface built so far (1.1–1.4), not just this slice's diff.

## Agent

Delegated to `backend-lead`. Files in scope: `TaskControllerMS.cs` (new structs), `TaskHistoryControllerMS.cs` (new), `TaskService.cs` (add `Update`, `UpdatePriority`, `UpdateDueDate`, `Assign`, `Delete`, `GetList`, `GetBoard`), `TaskHistoryService.cs` (new) + interface, `TaskHistoryMessages.cs` (new), `TasksController.cs` (add actions), `TaskHistoryController.cs` (new), DTOs, response models, AutoMapper profiles, `CoreDependencyConfiguration.AddServices`, tests under `BE/TaskFlow.Tests`.

## Result

Diff reviewed directly. All endpoints follow the Move/Reopen pattern exactly (existence → version → mutate → bump `Version` → history entry where applicable → one `SaveChanges()` with the shared two-catch concurrency guard via `TaskConcurrencyHelper.IsDeadlock`). `Delete` correctly does not bump `Version` (soft-delete path resets the entry to `Unchanged`, per `CLAUDE.md`'s *Soft delete* note) but still writes `Deleted` in the same commit. `GetBoard`/`GetList` share a new `BuildSummariesAsync` helper that batches assignee-name lookups (no N+1). The only shared-code touch is a pure extraction of `BuildAllowedTransitions` out of `BuildDetail` — confirmed a no-op for `Move`/`Reopen` (all of slice 1.2/1.3's existing tests still pass unchanged across all 3 runs).

One accepted judgment call beyond the contract's literal text: `TaskHistoryMessages`/`TaskHistoryService` resolve the `AssigneeChanged` entry's old/new user ids to display names for the rendered `message` (not just the actor, which is all T3 explicitly requires) — reasoned from F04 AC24 ("no internal identifier" in a rendered message). Accepted as a sensible, contained reading of AC24; structured `fromValue`/`toValue` columns still store raw ids per T2, so nothing stored changed.

### Verification

```
Build:   PASS   dotnet build BE/TaskFlow.sln -c Debug → 0 Warning(s), 0 Error(s)
Tests:   PASS   dotnet test BE/TaskFlow.Tests, run 3x consecutively against real MySQL
                 run 1: 98 passed, 0 failed, 0 skipped (58s)
                 run 2: 98 passed, 0 failed, 0 skipped (76s)
                 run 3: 98 passed, 0 failed, 0 skipped (73s)
                 No flake. All of 1.1–1.3's prior tests still pass alongside the 21 new ones.
Checked: diff read in full by the orchestrator; build and all 3 test runs executed directly by
         the orchestrator (not just reported by the subagent).
Not covered: no additional manual HTTP exercise beyond the xUnit HTTP-level suite.
```

### AC checklist

| AC | Status |
|---|---|
| F04 AC3, AC4, AC5, AC9 | PASS |
| F04 AC15–AC21, AC23 | PASS |
| F03 board/list shape (five columns, zero totals when empty; order/page-size left at F01 defaults) | PASS |
| F01 remaining primitives (Update/UpdatePriority/UpdateDueDate/Assign/Delete/GetList/GetBoard) | PASS |
| AC11–AC14 (nothing recorded on a refused request) | PASS — representative spot checks only, as scoped |

Status: **Built**, not yet Done. Next: mandatory `qa-lead` independent check across the full F01+F04 surface (1.1–1.4), per the batch's hard gate before stop point B.
