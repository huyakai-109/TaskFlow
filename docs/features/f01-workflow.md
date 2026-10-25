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

---

# Technical contract (designed 2026-10-25)

Everything above is the decided behaviour and does not change. This section is the technical contract that makes it implementable. Items marked **[SIGN-OFF]** are open until the user decides them; nothing is built against an open item.

## T1. Data model

Three new entities in `BE/TaskFlow.DataAccess/Entities/`, configurations in `BE/TaskFlow.DataAccess/Configurations/`, each registered in `MyDbContext.OnModelCreating`. `TaskFlow.DataAccess` has `<Nullable>disable</Nullable>` — declare entity strings plainly, as `Example.cs` does, and do not flip it.

**Naming.** `Task` collides with `System.Threading.Tasks.Task` and `TaskStatus` with `System.Threading.Tasks.TaskStatus`, both reachable through `ImplicitUsings`. Entity is `TaskItem` (table `Tasks`); enums are `TaskWorkflowStatus`, `TaskPriority`, `TaskHistoryEvent`, `TaskNotificationEvent`.

### `TaskItem` — table `Tasks` — `BaseEntity`, `IIsDeletedEntity`

| Column | CLR | MySQL | Null | Notes |
|---|---|---|---|---|
| `Id` | `long` | `bigint` identity | no | PK |
| `Title` | `string` | `varchar(200)` | no | `HasMaxLength(200)`; without it Pomelo emits `longtext` |
| `Description` | `string` | `varchar(4000)` | yes | |
| `Status` | `TaskWorkflowStatus` | `int` | no | |
| `Priority` | `TaskPriority` | `int` | no | ranked ascending, see T4 |
| `AssigneeId` | `long?` | `bigint` | yes | FK → `Users.Id`, `OnDelete(Restrict)` |
| `DueDate` | `DateOnly?` | `date` | yes | no zone arithmetic in the column |
| `CompletedAt` | `DateTimeOffset?` | `datetime(6)` | yes | written only by move→`Done` and reopen |
| `IsOverdue` | `bool` | `tinyint(1)` | no | default `false` |
| `Version` | `Guid` | `char(36)` | no | concurrency token, T2 |
| `IsDeleted` | `bool` | `tinyint(1)` | no | soft delete |
| audit | `BaseEntity` | | no | stamped by `UnitOfWork`, never set by hand |

Query filter: `builder.HasQueryFilter(i => !i.IsDeleted)`.

Indexes created with the table — `IsDeleted` leads every composite because MySQL has no filtered indexes and the query filter appends `IsDeleted = 0` to every read:

| Index | Columns | Query it answers |
|---|---|---|
| `IX_Tasks_IsDeleted_Status_Priority_DueDate` | `IsDeleted, Status, Priority, DueDate` | board column read; F03's active-column order |
| `IX_Tasks_IsDeleted_Status_CompletedAt` | `IsDeleted, Status, CompletedAt` | the `Done` column (F03 D4) |

Deliberately **not** created here: `(IsDeleted, AssigneeId, Status)` for F02's capacity count and `(IsDeleted, Status, IsOverdue, DueDate)` for F05's scan. Each is additive on a board-sized table and belongs to the feature that needs it.

### `TaskNotification` — table `TaskNotifications` — `BaseEntity` only

`Id` · `RecipientUserId` `long` FK → `Users.Id` `Restrict` · `TaskId` `long` FK → `Tasks.Id` `Restrict` · `Event` `TaskNotificationEvent` int (`ReturnedForRework = 1`, the only MVP value) · `IsRead` `bool` default false · `ReadAt` `DateTimeOffset?`.

No query filter. The inbox read inner-joins `Tasks`, so a notification for a soft-deleted task disappears from the inbox through the task's own filter — intended, and the reason the join is explicit.

### No navigation properties to `TaskItem`

