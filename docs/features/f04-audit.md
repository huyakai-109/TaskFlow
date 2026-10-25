# F04 — Task Audit & History Trail

| | |
|---|---|
| **Source** | `docs/brs/taskflow-brs.md` § F04 |
| **Status** | Planned — all open questions resolved 2026-10-20 |
| **Depends on** | [F01](f01-workflow.md) status changes and reopen · [F02](f02-capacity.md) assignee changes · [F05](f05-overdue.md) system-marked overdue |

**Goal** — every significant change to a task is permanently recorded, so anyone can see who did what and when, and nobody can rewrite that record.

## Scope

**In scope** — recording creation, status changes, assignee changes, priority changes, due-date changes, reopen (with its reason), system-marked overdue and deletion; reading a task's history in chronological order.

**Out of scope** — recording title or description edits; diffing arbitrary fields; exporting the trail; retention or archiving policy; a cross-task activity feed.

## Decisions

- **D1** an entry stores **structured fields** — event type, actor (or `System`), old value, new value, an optional reason, timestamp — and the sentences in the BRS are how the API renders them, not what is stored. Prose in the database would freeze wording and language into the data.
- **D2** entries are append-only: there is no update or delete operation, for any role, and the central soft-delete behaviour does not apply to them.
- **D3** creating a task writes a `Created` entry, so the trail begins at the beginning.
- **D4** timestamps are stored in UTC as `DateTimeOffset`; rendering in **Asia/Ho_Chi_Minh** is a presentation concern, using the same configured zone as [F05](f05-overdue.md).
- **D5** anyone with `ViewTasks` may read any task's full history — history is not a separate permission, and there is no per-team filtering ([F01](f01-workflow.md) D8).
- **D6** deleting a task is recorded as a `Deleted` entry and the trail **survives**: the task is soft-deleted, its history is untouched and remains readable.
- **D7** priority and due-date changes are recorded, so "who moved the deadline?" is answerable — the question F04 exists to answer.

## Recorded events

| Event | Captured |
|---|---|
| `Created` | actor, timestamp |
| `StatusChanged` | actor, from status, to status, timestamp |
| `AssigneeChanged` | actor, from assignee, to assignee, timestamp |
| `PriorityChanged` | actor, from priority, to priority, timestamp |
| `DueDateChanged` | actor, from date, to date (either may be null), timestamp |
| `Reopened` | actor, from `Done` to `Review`, **reason**, timestamp |
| `OverdueMarked` | `System` as actor, timestamp — see [F05](f05-overdue.md) |
| `Deleted` | actor, timestamp |

## Acceptance criteria

**Recording**

- **AC1** Given a task is created, when its history is read, then one `Created` entry exists naming the actor and the moment.
- **AC2** Given a task moves from `Todo` to `InProgress`, when the move succeeds, then one entry exists naming the acting user, both statuses and the moment.
- **AC3** Given a task is reassigned from member A to member B, when the change succeeds, then one entry exists naming the acting user, A and B.
- **AC4** Given a task's priority changes from `Normal` to `Urgent`, when the change succeeds, then one entry records both values (D7).
- **AC5** Given a task's due date is changed, cleared, or set for the first time, when the change succeeds, then one entry records the old and new values, with null represented explicitly (D7).
- **AC6** Given a Manager reopens a `Done` task with a reason, when the history is read, then the `Reopened` entry carries that reason text verbatim.
- **AC7** Given a task moved through three stages in one day, when its history is read, then all three changes appear as separate entries in the order they happened.
- **AC8** Given the overdue job flags a task, when its history is read, then the entry's actor is `System` and not any user account.
- **AC9** Given a Manager deletes a task, when the history is examined, then a `Deleted` entry exists and every earlier entry is unchanged (D6).
- **AC10** Given a task change and its history entry, when the change is persisted, then both are committed together — a change with no entry, or an entry with no change, must be impossible even if the request fails midway.

**Nothing recorded on failure**

- **AC11** Given a move refused for an invalid transition, when the history is read, then no entry was written.
- **AC12** Given a move refused because the assignee is at capacity, when the history is read, then no entry was written.
- **AC13** Given a move refused by the actor rule ([F01](f01-workflow.md) D5), when the history is read, then no entry was written.
- **AC14** Given a change refused because the caller lacks permission, when the history is read, then no entry was written and no actor is recorded anywhere.

**Reading**

- **AC15** Given a task with history, when a caller with `ViewTasks` reads it, then the full trail is returned in chronological order, oldest first.
- **AC16** Given a caller without `ViewTasks`, when they request a history, then the response is forbidden and no entries are returned — the trail must not become a side channel for data the caller cannot otherwise see.
- **AC17** Given a task created but not yet changed (the empty case), when its history is read, then exactly the `Created` entry is returned (D3) — never an empty list and never a not-found.
- **AC18** Given two entries share the same timestamp, when the history is read twice, then both responses return them in the same order (a deterministic tie-break by id).
- **AC19** Given an id that does not exist, when a caller requests its history, then the response is not-found.
- **AC20** Given a soft-deleted task, when its history is requested, then the trail is still readable (D6).
- **AC21** Given any entry, when it is returned, then the response carries the structured fields and a rendered sentence built from them, with the timestamp rendered in the configured zone (D1, D4).

**Immutability**

- **AC22** Given any existing entry, when any caller — including an Admin — attempts to change or remove it, then no API path exists to do so.
- **AC23** Given the task a trail belongs to is soft-deleted, when the store is inspected, then no history row was marked deleted or removed (D2, D6).
- **AC24** Given any rendered message, when it is returned, then no raw exception text, internal identifier or secret appears in it.

## Technical notes

- An append-only history table written in the same commit as the change (`backend.md` §4, Recipe 15 — except the actor, which is its own nullable column here, see below). The scaffold's central audit columns keep only the *last* change — they cannot satisfy AC7, which is exactly why this table exists.
- The entity must **not** implement `IIsDeletedEntity`, and no service may call update or delete on its repository. That is what makes AC22 and AC23 structural rather than a matter of discipline.
- Actor is nullable to represent `System` (AC8); do not invent a synthetic user row for it.
- `UnitOfWork.SaveChanges()` stamps `CreatedBy`/`CreatedAt` on insert, so actor and moment are captured for user-driven entries. A Quartz job runs with no `HttpContext` and would stamp `0`, so the `System` case must be set explicitly, never inferred from a missing claim (AC8).
- Old and new values span statuses, user ids, priorities and dates. Store them as text with the event type deciding how to read them, rather than adding a column per field — but keep the event type an enum, not free text.
- History grows faster than the task table. Index by task id plus timestamp, and page the read endpoint from the start.
