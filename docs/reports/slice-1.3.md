# Slice 1.3 — Reopen + notification store

Branch: `feature/f01-reopen-notify`, stacked on `feature/f01-move` (now merged to `main`; branched from `main`).

## Scope

Closes: F01 AC6, AC21–AC23, D6, D7; F04 AC6. Fully within the signed-off contract (`docs/features/f01-workflow.md` T1–T5, T3's `Task.Reopen.*` codes) — no new decision needed, self-approved per workflow.md gate 2.

- `PUT api/v1/tasks/{id}/reopen` — policy `ReopenTasks`. Request: `Reason` `[Required][MaxLength(500)]`, `Version` `[Required] Guid?`.
- Service `TaskService.Reopen`: existence → version → `TaskTransitions.FindReopen(Done, Review)` (AC13 stays intact since reopen validates against `Reopens`, not `Moves`) → clears `CompletedAt` → recomputes `IsOverdue` via `TaskOverdueRule.IsOverdue` (AC23) → writes `TaskHistory { Event = Reopened, Reason }` (F04 AC6, reason verbatim) → if the task becomes overdue by this recompute, a second `OverdueMarked` history entry with `ActorUserId = actorUserId` (ROADMAP T7 assumption, approved) — both in the same `SaveChanges()`.
- Concurrency: same two-catch pattern as `Move` (`DbUpdateConcurrencyException` + `IsDeadlock` unwrap for MySQL 1213) per R13's instruction to reuse it verbatim, not reinvent — factor `IsDeadlock` into a shared static (e.g. `TaskConcurrencyHelper`) or keep duplicated if extraction risks touching `Move`'s already-reviewed code; prefer extraction only if it's a pure lift with no behavior change.
- `Task.Reopen.NotFound` / `.Conflict` / `.InvalidTransition` / `.Exception` — already named in T3; add to `TaskControllerMS`.
- AC22 (blank reason) is a shape error via `[Required]` on the request model — no service-level check needed, consistent with R1's `InvalidModelStateResponseFactory`.
- AC21 (Member without `ReopenTasks`) is the `[Authorize(Policy = "ReopenTasks")]` 403 — no code needed, verified by an HTTP test.

### Notification store (D7's plumbing, already written by Move in 1.2)

1.2 already inserts `TaskNotification` rows on `Review→InProgress`. This slice adds the **read side**, per T5 endpoints 13–14:

- `GET api/v1/notifications?isRead=&start=&length=` — policy `ViewTasks`, caller's own rows only (`RecipientUserId == actorUserId`), inner join to non-deleted `Tasks`, newest first.
- `PUT api/v1/notifications/{id}/read` — policy `ViewTasks`, idempotent, 404-as-NotFound (not Forbidden) for another user's notification — no existence leak.
- `NotificationControllerMS`: `Notification.GetList.Exception`, `Notification.MarkRead.NotFound` / `.Exception`.
- New `NotificationsController`, `INotificationService`/`NotificationService`, `NotificationMapper`, DTOs under `TaskFlow.BusinessLogic/Dtos/Notifications/`, response models under `TaskFlow.Api/Models/Responses/Notifications/`.

## Verification plan

- `dotnet build BE/TaskFlow.sln -c Debug`
- `dotnet test BE/TaskFlow.Tests` × 3 consecutive runs (flake check, per batch instructions)
- New tests: reopen happy path (AC6), AC21 (403), AC22 (shape/blank reason), AC23 (overdue recompute + OverdueMarked entry), AC13 still holds (Done rejects ordinary move), reopen conflict (stale version), reopen deadlock path (reuse Move's AC29-style two-connection test pattern if proportionate — otherwise unit-level conflict test is sufficient, since the deadlock mechanism itself is already proven in 1.2 and R13 just asks for the same catch shape, not a second full concurrency repro). Notification list (own rows only, isRead filter, newest-first) and mark-read (idempotent, 404 on someone else's id).

## Agent

Delegated to `backend-lead`. Files in scope: `TaskControllerMS.cs`, `NotificationControllerMS.cs` (new), `TaskService.cs` (add `Reopen`), `NotificationService.cs` (new) + interface, `TasksController.cs` (add reopen action), `NotificationsController.cs` (new), DTOs, response models, AutoMapper profile, `CoreDependencyConfiguration.AddServices` registration, tests under `BE/TaskFlow.Tests`.

## Result

Diff reviewed directly (not just the agent's report): `TaskService.Reopen` follows `Move`'s exact pattern — existence → version → `TaskTransitions.FindReopen` → mutate → bump `Version` → append `Reopened` history (reason verbatim, F04 AC6) and, only when the overdue recompute flips false→true, a second `OverdueMarked` entry actor'd to the reopening user — both inside the one `SaveChanges()`, guarded by the same two-catch concurrency pattern as `Move` (`DbUpdateConcurrencyException` + the MySQL-1213 deadlock unwrap, now shared via `TaskConcurrencyHelper.IsDeadlock`, a pure lift with no behavior change to `Move`). Notification read side (`GetList`, `MarkRead`) matches T5 endpoints 13–14: own rows only, non-deleted-task join via `TaskItem`'s existing query filter, idempotent mark-read, 404 not 403 on another user's notification. No migration needed (`dotnet ef migrations has-pending-model-changes` → none; entities/permissions already landed in 1.1/1.2).

One minor deviation flagged by the agent and accepted: new controller actions don't call `TrimData()`, matching this controller's existing `Create`/`Move` actions, which already don't either — followed the established code over the written T5 convention (`coding-standards.md` §1).

### Verification

```
Build:   PASS   dotnet build BE/TaskFlow.sln -c Debug → 0 Warning(s), 0 Error(s)
Tests:   PASS   dotnet test BE/TaskFlow.Tests, run 3x consecutively against real MySQL
                 run 1: 77 passed, 0 failed, 0 skipped (58s)
                 run 2: 77 passed, 0 failed, 0 skipped (56s)
                 run 3: 77 passed, 0 failed, 0 skipped (57s)
                 No flake. New tests: TasksReopenControllerTests, NotificationsControllerTests.
Checked: diff read in full by the orchestrator, not just the subagent's report.
Not covered: no additional manual HTTP exercise beyond the xUnit HTTP-level test suite (same
             level slice 1.2 was verified at).
```

### AC checklist

| AC | Status | Evidence |
|---|---|---|
| F01 AC6 (reopen happy path, reason stored) | PASS | `TasksReopenControllerTests` happy-path test, passing in all 3 runs |
| F01 AC21 (Member without ReopenTasks → 403) | PASS | same suite |
| F01 AC22 (blank/whitespace reason → shape error) | PASS | same suite |
| F01 AC23 (overdue recompute on reopen) | PASS | same suite |
| F01 D7 (notification read side) | PASS | `NotificationsControllerTests` |
| F04 AC6 (reason verbatim in history) | PASS | same test as AC6 |

Note: slice 1.2's environment blocker (no Docker/MySQL reachable in the orchestrator's first session) was resolved by the user starting `taskflow.mysql`; verification above ran after that, directly by the orchestrator, not just by the subagent.

Status: **Built**, not yet Done — awaiting the batch's slice-1.4 checkpoint and the user's final acceptance.
