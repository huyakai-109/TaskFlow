# Slice 3.1 — Capacity guard on Move (the gate)

Branch: `feature/f02-move-capacity`, branched from `docs/f02-design`.

## Scope

Closes: F02 AC1–AC6, AC11, AC15, AC16 (move), AC18, AC19, AC20, AC21 (regression), AC22 (move). Fully within the approved contract (`docs/features/f02-capacity.md` → Technical contract, `docs/adr/0002-f02-capacity-token-on-user-concurrencystamp.md`, Accepted) — no new decision needed.

This session picked up work already written in a prior session (code and tests were already present, uncommitted, on the branch); the task here was to verify, fix what verification found, and commit — not to author the feature from scratch.

- `TaskCapacityRule` (`BE/TaskFlow.Common/Constants/TaskCapacityRule.cs`, new): `OccupiesSlot(status)` → `true` only for `InProgress` (D2); `AssigneeRequiredWhile(status)` → `true` for `InProgress`, `Review`, `Done` (D5, clarified 2026-11-05). Shared with the future Assign guard (slice 3.2).
- `TaskService.Move`: validation order extended to existence → version → same-status → transition → actor rule → `AssigneeRequired` (loads the assignee tracked; refuses if absent or unresolvable) → capacity (`CountActiveInProgress`, one indexed query `AssigneeId == target && Status == InProgress && Id != task.Id`, soft-deleted excluded via the existing query filter). On an allowed move into `InProgress`, the same tracked `User` row's `ConcurrencyStamp` is bumped in the same `SaveChanges()` as the task update + history (ADR 0002's token).
- New codes: `Task.Move.AssigneeRequired` (AC11), `Task.Move.CapacityReached` (AC2/AC6/AC22), both 400. `TaskControllerMS.Templates.CapacityReached` formats the envelope's `Error` field with the assignee's `userName` and the configured limit for `CapacityReached` only.
- `Workflow:MaxInProgressPerAssignee` config key (`ConfigKeys.Workflow.MaxInProgressPerAssignee`), value `3` in `appsettings.json`; `Program.cs` fails startup if missing or `< 1`, same pattern as the CORS allow-list check.
- New index `IX_Tasks_IsDeleted_AssigneeId_Status` on `Tasks (IsDeleted, AssigneeId, Status)` — migration `20261105045017_AddTasksAssigneeStatusIndex`.
- `TasksController.Move`: the service's 3-tuple result now carries an optional `ErrorMessage`; the controller copies it into the envelope's `Error` field only when non-empty (internal signature change, no wire contract change).
- Deadlock handling: no new code — Move's existing two-catch (`DbUpdateConcurrencyException` + `TaskConcurrencyHelper.IsDeadlock` for MySQL 1213, R13) already covers the new FK S-lock path introduced by writing `Users.ConcurrencyStamp` in the same commit.

## Files changed

| File | Reason |
|---|---|
| `BE/TaskFlow.Common/Constants/TaskCapacityRule.cs` (new) | `OccupiesSlot` / `AssigneeRequiredWhile` predicates |
| `BE/TaskFlow.Common/Constants/ConfigKeys.cs` | `Workflow.MaxInProgressPerAssignee` key |
| `BE/TaskFlow.Common/Constants/Messages/TaskControllerMS.cs` | `Move.AssigneeRequired`, `Move.CapacityReached`, `Templates.CapacityReached` |
| `BE/TaskFlow.BusinessLogic/Services/TaskService.cs` | `Move`'s guard chain extended; `CountActiveInProgress` helper; signature gains `ErrorMessage` |
| `BE/TaskFlow.Api/Controllers/V1/TasksController.cs` | copies `ErrorMessage` into the envelope when present |
| `BE/TaskFlow.Api/Program.cs` | fail-fast check for `MaxInProgressPerAssignee` |
| `BE/TaskFlow.Api/appsettings.json` | `Workflow:MaxInProgressPerAssignee = 3` |
| `BE/TaskFlow.DataAccess/Configurations/TaskItemConfiguration.cs` | new composite index |
| `BE/TaskFlow.DataAccess/Migrations/20261105045017_AddTasksAssigneeStatusIndex.cs` (new) + `.Designer.cs` (new) | the index migration |
| `BE/TaskFlow.DataAccess/Migrations/MyDbContextModelSnapshot.cs` | snapshot updated to match |
| `BE/TaskFlow.Tests/Http/TasksMoveControllerTests.cs` | new F02 capacity tests (AC1–AC6, AC11, AC18, AC19, AC20, AC22); one pre-existing test fixed (see below) |