`TaskHistory` and `TaskNotification` configure `HasOne<TaskItem>().WithMany().HasForeignKey(i => i.TaskId)` with no navigation property. A required navigation from an unfiltered dependent to a filtered principal raises EF's `RequiredNavigationWithQueryFilterWarning` on every query and would silently hide history rows of a deleted task, which F04 AC20 forbids. Reads use explicit LINQ joins; the precedent is the role/claim join in `AuthService.Login`.

### Enums — `BE/TaskFlow.Common/Enums/` (new folder)

```
TaskWorkflowStatus     Backlog=1, Todo=2, InProgress=3, Review=4, Done=5
TaskPriority           Urgent=1, High=2, Normal=3     // ascending rank: ORDER BY Priority ASC is F03's rule
TaskHistoryEvent       Created=1 … Deleted=8          // see f04-audit.md
TaskNotificationEvent  ReturnedForRework=1
```

Stored as **int** (house style). Exposed on the wire as **names**, which `FE/src/api/types.ts` already assumes. That needs one global registration in `Program.cs`:

```csharp
builder.Services.AddControllers()
    .AddJsonOptions(o => o.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter()));
```

The converter still accepts numbers, so every enum **request** property also carries `[EnumDataType(typeof(T))]`, making an out-of-range value a model-validation failure rather than a business one (AC15). Never reorder or renumber a member.

### Permissions (data — `CLAUDE.md` *Permissions are data*)

New `Tasks` struct in `Permissions.cs`, appended to `Permissions.All`: `ViewTasks · CreateTasks · UpdateTasks · MoveTasks · AssignTasks · DeleteTasks · ReopenTasks`. Appended to each role's `AllowedPermissions` in `RolePolicies.cs` — never inserted in the middle, because ids are positional inside each role's id block (slice 0.1):

| Role | Appends | RoleClaim ids |
|---|---|---|
| Admin (1) | all 7 | 6–12 (1–5 in use) |
| Manager (3) | all 7 | 1006–1012 (1001–1005 in use) |
| Member (4) | View, Create, Update, Move, Assign | 2001–2005 (block empty today) |

`SecurityConfiguration.AddSecurity` needs no change — it loops `Permissions.All`. The migration is data-only `HasData` inserts: **Additive**.

### Shared rules that are not entities

- **Overdue predicate**, one implementation called by F01's write paths and later by F05's job: `TaskOverdueRule.IsOverdue(DateOnly? dueDate, TaskWorkflowStatus status, DateOnly todayInZone)`. F01 calls it on create, due-date change, move→`Done` and reopen. Never duplicate the predicate.
- **Zone**: new key `ConfigKeys.Workflow.TimeZone = "Workflow:TimeZone"`, value `Asia/Ho_Chi_Minh`, resolved through `DateTimeHelper.ToTimezone`. Blocked by R-TZ below until `InvariantGlobalization` is turned off.

## T2. Concurrency (AC29, AC30, AC31)

**Token.** `TaskItem.Version`, `Guid`, `char(36)`, configured `builder.Property(i => i.Version).IsConcurrencyToken();` — the technique `UserConfiguration` already uses for `ConcurrencyStamp`. Pomelo/MySQL has no `rowversion` and no `xmin`; a service-assigned `Guid` is the provider-neutral token and the only option that works here.

**Bumped on every write.** Every mutating path assigns `task.Version = Guid.NewGuid();` before `SaveChanges()`. Soft delete is the exception: `UnitOfWork.SaveChangesSoftDelete` resets the entry to `Unchanged` and flips only `IsDeleted`, so the token is not bumped — EF still places it in the `WHERE` clause, so the guard holds, and the row leaves every filtered query anyway.

**On the wire.** Every task payload carries `version`. Every mutating request body carries `version` as `[Required] public Guid? Version { get; set; }` — nullable so a missing field is a field-level model error instead of silently binding `Guid.Empty`. `DELETE` carries it as `[FromQuery]`.

