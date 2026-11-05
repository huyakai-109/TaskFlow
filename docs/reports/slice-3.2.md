# Slice 3.2 — Capacity guard on Assign

Branch: `feature/f02-move-capacity` (shared with 3.1/3.3/3.4, user decision 2026-11-05 — no new branch, no rebase, commits only added on top).

## Plan (written before building, per the batch-mode instruction — build proceeded without a separate approval round)

**Goal restated:** `PUT /tasks/{id}/assignee` enforces the same capacity rule Move enforces (slice 3.1), using the same shared `TaskCapacityRule` predicates, in the validation order the approved contract (`docs/features/f02-capacity.md` Technical contract T3) specifies for Assign specifically: existence → version → clearing-while-required (`AssigneeRequired`) → target exists (`AssigneeNotFound`, reused, no new code) → capacity (`CapacityReached`, new code) when the task occupies a slot and the target differs from the current assignee.

**Acceptance criteria closed:** F02 AC7–AC10 (reassignment + capacity), AC12–AC14 (D5 clarified: `InProgress`/`Review`/`Done` all refuse clearing), AC16 (assign, 403 has no side effect), AC17 (nonexistent + soft-deleted target id only — "deactivated" stays out of scope per the 2026-11-05 clarification), AC18 assign variants (two concurrent assigns to the same target; one assign racing one move for the same target), AC22 (assign, distinct error codes).