## Database

Migration `20261105045017_AddTasksAssigneeStatusIndex` — **Additive**. Idempotent script generated and read (`dotnet ef migrations script --idempotent`): the only new statement is

```sql
CREATE INDEX `IX_Tasks_IsDeleted_AssigneeId_Status` ON `Tasks` (`IsDeleted`, `AssigneeId`, `Status`);
```

No table rewrite, no data touched, no existing index (`IX_Tasks_AssigneeId` included) modified. `Down()` drops the index. `dotnet ef migrations has-pending-model-changes` → "No changes have been made to the model since the last migration." **Not applied to any database** — generated and read only, per workflow.md §3.

## Defect found and fixed during verification

The first test run (`dotnet test --filter Move`) failed one pre-existing test: `Move_ThreeStagesInOneDay_WriteThreeSeparateHistoryEntries_InOrder` moved an unassigned task into `InProgress`, which is exactly the behaviour F02 AC11 now refuses (`Task.Move.AssigneeRequired`). This is the correct new behaviour per the approved contract, not a regression in the new code — the test predates F02 and never exercised the assignee rule. Fixed by giving the task an assignee before the move, since the test's actual subject is the three-stage history trail, not the no-assignee path. One fix, first attempt, confirmed green on rebuild — no second or third attempt needed.

## Verification

```
Build:   PASS   dotnet build BE/TaskFlow.sln -c Debug → 0 Warning(s), 0 Error(s)
Tests:   PASS   dotnet test BE/TaskFlow.Tests --filter "FullyQualifiedName~Move", run 3x consecutively
                 against real MySQL (mydb_test, via DatabaseFixture)
                 run 1: 52 passed, 0 failed, 0 skipped (1m 4s)
                 run 2: 52 passed, 0 failed, 0 skipped (1m 5s)
                 run 3: 52 passed, 0 failed, 0 skipped (1m 11s)
                 No flake, including the AC18 concurrency variants (a) two-MyDbContext interleave,
                 (b) HTTP Task.WhenAll different-tasks race, (c) actor = assignee 1213-deadlock path.
         PASS   dotnet test BE/TaskFlow.Tests (full suite, no filter), 1 run
                 114 passed, 0 failed, 0 skipped (1m 55s)
Checked: migration SQL generated and read (additive, CREATE INDEX only); model snapshot confirmed
         in sync (has-pending-model-changes → none); diff reviewed file-by-file against the approved
         contract and ADR 0002 before committing.
Not covered: CI has not run this branch. No independent qa-lead review yet. No manual/browser
         exercise of the endpoint beyond the xUnit HTTP-level test suite (same level prior F01/F02
         slices were verified at).
```

### AC checklist

| AC | Status | Evidence |
|---|---|---|
| AC1 (allows 3rd task at count 2) | PASS | `Move_AllowsThirdTask_WhenAssigneeHasExactlyTwoInProgress` |
| AC2 / AC22 (refuses 4th, distinct code, no history written) | PASS | `Move_Refuses_FourthTask_..._NamesAssigneeAndLimit_AndWritesNoHistory` |
| AC3 (allows when assignee has none) | PASS | `Move_Allows_WhenAssigneeHasNoTasksAtAll` |
| AC4 (slot freed by leaving InProgress) | PASS | `Move_Allows_WhenAssigneeDropsBelowLimit_AfterMovingOneToReview` |
| AC5, AC6, AC15, AC16, AC21 | PASS | remaining tests in the F02 block of `TasksMoveControllerTests.cs` |
| AC11 (no assignee → AssigneeRequired, distinct from CapacityReached) | PASS | dedicated test + the fixed pre-existing test now exercising an assigned task |
| AC18(a) two-connection interleave | PASS | 3/3 runs, no flake |
| AC18(b) HTTP concurrent race, ~10 rounds | PASS | 3/3 runs, no flake |
| AC18(c) actor = assignee, 1213 path | PASS | 3/3 runs, never a 500 |
| AC19 (configurable limit, N=2 in-memory config) | PASS | in-memory `TaskService` construction per `TasksMoveControllerTests.cs:392-397` precedent |
| AC20 (soft-deleted task excluded from count) | PASS | dedicated test |
| AC22 (CapacityReached distinguishable from InvalidTransition/AssigneeRequired/permission failure) | PASS | distinct error codes asserted across the F02 test block |