**Two guards, both required.**

| Case | Mechanism |
|---|---|
| AC30 — stale board | Explicit service check right after the existence guard: `if (task.Version != requestDto.Version) return …Conflict;` |
| AC29 — two simultaneous moves | The entity is loaded **tracked** and mutated in place, so EF emits `UPDATE … WHERE Id = @id AND Version = @original`. The second writer blocks on the InnoDB row lock, re-reads under it, matches 0 rows, and EF raises `DbUpdateConcurrencyException` |

**Never re-attach a detached instance** to `BaseRepository.Update`: original values would equal current values and the `WHERE` guard would be lost. Load tracked, mutate in place.

`DbUpdateConcurrencyException` is caught **in the service**, around the single `SaveChanges()` call only, and returned as the operation's `…Conflict` code (business failures are returned, not thrown — `backend.md` §4).

**Atomicity (AC31, F04 AC10).** Exactly one `await unitOfWork.SaveChanges()` per operation. It is a single `SaveChangesAsync()`, and EF wraps all its statements in one transaction, so `UPDATE Tasks` + `INSERT TaskHistories` + `INSERT TaskNotifications` commit together or not at all. **No explicit transaction is needed and none is to be added.** The review rule: a move, reopen or assign service method contains exactly one `SaveChanges()`.

**Validation order.** `[Authorize(Policy)]` runs first, so a caller without the permission gets 403 even for a missing id — this does not leak existence. Inside the service, D3's order governs, with the version check inserted second: existence → **version** → transition validity → actor rule → (F02) capacity. AC27 and AC28 are unaffected, because they describe requests that carry a current version.

## T3. Error codes

**Location [SIGN-OFF]:** `BE/TaskFlow.Common/Constants/Messages/`, one file per controller — `TaskControllerMS.cs`, `TaskHistoryControllerMS.cs`, `NotificationControllerMS.cs`. `Constants/` is flat today with 7 files; a `Messages/` subfolder keeps the per-feature message classes together as F02–F05 add theirs. The alternative is flat in `Constants/`. Whichever is chosen is the precedent for F02, F03 and F05.

Shape per `backend.md` §5: a nested struct per operation, every group carrying an `Exception` member, codes formatted `"Feature.Operation.Reason"`.

**HTTP convention established here [SIGN-OFF]:** every business failure is **400** with `success:false` and an `errorCode`. No 404, no 409 — the client branches on `errorCode` alone (`FE/src/api/client.ts`), and `backend.md` §3 forbids mixing status-code conventions. `ExampleController`'s `NotFound(...)` is explicitly not the precedent. 401 and 403 come from the framework with an empty body and are mapped by the client from the status.

