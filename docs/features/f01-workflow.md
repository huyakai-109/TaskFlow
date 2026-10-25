# F01 — Strict Workflow State Machine

| | |
|---|---|
| **Source** | `docs/brs/taskflow-brs.md` § F01 |
| **Status** | Planned — all open questions resolved 2026-10-20 |
| **Depends on** | — (this spec owns the shared task model, the permission set and the transition table) |
| **Related** | [F02](f02-capacity.md) capacity on the same move · [F04](f04-audit.md) every accepted move is recorded · [F05](f05-overdue.md) reopen recomputes the overdue flag |

**Goal** — a task may only move one step along a fixed lifecycle, so no review or handoff step can be skipped, and a closed task stays closed unless a manager deliberately reopens it.

## Shared task model

| Field | Notes |
|---|---|
| `Id` | identity |
| `Title` | required |
| `Description` | optional |
| `Status` | `Backlog` · `Todo` · `InProgress` · `Review` · `Done` — only this spec may change it |
| `Priority` | `Urgent` · `High` · `Normal` — see [F03](f03-sorting.md) |
| `AssigneeId` | nullable user reference; **required from `InProgress` onward** and may not be cleared while the task is `InProgress` or `Review` — see [F02](f02-capacity.md) |
| `DueDate` | `DateOnly?` — a calendar date with no time or offset; "today" is resolved in the configured zone, see [F05](f05-overdue.md) |
| `CompletedAt` | `DateTimeOffset?` (UTC) — set when the task enters `Done`, cleared on reopen; [F03](f03-sorting.md) orders the `Done` column by it |
| `IsOverdue` | maintained by [F05](f05-overdue.md); a workflow move never sets it directly, it asks F05 to recompute |
| audit + `IsDeleted` | supplied by the scaffold (`BaseEntity`, `IIsDeletedEntity`) — never set by hand |

A new task starts in `Backlog` (**D1**).

## Permissions

A new `Tasks` group, registered in `Permissions.All` and mapped in `RolePolicies`. Referenced by all five specs.

| Permission | Member | Manager | Admin |
|---|:--:|:--:|:--:|
| `ViewTasks` · `CreateTasks` · `UpdateTasks` · `MoveTasks` · `AssignTasks` | ✓ | ✓ | ✓ |
| `DeleteTasks` · `ReopenTasks` | — | ✓ | ✓ |

A Member may move and assign **any** task on the board, not only their own. There is one shared board: no team entity, no row-level filtering, every authenticated user sees every task (**D8**).

## Scope

**In scope** — validating and persisting a requested status change; the reopen operation; the "someone else must move it out of Review" rule; maintaining `CompletedAt`; raising the rework notification; exposing the legal next moves to the client.

**Out of scope** — email or any outbound channel; configurable workflows; moving several tasks in one request; manual ordering of cards inside a column (**D9**).

## Decisions

- **D1** a task is created in `Backlog`.
- **D2** a move to the same status (`Todo → Todo`) is rejected as an invalid transition, not silently accepted.
- **D3** validation order is fixed: existence → caller's permission → transition validity → actor rule (D5) → assignee capacity ([F02](f02-capacity.md)). Error responses stay predictable when several rules fail at once.
- **D4** status and assignee are changed by two separate operations, so one request never has to satisfy both this spec and F02's reassignment rule.
- **D5** **leaving `Review` — in either direction — must be done by a user other than the task's current assignee.** No separate reviewer role is introduced. Accepted consequence: on a board with one member, a task reaches `Review` and stays there; this is a rule, not a defect.
- **D6** reopen is `Done → Review`, restricted to `ReopenTasks` (Manager / Admin), **requires a reason** which is stored in the history entry, clears `CompletedAt`, asks F05 to recompute the overdue flag, and performs no capacity check because `Review` consumes no slot.
- **D7** a rework move (`Review → InProgress`) raises a minimal **in-app** notification to the task's assignee. No email. This is the only notification event in the MVP.
- **D9** the board is drag-and-drop. Dropping a card into a column means *change status to that column* and nothing else — **the drop position within a column carries no meaning**, because [F03](f03-sorting.md) owns the order. There is no manual reordering and no stored sort position.
- **D10** every task the API returns carries the list of statuses the **calling user** may move it to right now, with permissions, the transition table and D5 already applied. Without it a drag-and-drop client can only discover an illegal move by attempting it.

## Transition table

| From | To | Requires | Side effects |
|---|---|---|---|
| `Backlog` | `Todo` | `MoveTasks` | — |
| `Todo` | `InProgress` | `MoveTasks`, an assignee, free capacity | — |
| `InProgress` | `Review` | `MoveTasks` | — |
| `Review` | `Done` | `MoveTasks`, actor ≠ current assignee | sets `CompletedAt`, clears `IsOverdue` |
| `Review` | `InProgress` | `MoveTasks`, actor ≠ current assignee | notifies the assignee (D7) |
| `Done` | `Review` | `ReopenTasks`, a reason | clears `CompletedAt`, recomputes `IsOverdue` |

Every other pair is invalid.

## Acceptance criteria

**Happy path**