**Files expected to change:** `TaskService.cs` (interface signature + `Assign` body), `TaskControllerMS.cs` (two new `Assign` codes), `TasksController.cs` (copy `ErrorMessage` into the envelope, same pattern as Move), one new HTTP test file for the Assign capacity surface, one new permanent regression test file for the `FindById`/soft-delete mechanism AC17 depends on (review follow-up #1), a documentation-only comment in `TaskTransitions.cs` (review follow-up #2) and one more comment in `TaskService.Move` (review follow-up #3). No migration — the index Assign's capacity count needs (`IX_Tasks_IsDeleted_AssigneeId_Status`) was already added in slice 3.1 and covers both operations.

**Test approach:** a new `TasksAssignControllerTests.cs`, modelled directly on `TasksMoveControllerTests.cs` (same fixture/collection pattern, same `CreateAuthenticatedClientAsync`/`CreateTaskAsync`/`MoveAsync` helper shapes, duplicated per-file per the project's existing precedent of per-controller-area test files owning their own helpers). HTTP-level tests for every AC in scope; two direct-`TaskService` tests for the deterministic two-context interleave (AC18) alongside the HTTP-level concurrent variants, mirroring Move's AC18(a)/(b) split. Expected values (the capacity message text, the error codes) taken from the approved contract (T4's table) and from the already-accepted Move behaviour, not recomputed.

## Changes

- `BE/TaskFlow.BusinessLogic/Services/TaskService.cs`:
  - `ITaskService.Assign` signature gains `string? ErrorMessage` (same internal-only change T4 made to `Move` in 3.1).
  - `Assign` body: validation order now existence → version → `AssigneeRequiredWhile(task.Status)` when clearing (`Task.Assign.AssigneeRequired`) → target exists (`Task.Assign.AssigneeNotFound`, unchanged code) → capacity when `OccupiesSlot(task.Status)` and the target differs from the task's current assignee (`isNoOpReassignment` guard implements D3/AC9 — skips both the count and the `ConcurrencyStamp` bump). Reuses `CountActiveInProgress` (no duplicate query), same `ConcurrencyStamp` bump pattern as Move, same two-catch (`DbUpdateConcurrencyException` + `TaskConcurrencyHelper.IsDeadlock`) around the one `SaveChanges()`.
  - A new comment above the `targetAssignee!.Id` dereference in `Move`'s capacity block, naming the `OccupiesSlot ⇒ AssigneeRequiredWhile` invariant it depends on (review follow-up #3 — comment chosen over a defensive null-check or `Debug.Assert`, since the two predicates already live together in one small file and a comment is the smallest change that makes a future editor aware of the dependency).
- `BE/TaskFlow.Common/Constants/Messages/TaskControllerMS.cs`: `Assign.AssigneeRequired` and `Assign.CapacityReached` added (both 400, per T4's table). No existing code renamed or removed.
- `BE/TaskFlow.Api/Controllers/V1/TasksController.cs`: `Assign` action unpacks the 3-tuple and copies `ErrorMessage` into `response.Error` when present, identical to `Move`'s pattern from 3.1. Route, verb and `[Authorize(Policy = Permissions.Tasks.AssignTasks)]` unchanged — no wire-contract change beyond the additive `Error` field value, which was already possible on `Move`.
- `BE/TaskFlow.Common/Constants/TaskTransitions.cs`: one documentation-only comment above `TaskTransition`'s declaration, recording that `RequiresAssignee`/`ConsumesCapacity` are dead (confirmed by `grep`, nothing in `BE/` reads them) and that `TaskCapacityRule` is the actual source of truth (review follow-up #2). **Decision: comment only, fields kept** — removing them would touch every `Moves`/`Reopens` array literal and risk an F01 regression for no behavioural gain; the full test suite (130/130) is green with the fields present and dead, so there is nothing to validate by removing them in this slice. Does not touch `docs/features/f01-workflow.md` (out of scope, not a decision for this slice to make).
- `BE/TaskFlow.Tests/Http/TasksAssignControllerTests.cs` (new): the Assign capacity test surface — see AC checklist below.
- `BE/TaskFlow.Tests/DataAccess/UserRepositorySoftDeleteTests.cs` (new): permanent regression test for `BaseRepository<User>.FindById` honouring `UserConfiguration.HasQueryFilter(!IsDeleted)` from a fresh `DbContext` (review follow-up #1). Seeds a `User` directly (precedent: `ExampleSoftDeleteTests.cs`), soft-deletes it through `IUnitOfWork`/`BaseRepository<User>.Delete` + `SaveChanges()` (never the `IsDeleted` field set by hand — `backend.md` §6/§11, `CLAUDE.md` "Soft delete"), then asserts `FindById` returns `null` from a brand-new context.

No migration in this slice: slice 3.1 already added `IX_Tasks_IsDeleted_AssigneeId_Status`, which covers Assign's `CountActiveInProgress(assigneeId, taskId)` query the same way it covers Move's.

## Database

No new migration. Reused `IX_Tasks_IsDeleted_AssigneeId_Status` from `20261105045017_AddTasksAssigneeStatusIndex` (slice 3.1, Additive, not applied to any database in that slice either).

## Verification

```
Build:   PASS   dotnet build BE/TaskFlow.sln -c Debug → 0 Error(s) (pre-existing warnings only: AV0013, CS8618
                 in Pagination<T>, CS8602 in test files already present before this slice)
Tests:   PASS   dotnet test BE/TaskFlow.Tests --filter "FullyQualifiedName~Assign", run 3x consecutively
                 against real MySQL (mydb_test, via DatabaseFixture)
                 run 1: 36 passed, 0 failed, 0 skipped (1m 16s) [one assertion typo fixed before this run - see below]
                 run 2: 36 passed, 0 failed, 0 skipped (58s)
                 run 3: 36 passed, 0 failed, 0 skipped (1m 19s)
                 No flake, including both AC18 assign variants (two concurrent HTTP assigns to the same
                 target, one assign racing one move for the same target) and the deterministic
                 two-MyDbContext interleave.
         PASS   dotnet test BE/TaskFlow.Tests (full suite, no filter), 1 run
                 130 passed, 0 failed, 0 skipped (2m 20s) — up from 114 in slice 3.1 (16 new tests: 15 in
                 TasksAssignControllerTests + 1 in UserRepositorySoftDeleteTests)
Checked: diff reviewed file-by-file against the approved contract (f02-capacity.md T3/T4) before committing;
         confirmed via grep that TaskTransition.RequiresAssignee/ConsumesCapacity are read nowhere in BE/
         before adding the documentation comment; confirmed the existing index from 3.1 already covers
         Assign's count query (no new migration needed).
Not covered: CI has not run this branch. No independent qa-lead review yet (per the batch-mode instruction,
         not required before handoff for this slice). No manual/browser exercise of the endpoint beyond the
         xUnit HTTP-level test suite (same level slices 1.2-3.1 were verified at).
```

### Defect found and fixed during verification

First run of `--filter Assign` failed one new test, `Assign_Refuses_WhenTargetAlreadyAtCapacity`: the assertion expected the old assignee's active count to still read 3 after a refused reassignment, but the old assignee in that test only ever had 1 `InProgress` task (the one being reassigned) — a test-authoring mistake (wrong expected value), not an application defect. Fixed the assertion to `1`; confirmed green on the next run. One fix, first attempt, no second or third attempt needed.

### AC checklist

| AC | Status | Evidence |
|---|---|---|
| AC7 (refuses when target at capacity, check applies to new assignee) | PASS | `Assign_Refuses_WhenTargetAlreadyAtCapacity` |
| AC8 (allows at 2, target reaches 3, old assignee drops by one) | PASS | `Assign_Allows_WhenTargetHasTwoActive_AndOldAssigneeDropsByOne` |
| AC9/D3 (no-op reassignment to self at exactly 3 is allowed) | PASS | `Assign_Allows_ReassignmentToSameAssignee_AtExactlyThree` |
| AC10 (non-`InProgress` task: reassign allowed even if target at capacity) | PASS | `Assign_Allows_WhenTaskIsNotInProgress_EvenIfTargetIsAtCapacity` |
| AC12 (clearing refused — `InProgress`) | PASS | `Assign_Refuses_ClearingAssignee_WhenTaskIsInProgress` |
| AC13 (clearing refused — `Review`) | PASS | `Assign_Refuses_ClearingAssignee_WhenTaskIsInReview` |
| D5 clarified — clearing refused — `Done` | PASS | `Assign_Refuses_ClearingAssignee_WhenTaskIsDone` |
| AC14 (clearing allowed — `Backlog`, `Todo`) | PASS | `Assign_Allows_ClearingAssignee_WhenTaskIsInBacklog`, `..._WhenTaskIsInTodo` |
| AC16 (assign) (403 has no side effect on count) | PASS | `Assign_Refuses_WhenCallerLacksAssignTasksPermission_EvenWhenTargetIsAtCapacity_CountUnchanged` |
| AC17 (nonexistent id) | PASS | `Assign_Refuses_WhenTargetIdDoesNotExist_WithCodeDistinctFromCapacity` (pre-existing `Assign_RefusesUnknownAssigneeId` in `TasksReadsEditsControllerTests.cs` also still green, unchanged) |
| AC17 (soft-deleted id) | PASS | `Assign_Refuses_WhenTargetIdIsSoftDeleted_WithCodeDistinctFromCapacity` |
| AC18 (two concurrent assigns to the same target at 2) | PASS | `Assign_TwoConcurrentAssigns_OfTwoInProgressTasks_ToSameTargetAtTwo_ExactlyOneSucceeds`, 3/3 runs, no flake |
| AC18 (one assign + one move racing for the same assignee) | PASS | `Assign_OneAssignAndOneMove_RacingForSameAssignee_ExactlyOneWins_CountNeverExceedsLimit`, 3/3 runs, no flake |
| AC18 (deterministic two-`TaskService` interleave) | PASS | `Assign_TwoRacingSaveChanges_OnSeparateConnections_SameTarget_ExactlyOneSucceeds`, 3/3 runs, no flake |
| AC22 (assign) (capacity distinct from AssigneeRequired/AssigneeNotFound/permission failure) | PASS | distinct error codes asserted across the whole test block above |

### The three review follow-ups from slice 3.1

1. **`FindById`/soft-delete regression test** — done as `BE/TaskFlow.Tests/DataAccess/UserRepositorySoftDeleteTests.cs`, placed under `DataAccess/` (not merged into the Assign HTTP test file) because it tests a data-access-layer mechanism directly through `BaseRepository<User>`/`IUnitOfWork`, not through an HTTP endpoint — the same placement rationale as the existing `ExampleSoftDeleteTests.cs` in the same folder, which it follows closely as a precedent (seed the entity directly, soft-delete through `UnitOfWork`, read back from a fresh context). Ran as part of both the full suite and independently — passes.
2. **`TaskTransition.RequiresAssignee`/`ConsumesCapacity` dead data** — resolved with a documentation comment only (see *Changes* above). Chose this over removing the fields: removal would touch every edge literal in `Moves`/`Reopens` for a change with no behavioural effect (the fields are already unread), and the instruction was to fall back to the comment-only option if there was any risk to F01 tests from removal — removing two positional record fields used in nine `new(...)` calls is a mechanical but non-trivial diff I judged not worth the risk for a should-fix, not a must-fix.
3. **`targetAssignee!.Id` invariant comment in `Move`** — done, comment only (no `Debug.Assert`): the invariant is already easy to verify by reading `TaskCapacityRule.cs` (both predicates sit next to each other in one ~25-line file), so a comment pointing a future editor there is proportionate; an assert would duplicate logic already expressed by the two predicates existing side by side.

## Invariants held

- No push, no merge, no commit to `main`.
- No rebase, no history rewrite on `feature/f02-move-capacity` — commits only added on top.
- Tests ran only against `mydb_test` (via `DatabaseFixture`); `mydb` untouched.
- No migration generated in this slice (none needed); the one from 3.1 remains **not applied** to any database.
- No `EnableRetryOnFailure`, no new retry logic, no new dependency.
- No secret, password or token logged or committed.
- No test or validation weakened to pass; the one test fix (above) corrected a wrong expected value in a brand-new test, not an existing/passing one.
- ROADMAP status set to **Built**, not Ready for Review or Done — those are the orchestrator's/user's calls per the batch-mode instruction.

## Risks / known accepted items (not re-litigated here)

- Same R13 lock-upgrade deadlock risk as Move (MySQL 1213), now also reachable through `Tasks.AssigneeId`'s own FK S-lock on `Users` in addition to `TaskHistories.ActorUserId`/`TaskNotifications.RecipientUserId` — handled by the same existing two-catch, per ADR 0002/T2, exercised by the AC18 deadlock-adjacent variants above (no 500 observed in any of the 3 runs).
- "Deactivated account" (AC17's third clause) remains explicitly out of scope — no user-management/deactivation feature exists (ROADMAP 2026-11-05 clarification, unchanged by this slice).
- `TaskTransition.RequiresAssignee`/`ConsumesCapacity` remain present but dead, now documented as such — a future slice that touches `TaskTransitions.cs` for an unrelated reason may want to finish the removal; not done here per the smallest-safe-change call above.

Status: **Built** — not yet independently reviewed or accepted.

## Review

`Review: APPROVE WITH RECOMMENDATIONS on d553958, 2026-11-05`

Re-verified by re-reading `git diff a2344c4..fd649f1` (the 3.2 commits) file-by-file against `docs/features/f02-capacity.md` Technical contract T3/T4 and ADR 0002; did not re-run the full test suite (already green 3×/3× for the Assign filter + full suite, same base commits, clean working tree before the one fix below).

### Must fix

None.

### Recommendations

1. Assign's capacity block had the identical null-forgiving `targetAssignee!.Id` pattern as Move's (`TaskService.cs`, then line 626), but — unlike Move — carried no comment naming the invariant it depends on (`OccupiesSlot(status) ⇒ AssigneeRequiredWhile(status)`, combined here with "a clearing request for such a status already returned `AssigneeRequired` above"). **Fixed directly** (one-line addition, rebuilt clean, 0 errors) rather than sent back for a second round, since it was the exact follow-up #3 the user asked for, just not yet applied to this second occurrence of the same pattern.
2. The two recommendations carried over from slice 3.1's review (dead `TaskTransition.RequiresAssignee`/`ConsumesCapacity` fields; the invariant behind `targetAssignee!` in Move) were resolved as documented in *The three review follow-ups* above — comment-only in both cases, a reasonable smallest-safe-change call given removing the two record fields would touch nine edge literals for no behavioural gain.

### Pre-existing / out of scope

- None new. The `FindById`/soft-delete assumption flagged in 3.1's QA pass is now a permanent regression test (`UserRepositorySoftDeleteTests.cs`) rather than an open question.

Not reviewed: CI run on this branch, independent `qa-lead` pass (scheduled for gate B after slice 3.3, per the user's batch instructions), browser/manual exercise beyond the xUnit HTTP-level suite.