| Code | Acceptance criterion | HTTP |
|---|---|---|
| `Task.Create.DueDateInPast` · `.AssigneeNotFound` · `.Exception` | F05 AC12 · F02 AC17 · — | 400 · 400 · 500 |
| `Task.Get.NotFound` · `.Exception` | AC24 · — | 400 · 500 |
| `Task.GetList.Exception` · `Task.GetBoard.Exception` | — | 500 |
| `Task.Update.NotFound` · `.Conflict` · `.Exception` | AC24 · AC30 · — | 400 · 400 · 500 |
| `Task.UpdatePriority.NotFound` · `.Conflict` · `.Exception` | AC24 · AC30 · — | 400 · 400 · 500 |
| `Task.UpdateDueDate.NotFound` · `.Conflict` · `.DueDateInPast` · `.Exception` | AC24 · AC30 · F05 AC12 · — | 400 ×3 · 500 |
| `Task.Assign.NotFound` · `.Conflict` · `.AssigneeNotFound` · `.Exception` | AC24 · AC30 · F02 AC17 · — | 400 ×3 · 500 |
| `Task.Assign.AssigneeRequired` · `.CapacityReached` | **reserved for F02** AC12/AC13 · AC7 | 400 |
| `Task.Move.NotFound` | AC24 | 400 |
| `Task.Move.Conflict` | AC29, AC30 | 400 |
| `Task.Move.InvalidTransition` | AC7–AC13, AC20, AC27, AC28 | 400 |
| `Task.Move.SameStatus` **[SIGN-OFF]** | AC14 (D2) | 400 |
| `Task.Move.ActorIsAssignee` | AC16, AC20 | 400 |
| `Task.Move.AssigneeRequired` · `.CapacityReached` | **reserved for F02** AC11 · AC2/AC22 | 400 |
| `Task.Move.Exception` | — | 500 |
| `Task.Reopen.NotFound` · `.Conflict` · `.InvalidTransition` · `.Exception` | AC24 · AC30 · AC6 inverse · — | 400 ×3 · 500 |
| `Task.Delete.NotFound` · `.Conflict` · `.Exception` | AC24 · AC30 · — | 400 · 400 · 500 |
| `TaskHistory.GetList.TaskNotFound` · `.Exception` | F04 AC19 · — | 400 · 500 |
| `Notification.GetList.Exception` | — | 500 |
| `Notification.MarkRead.NotFound` · `.Exception` | another user's notification returns NotFound, never Forbidden — no existence leak | 400 · 500 |

Covered without a code: **permission** → 403 from `[Authorize(Policy)]` (AC21, AC25; F04 AC16); **unauthenticated** → 401 (AC26); **shape** — unknown status, blank reason, over-length title → 400 envelope with `validates[]`, which **requires R1's `InvalidModelStateResponseFactory` (slice 0.3)**. AC15 and AC22 cannot be demonstrated before it lands.

`Task.Move.SameStatus` **[SIGN-OFF]**: D2 requires a same-status move to be rejected as an invalid transition; a distinct code refines that rather than contradicting it, and AC20 asks the error code to identify which rule failed. It exists because a drag-and-drop board drops into the origin column constantly and that should not read as "you skipped a stage". Reverting to `InvalidTransition` is one line and one client mapping.

## T4. The transition table as data (D10, AC17, AC18)

One declaration, `BE/TaskFlow.Common/Constants/TaskTransitions.cs`:

```csharp
public sealed record TaskTransition(
    TaskWorkflowStatus From,
    TaskWorkflowStatus To,
    string RequiredPermission,     // MoveTasks | ReopenTasks
    bool ActorMustNotBeAssignee,   // D5
    bool RequiresAssignee,         // the edge's precondition (F02 D5)
    bool ConsumesCapacity,         // declared here, read by F02
    bool RequiresReason);          // reopen only
```

| Set | Edges |
|---|---|
| `Moves` | `Backlog→Todo` · `Todo→InProgress` (RequiresAssignee, ConsumesCapacity) · `InProgress→Review` · `Review→Done` (ActorMustNotBeAssignee) · `Review→InProgress` (ActorMustNotBeAssignee, RequiresAssignee, ConsumesCapacity) |
| `Reopens` | `Done→Review` (ReopenTasks, RequiresReason) |

| Member | Use |
|---|---|
| `FindMove(from, to)` | the move endpoint validates against `Moves` only → AC13 holds, because `Done → anything` is not a move |
| `FindReopen(from, to)` | the reopen endpoint validates against `Reopens` only → AC6 holds |
| `AllowedFor(status, assigneeId, actorUserId, string[] permissions)` | D10 / AC17 / AC18, evaluated over `Moves ∪ Reopens` |

`AllowedFor` applies exactly what D10 specifies and nothing more: `edge.From == status` **and** `permissions.Contains(edge.RequiredPermission)` **and** `!(edge.ActorMustNotBeAssignee && assigneeId == actorUserId)`.