- **AC1** Given a task in `Backlog`, when a caller with `MoveTasks` moves it to `Todo`, then the status is `Todo` and one history entry is recorded.
- **AC2** Given a task in `Todo` with an assignee below capacity, when a caller moves it to `InProgress`, then the status is `InProgress`.
- **AC3** Given a task in `InProgress`, when a caller moves it to `Review`, then the status is `Review`.
- **AC4** Given a task in `Review`, when a caller who is **not** its assignee moves it to `Done`, then the status is `Done`, `CompletedAt` is set to the moment of approval, and `IsOverdue` is cleared.
- **AC5** Given a task in `Review`, when a caller who is **not** its assignee moves it to `InProgress`, then the status is `InProgress` and the assignee receives an in-app notification.
- **AC6** Given a task in `Done`, when a Manager or Admin reopens it with a reason, then the status is `Review`, `CompletedAt` is null, the overdue flag is recomputed from the current due date, and the history entry carries the reason text.

**Rejected moves** — each returns a dedicated error code, leaves the task untouched, and writes no history entry.

- **AC7** Given a task in `Backlog`, when a caller moves it to `InProgress`, then the move is refused and the response says it must go to `Todo` first.
- **AC8** Given a task in `Backlog`, when a caller moves it to `Review` or `Done`, then the move is refused.
- **AC9** Given a task in `Todo`, when a caller moves it to `Review` or `Done`, then the move is refused.
- **AC10** Given a task in `InProgress`, when a caller moves it to `Todo` or `Backlog`, then the move is refused.
- **AC11** Given a task in `InProgress`, when a caller moves it to `Done`, then the move is refused — `Review` cannot be skipped.
- **AC12** Given a task in `Review`, when a caller moves it to `Todo` or `Backlog`, then the move is refused.
- **AC13** Given a task in `Done`, when any caller moves it through the ordinary move operation — to any status, including `Review` — then the move is refused; reopen is a separate, permissioned operation (AC6, AC17).
- **AC14** Given a task in any status, when a caller moves it to that same status, then the move is refused (D2).
- **AC15** Given a status value outside the five defined ones, when a caller submits it, then the request is refused at the boundary as a shape error, before any business rule runs.

**The actor rule (D5)**

- **AC16** Given a task in `Review` assigned to member A, when A themselves moves it to `Done` or back to `InProgress`, then the move is refused with its own error code naming the rule, and the status is unchanged.
- **AC17** Given a task in `Review`, when its allowed-move list is computed for the assignee, then neither `Done` nor `InProgress` appears in it, while for any other member both do (D10) — the client must not offer a drop the server will refuse.

**Drag-and-drop support (D9, D10)**

- **AC18** Given any task returned by the API, when the response is inspected, then it carries the statuses the calling user may move it to right now — empty for a `Done` task read by a Member, and `[Review]` for the same task read by a Manager.
- **AC19** Given a card is dropped at a specific position inside a column, when the move is persisted, then only the status changes; no position or sort index is stored, and the task's place in the column follows [F03](f03-sorting.md)'s order (D9).
- **AC20** Given a refused drop, when the client receives the error, then the error code identifies which rule failed — invalid transition, capacity, actor rule or permission — so the card can be returned to its original column with an accurate message.

**Reopen (D6)**

- **AC21** Given a task in `Done`, when a Member without `ReopenTasks` attempts to reopen it, then the response is forbidden and the task remains `Done`.
- **AC22** Given a reopen request with no reason, or a blank one, when it is submitted, then it is refused as a shape error and nothing changes.
- **AC23** Given a reopened task whose due date is already in the past, when the reopen completes, then the task is flagged overdue again in the same request, without waiting for the nightly scan.

**Missing cases the BRS does not cover**

- **AC24** Given an id that does not exist, or a soft-deleted task, when a caller requests a move, then the response is not-found and no row is touched.
- **AC25** Given a caller without `MoveTasks`, when they request any transition — including a valid one — then the response is forbidden, the status is unchanged, and no history entry exists.
- **AC26** Given an unauthenticated caller, when they request a move, then the response is unauthorised.
- **AC27** Given a task where both the transition is invalid and the target assignee is at capacity, when a caller requests the move, then the invalid-transition error is returned (D3) — deterministically the same error every time.
- **AC28** Given a task in `Review` where the caller is the assignee **and** the transition is invalid, when the move is requested, then the invalid-transition error is returned, not the actor-rule error (D3).
- **AC29** Given a task in `Todo`, when two callers request `Todo → InProgress` at the same instant, then exactly one succeeds, the other receives a conflict error, and the task has advanced exactly one step.
- **AC30** Given a card dragged from a stale board view — the task has since moved on another user's screen — when the move is submitted, then it is refused with a conflict error rather than applied to the status the server now holds.
- **AC31** Given any accepted move, when it is persisted, then the status change, its history entry and any notification are committed together — none of the three can exist without the others.

## Technical notes

- The transition table is declared once, as data, with the actor rule and the required permission as attributes of each edge (`backend.md` §4, Recipe 13). Do not scatter `if (status == …)` across the service. The same table computes the allowed-move list for D10, so the client and the server can never disagree about what is legal.
- `Done → Review` exists **only** on the reopen operation's table, which is why AC13 and AC6 can both hold.
- Concurrency (AC29, AC30): an application-level read-then-write cannot hold this — guard the row with a concurrency token changed in the same `SaveChanges()`. Drag-and-drop makes AC30 routine rather than theoretical, because a board left open goes stale quickly; the client should send the version it rendered and the server should reject a mismatch.
- The notification (D7) needs a small store of its own (recipient, task, event, read flag) plus an endpoint for the client to read and mark as read. It is the one genuinely new component this feature introduces; keep it to that and do not generalise it into an event bus.
- `CompletedAt` is written by this spec only. [F03](f03-sorting.md) reads it for the `Done` column and must never fall back to `UpdatedAt`, which changes on any edit.
