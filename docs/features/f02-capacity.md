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
- **D5** an assignee is **mandatory from `InProgress` onward**: a task cannot enter `InProgress` unassigned, and the assignee cannot be cleared while the task is `InProgress` or `Review`. Reassignment to a different member is always allowed subject to capacity.
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