- AC18 — a `Done` task read by a Member yields `[]`; read by a Manager, `[Review]` through the reopen edge.
- AC17 — a `Review` task read by its assignee offers neither `Done` nor `InProgress`; read by anyone else, both.
- **Deliberately not applied:** `RequiresAssignee` and `ConsumesCapacity`. An unassigned `Todo` task still offers `InProgress`, because `docs/ui.md`'s assignee-picker flow ("Assign to … and start") is triggered by exactly that drop, and capacity is a per-target-person rule the picker resolves. D10 names permissions, the table and D5 — this matches it literally.

The caller's permissions reach the service as a plain `string[]`, read in the controller through the existing `this.User.Claims.GetUserRoleClaims()`. No `ClaimsPrincipal` in a service.

## T5. API contract

Conventions for every endpoint below: route prefix `api/v{version:apiVersion}/…`, `[ApiVersion("1.0")]`, controller inherits `BaseController`; envelope created first; `try`/`catch (Exception ex)` → `Logger.LogError(ex, "…")` → `InternalServerError(response)`; write actions call `request = (TReq)request.TrimData();` before mapping; route ids are `[FromRoute]` and assigned onto the DTO after mapping, never accepted in the body.

**Paging vocabulary, pinned because the scaffold is inconsistent:** `start` is a **1-based page number** (matching `GlobalConstants.PageConfig.Start = 1`), `length` is the page size, maximum 100. `PaginationRes` is constructed as `new PaginationRes(total, currentCount, (start - 1) * length, length)`. New code pages asynchronously: slice 1.1 **adds** `Pagination<T>.CreateAsync(IQueryable<T>, int pageNumber, int pageSize)` and leaves the existing synchronous constructors untouched.

Controllers and services: `TasksController` → `ITaskService`, `TaskHistoryController` → `ITaskHistoryService`, `NotificationsController` → `INotificationService`, all registered scoped in `CoreDependencyConfiguration.AddServices`, with AutoMapper profiles `TaskMapper.cs`, `TaskHistoryMapper.cs`, `NotificationMapper.cs`.

**Service result protocol — a stated extension of the house style:** mutations return `Task<(string errorCode, TaskDetailResDto?)>` rather than bare `Task<string>`, because the client must receive the new `version` and the recomputed `allowedTransitions` or it would have to refetch the whole board after every drag.

### Shared response models (`BE/TaskFlow.Api/Models/Responses/Tasks/`)

```jsonc
// TaskSummaryRes — board card
{
  "id": 7,
  "title": "Fix the login redirect",
  "status": "InProgress",
  "priority": "Urgent",
  "assignee": { "id": 12, "userName": "member1" },   // or null
  "dueDate": "2026-10-28",                            // yyyy-MM-dd, or null; never a timestamp
  "isOverdue": false,
  "completedAt": "2026-10-25T09:12:03.123+00:00",     // UTC, or null
  "version": "4f1d…",                                 // send back on every write
  "allowedTransitions": [
    { "status": "Review", "operation": "Move", "requiresReason": false }
  ]
}

// TaskDetailRes = TaskSummaryRes plus:
{ "description": "…", "createdAt": "…+00:00", "createdBy": 12, "updatedAt": "…+00:00" }
```

`allowedTransitions` is an **array of objects [SIGN-OFF]**, not the plain `TaskStatus[]` that `FE/src/api/types.ts:73` currently declares. The client must know that `Done → Review` is the *reopen* operation and needs a reason, and `docs/ui.md` forbids re-deriving the transition table in the client. AC18 is still satisfied literally, because the statuses are carried.

### Endpoints

