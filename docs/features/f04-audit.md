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

---

# Technical contract (designed 2026-10-25)

Everything above is the decided behaviour and does not change. This section is the technical contract. It is the F04 half of one design; the shared parts — concurrency, atomicity, error codes, the API conventions and the endpoint table — live in [f01-workflow.md](f01-workflow.md) → *Technical contract*, and are not repeated here.

## T1. `TaskHistory` — table `TaskHistories` — `BaseEntity` only, **no `IIsDeletedEntity`**

| Column | CLR | MySQL | Null | Notes |
|---|---|---|---|---|
| `Id` | `long` | `bigint` identity | no | PK |
| `TaskId` | `long` | `bigint` | no | FK → `Tasks.Id`, `OnDelete(Restrict)`, **no navigation property** |
| `Event` | `TaskHistoryEvent` | `int` | no | typed enum, never free text (D1) |
| `ActorUserId` | `long?` | `bigint` | yes | **`null` = System.** FK → `Users.Id`, `Restrict` |
| `FromValue` | `string` | `varchar(100)` | yes | text; the event type decides how to read it |
| `ToValue` | `string` | `varchar(100)` | yes | `null` is meaningful and explicit (AC5) |
| `Reason` | `string` | `varchar(500)` | yes | reopen only (F01 D6, AC6) |
| `CreatedAt` | `BaseEntity` | `datetime(6)` | no | **this is the entry's timestamp**, exposed as `occurredAt`. No second timestamp column |
| `CreatedBy`, `UpdatedBy`, `UpdatedAt` | `BaseEntity` | | no | kept for table consistency. **`CreatedBy` is never the actor and is never read as one** |

No query filter. Registered in `MyDbContext.OnModelCreating`.

Index `IX_TaskHistories_TaskId_CreatedAt_Id` on `(TaskId, CreatedAt, Id)` — the only read query the table serves, and it makes AC18's tie-break (`CreatedAt`, then `Id`) an index scan.

**Why the actor is its own column.** `UnitOfWork.SaveChanges()` stamps `CreatedBy` from claims and yields `0` when there is no `HttpContext`, which is exactly the F05 Quartz job's situation. AC8 forbids that, so the `System` case is written explicitly as `null` and is never inferred from a missing claim. This is also why `CLAUDE.md` overrides kit Recipe 15 on this point.

**Why immutability is structural, not a matter of discipline (D2, AC22, AC23).** The entity does not implement `IIsDeletedEntity`, so `UnitOfWork`'s soft-delete conversion can never touch it; no endpoint exposes an update or delete; and no service may call `Update` or `Delete` on `GetRepository<TaskHistory>()`. An accidental `Remove` would be a hard delete and must be caught in review.

**No navigation property to `TaskItem`.** Configure `HasOne<TaskItem>().WithMany().HasForeignKey(i => i.TaskId)`. A required navigation from an unfiltered dependent to a filtered principal raises EF's `RequiredNavigationWithQueryFilterWarning` on every query and would silently hide the history of a deleted task, which AC20 forbids. Reads use explicit LINQ joins.

## T2. Value encoding — one rule per event (D1)

| Event | `FromValue` | `ToValue` | `Reason` |
|---|---|---|---|
| `Created` = 1 | null | null | null |
| `StatusChanged` = 2 | status name | status name | null |
| `AssigneeChanged` = 3 | user id as text, or null | user id as text, or null | null |
| `PriorityChanged` = 4 | priority name | priority name | null |
| `DueDateChanged` = 5 | `yyyy-MM-dd` or null | `yyyy-MM-dd` or null | null |
| `Reopened` = 6 | `Done` | `Review` | required text |
| `OverdueMarked` = 7 | null | null | null |
| `Deleted` = 8 | null | null | null |

Enum **names** are stored in the value columns, not numbers, so renumbering an enum can never silently reinterpret history. The event column itself is the int enum. `UnitOfWork` trims strings and turns `""` into `null`, so never write an empty string.

## T3. Reading (AC15, AC18–AC21)

`GET api/v1/tasks/{taskId}/history?start=&length=` — policy `ViewTasks` (D5: history is not a separate permission), oldest first, `ORDER BY CreatedAt, Id`, paged from the start with the same paging vocabulary as every other list.

```jsonc
// TaskHistoryRes
{ "id": 44, "taskId": 7,
  "event": "StatusChanged",
  "actorUserId": 12,                                   // null = System (AC8)
  "actorName": "member1",                              // null = System
  "fromValue": "Todo", "toValue": "InProgress",
  "reason": null,
  "occurredAt":      "2026-10-25T02:12:03.000+00:00",  // UTC, from CreatedAt
  "occurredAtLocal": "2026-10-25T09:12:03.000+07:00",  // configured zone (D4)
  "message": "member1 moved the task from Todo to In Progress" }
```

`message` is **built on read** from the structured fields and never stored (D1), from templates in `BE/TaskFlow.Common/Constants/Messages/TaskHistoryMessages.cs`. It carries no exception text, no internal identifier and no secret (AC24). English only; i18n is out of MVP scope.

**Two query details that AC19 and AC20 depend on.** The history rows themselves need no filter-dodging, because `TaskHistory` is unfiltered. The **existence check** does: look the task up with `IgnoreQueryFilters()` so that a soft-deleted task still returns its trail (AC20) while a genuinely non-existent id still fails with `TaskHistory.GetList.TaskNotFound` (AC19). The actor join also uses `IgnoreQueryFilters()` on `Users`, so an entry written by a since-deleted user still resolves its name. On EF Core 9 that drops *every* filter on the entity, which is safe here because `IsDeleted` is the only one — the deliberate use `CLAUDE.md` describes.

## T4. Writing — who writes what, and when

Every entry is written in the **same `SaveChanges()`** as the change it records (AC10); see f01-workflow.md → T2 *Atomicity*. No entry is written on a refused operation (AC11–AC14), which follows from validating everything before mutating anything.

| Written by | Event | Slice |
|---|---|---|
| create | `Created` — exactly one, even when assignee, priority and due date are supplied | 1.1 |
| move | `StatusChanged` | 1.2 |
| reopen | `Reopened`, carrying the reason verbatim | 1.3 |
| assignee change | `AssigneeChanged` | 1.4 |
| priority change | `PriorityChanged` | 1.4 |
| due-date change | `DueDateChanged` | 1.4 |
| delete | `Deleted`, trail untouched (D6, AC9, AC23) | 1.4 |
| F05 overdue job | `OverdueMarked`, actor `null` | F05 |

**Open assumption [SIGN-OFF]** — reopening a task whose due date has already passed writes **two** entries, `Reopened` plus an `OverdueMarked` whose actor is the reopening user rather than `System`. F05 D7 says every transition into the overdue state is recorded; AC8 constrains only the job's actor. The alternative is a single `Reopened` entry with the `IsOverdue` flag changing unrecorded. Recommended as written; it is F05 territory reached through F01 AC23.
