# F02 — Resource Capacity Guard

| | |
|---|---|
| **Source** | `docs/brs/taskflow-brs.md` § F02 |
| **Status** | Planned — all open questions resolved 2026-10-20 |
| **Depends on** | [F01](f01-workflow.md) — task model, permissions, the move operation |
| **Related** | [F04](f04-audit.md) — an accepted reassignment is recorded |

**Goal** — no team member may hold more than 3 tasks in `InProgress` at once, so nobody is silently overloaded.

## Scope

**In scope** — counting an assignee's `InProgress` tasks and refusing any move or reassignment that would push them past the limit; freeing the slot as soon as a task leaves `InProgress`; keeping an assignee attached while a task is active.

**Out of scope** — per-team or per-role limits, temporary overrides, a queue of pending assignments, warning before the limit is reached, reporting on team load.

## Decisions

- **D1** the limit is **3 by default, read from configuration**, through one declared key constant — never a literal at a call site.
- **D2** the count includes only tasks that are `InProgress`, not soft-deleted, and assigned to that user. `Backlog` / `Todo` / `Review` / `Done` consume no slot.
- **D3** when the task being moved or reassigned is itself already `InProgress` and assigned to the target user, it is excluded from the count — otherwise a no-op reassignment would be refused at exactly 3.
- **D4** **no role bypasses the limit, including Admin.** There is no override flag and no emergency path.
- **D5** an assignee is **mandatory from `InProgress` onward**: a task cannot enter `InProgress` unassigned, and the assignee cannot be cleared while the task is `InProgress`, `Review` **or `Done`** (clarified 2026-11-05, *Technical contract* T4 — "from `InProgress` onward" reads as every status reachable from it, so that reopening a `Done` task into `Review` never produces an unassigned `Review` task). Reassignment to a different member is always allowed subject to capacity.
- **D6** only Manager / Admin may delete a task (`DeleteTasks`); soft-deleting an `InProgress` task frees its assignee's slot, since the count excludes deleted rows.
- **D7** the race in AC18 is prevented with a **concurrency token on the assignee row**, changed in the same `SaveChanges()` as the task (`backend.md` §4, Recipe 14) — not with an application-level `Count()` alone.

## Acceptance criteria

**Happy path and the limit**

- **AC1** Given member A has 2 tasks in `InProgress`, when a caller moves a third task to `InProgress` with A as assignee, then the move is allowed and A has 3.
- **AC2** Given member A has 3 tasks in `InProgress`, when a caller tries to move a fourth to `InProgress` with A as assignee, then the move is refused with a "capacity reached" error naming A and the limit, the status and assignee are unchanged, and no history entry is written.
- **AC3** Given member A has no tasks at all (the empty case), when a caller moves a task to `InProgress` with A as assignee, then the move is allowed.
- **AC4** Given member A has 3 in `InProgress` and moves one to `Review`, when a caller then assigns a new task to A, then the move is allowed because A is now at 2.
- **AC5** Given member A has 3 in `InProgress` and one is approved into `Done`, when a caller then assigns a new task to A, then the move is allowed — leaving `InProgress` in either direction frees the slot.
- **AC6** Given an Admin whose own assignee is at 3, when they attempt the move, then it is refused exactly as for any other caller (D4).

**Reassignment**

- **AC7** Given an `InProgress` task assigned to A (1 active) and member B has 3 active, when a caller reassigns it to B, then the reassignment is refused — the check applies to the new assignee.
- **AC8** Given an `InProgress` task assigned to A and member B has 2 active, when a caller reassigns it to B, then it is allowed, B has 3 and A's count drops by one.
- **AC9** Given an `InProgress` task assigned to A who has exactly 3 active (this task among them), when a caller reassigns it to A again, then the request is allowed, not refused (D3).
- **AC10** Given a task that is **not** `InProgress`, when a caller reassigns it to a member already at 3, then the reassignment is allowed — the limit constrains active work only.

**The assignee is mandatory while active (D5)**