| # | Verb + route | Policy | Request | Success | Error codes |
|---|---|---|---|---|---|
| 1 | `POST api/v1/tasks` | `CreateTasks` | `Title` `[Required][MaxLength(200)]` · `Description` `[MaxLength(4000)]` · `Priority` `[Required][EnumDataType]` · `DueDate` `DateOnly?` · `AssigneeId` `long?` | `ResultRes<TaskDetailRes>`, status `Backlog` (D1), `Version` set, exactly **one** `Created` entry even when assignee, priority and due date are supplied | `Task.Create.*` |
| 2 | `GET api/v1/tasks/{id}` | `ViewTasks` | route id | `ResultRes<TaskDetailRes>` | `Task.Get.NotFound` (soft-deleted included, AC24) |
| 3 | `GET api/v1/tasks/board` | `ViewTasks` | — | `ResultRes<BoardRes>`; `BoardRes { columns }`, `BoardColumnRes { status, items, pagination }`. **Always five columns in lifecycle order; empty ones present with zero totals** | `Task.GetBoard.Exception` |
| 4 | `GET api/v1/tasks?status=&start=&length=` | `ViewTasks` | `Status` `[EnumDataType]` optional · `Start` ≥1 default 1 · `Length` 1..100 default 50 | `PaginationResultRes<List<TaskSummaryRes>>` — page 2+ of one column | `Task.GetList.Exception` |
| 5 | `PUT api/v1/tasks/{id}` | `UpdateTasks` | `Title` `[Required][MaxLength(200)]` · `Description` `[MaxLength(4000)]` · `Version` | `ResultRes<TaskDetailRes>`, **no history entry** (F04 out of scope) | `Task.Update.*` |
| 6 | `PUT api/v1/tasks/{id}/priority` | `UpdateTasks` | `Priority` `[Required][EnumDataType]` · `Version` | + `PriorityChanged` entry | `Task.UpdatePriority.*` |
| 7 | `PUT api/v1/tasks/{id}/due-date` | `UpdateTasks` | `DueDate` `DateOnly?` (explicit null clears) · `Version` | + `DueDateChanged` entry, `IsOverdue` recomputed in the same commit | `Task.UpdateDueDate.*` |
| 8 | `PUT api/v1/tasks/{id}/assignee` | `AssignTasks` | `AssigneeId` `long?` · `Version` | + `AssigneeChanged` entry | `Task.Assign.*` |
| 9 | `PUT api/v1/tasks/{id}/status` | `MoveTasks` | `Status` `[Required][EnumDataType]` · `Version` | + `StatusChanged` entry; `Review→Done` sets `CompletedAt` and clears `IsOverdue` (AC4); `Review→InProgress` inserts one notification to the assignee (AC5, D7) — all in one `SaveChanges()` (AC31) | `Task.Move.*` |
| 10 | `PUT api/v1/tasks/{id}/reopen` | `ReopenTasks` | `Reason` `[Required][MaxLength(500)]` — `[Required]` rejects whitespace, so AC22 is a shape error · `Version` | status `Review`, `CompletedAt` null, `IsOverdue` recomputed (AC23), `Reopened` entry carrying the reason verbatim | `Task.Reopen.*` |
| 11 | `DELETE api/v1/tasks/{id}?version={guid}` | `DeleteTasks` | route id · `Version` `[Required]` from query | `ExecutionRes` — soft delete + `Deleted` entry, trail untouched | `Task.Delete.*` |
| 12 | `GET api/v1/tasks/{taskId}/history?start=&length=` | `ViewTasks` | paging | `PaginationResultRes<List<TaskHistoryRes>>`, **oldest first**, `ORDER BY CreatedAt, Id` | `TaskHistory.GetList.*` |
| 13 | `GET api/v1/notifications?isRead=&start=&length=` | `ViewTasks` | `IsRead` `bool?` · paging | newest first; **the caller's own rows only**, inner-joined to non-deleted tasks | `Notification.GetList.Exception` |
| 14 | `PUT api/v1/notifications/{id}/read` | `ViewTasks` | route id | `ExecutionRes`, idempotent | `Notification.MarkRead.*` |