## Invariants held

- No push, no merge, no commit to `main`.
- Tests ran only against `mydb_test` (via `DatabaseFixture`); `mydb` untouched.
- Migration generated and read; **not applied** to any database.
- No `EnableRetryOnFailure`, no new retry logic.
- No new dependency.
- No secret, password or token logged or committed.
- No test or validation weakened to pass; the one test change tightened the test to match the approved new rule (AC11), not the reverse.

## Risks / known accepted items (not re-litigated here)

- R13's lock-upgrade deadlock (MySQL 1213) is now also reachable through the FK S-lock on `Users` from the capacity-token write; accepted per ADR 0002 and handled by the existing two-catch — no separate test beyond AC18(c), per design.
- AC17 ("deactivated account") stays out of scope — no user-management/deactivation feature exists yet (resolved open question, ROADMAP 2026-11-05).

Status: **Built**, not yet "Ready for Review" — awaiting independent `qa-lead` verification before that status change, and the user's final acceptance before "Done."

## Review

`Review: APPROVE WITH RECOMMENDATIONS on 4366bbd, 2026-11-05`

Re-verified by re-reading `git diff docs/f02-design..feature/f02-move-capacity` file-by-file against `docs/features/f02-capacity.md` Technical contract T1–T5 and ADR 0002; did not re-run the test suite (already green 3×/3× + full suite per *Verification* above, same commit, clean working tree).

### Must fix

None.

### Recommendations

1. `TaskTransition.RequiresAssignee` / `ConsumesCapacity` are now dead data, and diverge from what's actually enforced — `BE/TaskFlow.Common/Constants/TaskTransitions.cs:28-35`. `TaskService.Move` gates on `TaskCapacityRule.AssigneeRequiredWhile(toStatus)` (target-status-only, per the Technical contract), not on these per-edge flags — confirmed by `grep` that neither field is read anywhere in `BE/`. The `InProgress → Review` edge still declares `RequiresAssignee: false`, but a move into `Review` is in fact refused today without a resolvable assignee, since `Review` is in `AssigneeRequiredWhile`'s set. A future reader editing these flags expecting an effect, or "fixing" them to match reality, would be editing inert data next to the real rule. Fix: either remove the two fields from `TaskTransition` (and the matching comment in `docs/features/f01-workflow.md` T4) now that F02 fully supersedes them, or add a one-line comment on the record noting they're superseded by `TaskCapacityRule` for Move.
2. `TaskService.Move`'s capacity block uses `targetAssignee!.Id` (`BE/TaskFlow.BusinessLogic/Services/TaskService.cs:211`), relying on the unenforced invariant that `OccupiesSlot(status) ⇒ AssigneeRequiredWhile(status)`. Both predicates live together in `TaskCapacityRule.cs` today so the invariant is easy to keep, but a future edit to one without the other turns a would-be business refusal into a `NullReferenceException` (surfacing as `Task.Move.Exception`/500 instead of a clean error code). Low risk now; a one-line comment stating the dependency (or a defensive null-check) would make it self-protecting.

### Pre-existing / out of scope

- `TaskService.Move`'s new assignee lookup (`FindById` on `User`, line 199) follows the exact same pattern already used at lines 77, 145, 295, 396, 442, 498, 562, 591 for resolving `AssigneeId` — all of them rely on EF Core applying `UserConfiguration`'s `HasQueryFilter(!IsDeleted)` to a primary-key `FindAsync` lookup so a soft-deleted user resolves to `null`. This predates slice 3.1 and is not introduced by it; worth an explicit test before slice 3.2 relies on the same mechanism for AC17 (soft-deleted assignee id).

Not reviewed: CI run on this branch, independent `qa-lead` pass (next step), browser/manual exercise beyond the xUnit HTTP-level suite.