- **AC11** Given a task in `Todo` with no assignee, when a caller moves it to `InProgress`, then the move is refused with its own error code, distinct from the capacity error.
- **AC12** Given a task in `InProgress`, when a caller tries to clear its assignee, then the request is refused and the assignee is unchanged.
- **AC13** Given a task in `Review`, when a caller tries to clear its assignee, then the request is refused — [F01](f01-workflow.md)'s actor rule needs an assignee to compare against.
- **AC14** Given a task in `Backlog` or `Todo`, when a caller clears its assignee, then it is allowed.

**Missing cases the BRS does not cover**

- **AC15** Given a caller who themselves has 3 tasks in `InProgress`, when they move a task to `InProgress` assigned to someone at 0, then it is allowed — the limit applies to the assignee, never to the requester.
- **AC16** Given a caller without `MoveTasks` or `AssignTasks`, when they attempt the corresponding operation, then the response is forbidden and no count changes.
- **AC17** Given a target assignee id that does not exist, is soft-deleted, or is a deactivated account, when a caller assigns a task to them, then the request is refused with an error distinct from the capacity one.
- **AC18** Given member A has 2 active, when two callers simultaneously move two different tasks to `InProgress` with A as assignee, then exactly one succeeds and A never ends up with more than 3 (D7).
- **AC19** Given the configured limit is changed from 3 to another value N, when the rules are exercised again, then the boundary enforced and the value in the error message both follow N with no code change (D1).
- **AC20** Given a soft-deleted task that was `InProgress` and assigned to A, when A's capacity is evaluated, then that task is not counted (D6).
- **AC21** Given a Member attempts to delete any task, when the request is made, then it is forbidden — only Manager / Admin may delete (D6).
- **AC22** Given an attempt refused for capacity, when the response is inspected, then it distinguishes "capacity reached" from an invalid transition, a missing assignee and a permission failure — a drag-and-drop client must be able to tell them apart to show the right message and roll the card back ([F01](f01-workflow.md) AC20).

## Technical notes

- This is the classic "rule that counts other rows is a race" (`backend.md` §4, Recipe 14). Two concurrent requests both read 2 and both pass an application-level check, which is why D7 exists; AC18 cannot be met by a `Count()` in the service alone.
- The count must run as one query against indexed columns (`AssigneeId`, `Status`), not by loading the assignee's tasks and counting in memory.
- Validation order is fixed by [F01](f01-workflow.md) D3: transition validity is checked before capacity, so an invalid move never reports a capacity error.
- The limit's configuration key goes in `ConfigKeys` alongside the existing entries, and the service reads it through that constant.
- `IsOverdue` ([F05](f05-overdue.md)) plays no part in the count — an overdue task still occupies its slot.

---

# Technical contract (designed 2026-11-05)

Everything above is the decided behaviour and does not change, except the one clarification of D5 marked above. This section is the technical contract that makes it implementable, following the pattern of [F01](f01-workflow.md)'s *Technical contract*. All [SIGN-OFF] items below were approved by the user 2026-11-05.

## T1. Concurrency token (D7)