**Left to F03 on endpoints 3 and 4:** the `ORDER BY` (F01 ships a deterministic `CreatedAt asc, Id asc`), the per-column page sizes (F01 ships 50 everywhere; F03 sets 50 active / 20 `Done`) and the `Done` column's `CompletedAt desc` rule. The response shape, the routes and the paging vocabulary are **final** — F03 changes an order-by expression and two default constants, not the contract. F05 later adds an `isOverdue` filter to endpoint 4, additive and non-breaking.

## T6. Two contract gaps the client hits immediately

1. **Caller permissions [SIGN-OFF].** `allowedTransitions` covers per-task moves, but the "New task", "Delete" and "Reopen" affordances need the caller's permission set. Proposal: add `permissions: string[]` to the **login and refresh** responses — the claims are already assembled in `AuthService.Login`. This touches unit 0's auth work and must be coordinated with slice 0.4.
2. **User roster [SIGN-OFF].** `docs/ui.md`'s assignee picker, avatar initials and team popover need a user list; no endpoint lists users today. Embedding `assignee { id, userName }` in task payloads covers cards and detail but not the picker. Proposal: a minimal `GET api/v1/users` → `[{ id, userName, role }]` with policy `ViewTasks` (not `ViewMembers`, which Members do not hold). This is a new endpoint beyond the written F01 scope.

## T7. Risks

| Risk | Impact | Mitigation |
|---|---|---|
| **R-TZ — blocking, confirmed by experiment 2026-10-25.** `TaskFlow.Api.csproj` sets `InvariantGlobalization=true`. Under that setting on this Windows machine, `TimeZoneInfo.FindSystemTimeZoneById("Asia/Ho_Chi_Minh")` throws `TimeZoneNotFoundException`; with the setting off it resolves to +07:00. ROADMAP R6 scheduled this "before F05", but F01 needs the zone for the due-date rule and history rendering — it is due **now** | Create with a due date, reopen, and every history read would throw 500 on the dev machine | Set `<InvariantGlobalization>false</InvariantGlobalization>` in `TaskFlow.Api.csproj`: one line, reversible, ~28 MB of ICU in the image, and the Debian-based `aspnet:10.0` image already carries libicu. **[SIGN-OFF]** |
| AC15 and AC22 are unreachable until R1's `InvalidModelStateResponseFactory` lands | Two criteria cannot be demonstrated | Slice 0.3 precedes slice 1.1 |
| R4 is masked on the delete path: `SaveChangesInternal` early-returns before the soft-delete conversion when nothing is Added or Modified, and the `Deleted` history insert happens to keep the path working | A future delete path that writes no history would **hard delete** | Slice 0.3 fixes it; until then, add no delete path without a history row |
| A `Guid` token means any edit invalidates a rendered card's version, so a title edit makes a concurrent move fail with `Conflict` | More conflicts than a status-only token would produce | Accepted and intended (AC30); the client refetches on `Conflict` |
| `DateTimeOffset` on MySQL stores `datetime(6)` and discards the offset | A non-UTC value written by hand would silently lose its offset | Every timestamp comes from `DateTimeHelper.GetDtOffset()` (UTC), as every existing column does |
| History grows unbounded; retention is explicitly out of F04 scope | Table growth | Paged reads plus the `(TaskId, CreatedAt, Id)` index |
| F02 will want a concurrency token on the assignee row. `User.ConcurrencyStamp` already is one, but `UserManager` rewrites it | A second token added in F02 would be redundant and conflicting | Recorded here; F02 decides |
| **Assumption [SIGN-OFF]** — reopening a task whose due date has already passed writes **two** entries, `Reopened` and an `OverdueMarked` whose actor is the reopening user. F05 D7 says every transition into the overdue state gets an entry; F04 AC8 constrains only the job's actor | Changes what the timeline shows after a reopen | Recommended as written; it is F05 territory touched by AC23 |
| **Assumption** — creating a task with an assignee, priority and due date writes exactly **one** `Created` entry, not three | F04 AC1 wording | Recommended as written |