**Reuse `Users.ConcurrencyStamp`** (ASP.NET Identity, already `IsConcurrencyToken()` in `UserConfiguration`) — no new column, **no migration for the token**. Move and Assign load the target assignee **tracked** and, only when the write makes the task occupy a slot it did not occupy before for that user, set `assignee.ConcurrencyStamp = Guid.NewGuid().ToString()` in the **same single `SaveChanges()`** as the task update, the history insert and any notification (F01 T2's "exactly one `SaveChanges()`" rule, unchanged).

A slot is newly occupied: Move along a `ConsumesCapacity` edge (`Todo → InProgress`, `Review → InProgress`); Assign of an already-`InProgress` task to a different user. Freeing a slot (leaving `InProgress`) never bumps the token — a count can only go down, so no race needs guarding there.

**Read the assignee (and its token) before counting.** Reversing the order — count first, then load/bump the token — lets a stale count pair with a fresh token and both writers pass: T1 counts 2, T2 commits (now 3), T1 still bumps the token it read before T2's write and succeeds, landing at 4. Loading the assignee tracked first means the `UPDATE Users … WHERE Id=@id AND ConcurrencyStamp=@original` from the loser of two concurrent writes always fails after the winner commits.

Two simultaneous callers assigning/moving to the same user therefore race the same way F01 T2 describes for a task row: the loser gets `DbUpdateConcurrencyException`, or — per T2 below — a deadlock.

## T2. R13 reused: the same lock-upgrade deadlock, the same two-catch

F02's writes add FK-driven shared locks on the **Users** row they then update under T1: `TaskHistories.ActorUserId` (when the actor is the assignee), `TaskNotifications.RecipientUserId` (on a rework notification), and `Tasks.AssigneeId` itself (an `UPDATE` that changes a FK column takes a shared lock on the referenced parent row). Each of these can interleave with the capacity write's own `UPDATE Users … WHERE ConcurrencyStamp=@original` exactly as ROADMAP R13 describes for `Tasks`: two concurrent transactions both acquire the FK's shared lock, then both try to upgrade to exclusive for their `UPDATE` — a lock-upgrade deadlock (MySQL 1213).

**No new error-handling code.** `TaskService.Move` and `TaskService.Assign` already wrap their single `SaveChanges()` in F01's two-catch (`DbUpdateConcurrencyException` plus `TaskConcurrencyHelper.IsDeadlock` walking the exception chain for MySQL 1213). Both failure modes map to the operation's existing `Task.Move.Conflict` / `Task.Assign.Conflict`, logged at `Warning` (expected outcome of real concurrency, not a defect) — identical to how reopen and assign already handle it for the `Tasks` row. **No retry** — no `EnableRetryOnFailure`, no application-level retry; the loser's client refetches and resubmits, same as any other `…Conflict`.

## T3. One capacity check, shared by Move and Assign

- **Rule declaration** — `BE/TaskFlow.Common/Constants/TaskCapacityRule.cs`, beside the precedent `TaskOverdueRule`:
  - `OccupiesSlot(TaskWorkflowStatus status)` → `true` only for `InProgress` (D2).
  - `AssigneeRequiredWhile(TaskWorkflowStatus status)` → `true` for `InProgress`, `Review`, **and `Done`** (D5, clarified above).
- **Count** — one query: `AssigneeId == targetId && Status == InProgress && Id != task.Id`, counted against indexed columns. The query filter (`!IsDeleted`) excludes soft-deleted rows automatically (D6, AC20); `Id != task.Id` implements D3/AC9 directly, so a no-op reassignment is never refused.
- **Index** — `IX_Tasks_IsDeleted_AssigneeId_Status` (`IsDeleted` leads, matching F01 T1's convention), deliberately left out of F01 T1 for F02 to add. Migration `AddTasksAssigneeStatusIndex`: `CREATE INDEX` only — **Additive**, does not touch `IX_Tasks_AssigneeId` or any existing index.
- **Validation order** (F01 D3, unchanged): existence → version → same-status (move only) → transition validity → actor rule → `AssigneeRequiredWhile` (`AssigneeRequired`; also triggered when the stored `AssigneeId` no longer resolves to a user) → capacity (`CapacityReached`). Assign: existence → version → `AssigneeRequiredWhile` when clearing → target exists and is not soft-deleted (`AssigneeNotFound`, AC17) → capacity when `OccupiesSlot(task.Status)` and target ≠ current assignee (AC9 skips both the check and the token bump).
- **No role or permission reaches the check** — the helper takes no caller context, so D4/AC6 hold structurally: Admin cannot bypass it because nothing routes around it.
- **Limit (D1)** — `ConfigKeys.Workflow.MaxInProgressPerAssignee = "Workflow:MaxInProgressPerAssignee"`, default `3` in `appsettings.json`. Startup fails if the key is missing or the value is < 1 (the existing CORS-allow-list precedent in `Program.cs`). Read once in `TaskService`'s constructor, the same way `_timeZone` is read today — never a literal at the call site (AC19).

## T4. Error codes, messages and the `GET /users` contract

New codes in `BE/TaskFlow.Common/Constants/Messages/TaskControllerMS.cs` (the class F01 T3 already established), all mapped to **400**:

| Code | AC | Meaning |
|---|---|---|
| `Task.Move.AssigneeRequired` | AC11 | move to `InProgress` with no (or no longer resolvable) assignee |
| `Task.Move.CapacityReached` | AC2, AC6, AC22 | move to `InProgress` would exceed the limit |
| `Task.Assign.AssigneeRequired` | AC12, AC13 | clearing the assignee while `AssigneeRequiredWhile(task.Status)` |
| `Task.Assign.CapacityReached` | AC7, AC22 | reassigning would push the target over the limit |

**AC17 adds no new code** — a non-existent or soft-deleted target id reuses the existing `Task.Assign.AssigneeNotFound` / `Task.Create.AssigneeNotFound`, already distinct from the capacity codes (AC22). *"Deactivated account"* in AC17's wording has no corresponding state on `User` today (only `IsDeleted` and Identity lockout) — **clarified 2026-11-05: out of scope until user management exists; AC17 is satisfied by the non-existent-id and soft-deleted-id cases only.**

**[SIGN-OFF] Message content (AC2, AC19).** For `*.CapacityReached`, the envelope's `error` field is filled from a message template, `TaskControllerMS.Templates.CapacityReached`, naming the assignee's `userName` and the configured limit (e.g. "member1 already has 3 tasks in progress, which is the limit."). This means Move/Assign's internal service result gains an optional `string? ErrorMessage` alongside its error code — an internal change inside `TaskFlow.BusinessLogic`/`TaskFlow.Api`, not a wire contract change. No request or response field changes; the AC18 race loser still gets the existing `Task.Move.Conflict` / `Task.Assign.Conflict`.

**`GET api/v1/users`** → `ResultRes<List<UserRes>>` with `{ id, userName, role }`, policy `ViewTasks`, soft-deleted users excluded, ordered by `userName`. This closes the sign-off already recorded in `docs/ROADMAP.md` *Technical contract sign-off* item 6 (approved 2026-10-25) — F02 is the first feature to actually build it, needed so the assignee picker can offer anyone, not only people already visible on the board (slice 1.7's stopgap).

## T5. Spec notes found while designing (not changed here)

- [F01](f01-workflow.md)'s transition table lists "free capacity" only on `Todo → InProgress`; F01 T4 and `TaskTransitions.cs` also mark `Review → InProgress` as `ConsumesCapacity: true`, matching this feature's goal. This contract follows T4 and the code, not the prose table — a one-line fix to F01's table is proposed separately, not made here.

---

# Revision R2 (2026-11-13) — follows F01 D11–D13. Rule set D1–D7 unchanged.
Status: **Signed off 2026-11-13** — see [round-2-decisions.md](round-2-decisions.md) A1–A2

- Who starts work: only the assignee (F01 D11). Who receives a slot from someone else: send-back (`Review→InProgress`, by a non-assignee) and reassignment of an InProgress task (takeover by the taker, or by a Manager/Admin).
- Takeover is an Assign: capacity is checked on the **taker** when the task is InProgress; the previous holder's slot frees. Takeover of Backlog/Todo is never capacity-checked (AC10). Review/Done are locked (F01 D12).
- Accepted consequence (existing behaviour, now more visible): a reviewer cannot send back a task whose assignee is at the limit — `Task.Move.CapacityReached`; the reviewer approves, waits, or a Manager reassigns.
- AC17: "deactivated account" is in scope once F08 lands → `Task.Assign.AssigneeNotFound` / `Task.Create.AssigneeNotFound` (supersedes T4's 2026-11-05 clarification).

## AC changes
AC1–AC5: "when A moves … to InProgress" (A is the assignee). AC6: "an Admin at 3 starting their own fourth → refused". AC7–AC10: "a Manager reassigns … or B takes over …". AC13: code is now `Task.Assign.Locked`. AC14: a Member clears only their own Backlog/Todo assignment; others → `Task.Assign.SelfOnly`; Manager/Admin clear any. AC15 (rewritten): "Given reviewer R with 3 InProgress sends back A's task with A at 2, then allowed — the limit applies to the assignee, never the requester." AC18: "A starts two tasks from two tabs, or A starts one while R sends one back to A, at the same instant → exactly one succeeds, A ≤ limit." AC22: also distinguishes ActorNotAssignee, SelfOnly, Locked.

- **AC23 (new)** Given B at 3 InProgress, when B takes over A's InProgress task, then `Task.Assign.CapacityReached`, nothing changes.
- **AC24 (new)** Given B at 2, when B takes over A's InProgress task, then B has 3 and A's count drops by one, one `AssigneeChanged` entry with actor B.
- **AC25 (new)** Given B at 3, when B takes over a Todo task, then allowed.
