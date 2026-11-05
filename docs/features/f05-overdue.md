# F05 — Deadline & Overdue Management

| | |
|---|---|
| **Source** | `docs/brs/taskflow-brs.md` § F05 |
| **Status** | Planned — all open questions resolved 2026-10-20 |
| **Depends on** | [F01](f01-workflow.md) task model, `Done`, reopen · [F04](f04-audit.md) the `System` history entry |

**Goal** — tasks that pass their deadline without being completed are flagged automatically, so delays are visible without anyone checking task by task.

## Scope

**In scope** — the overdue rule, the daily scan that applies it, clearing the flag when a task is completed or its deadline moves into the future, and validating a newly set deadline.

**Out of scope** — the red indicator itself (the API exposes a flag; rendering belongs to the client); reminders or escalation before a deadline; SLA reporting; working-day or holiday calendars.

## Decisions

- **D1** `IsOverdue` is a **stored flag**, not computed per read. The BRS requires a history entry at the moment a task becomes overdue ([F04](f04-audit.md) AC8), and an event needs a transition to anchor to.
- **D2** the flag is also recomputed **synchronously** whenever `DueDate` or `Status` changes, and on reopen, so a user never sees a stale flag while waiting for the nightly run.
- **D3** the daily scan is per-task isolated: one failing task is logged and skipped, the rest of the scan continues.
- **D4** `DueDate` is a `DateOnly`. "Today" is resolved in **Asia/Ho_Chi_Minh**, read from a configuration key — never hard-coded — while all timestamps remain UTC. A task is overdue once today, in that zone, is **after** its due date; a task due today is not overdue.
- **D5** the scan runs daily at **00:05 Vietnam time** on the scaffold's existing Quartz scheduler. No new infrastructure.
- **D6** the scan covers **every task that is not `Done`, including `Backlog`** — this is the BRS's definition of overdue, applied literally.
- **D7** each time a task transitions into the overdue state it gets a new `OverdueMarked` entry, including after a deadline was extended and has passed again.
- **D8** a due date in the past is rejected as a **business** validation (it needs "today" in the configured zone), checked **only when the due date changes**, and today's date is accepted. A date already in the past on an untouched task is never re-validated.

## The rule

A task is overdue when **both** hold:
- it has a `DueDate` and that date is before today in the configured zone, and
- its status is not `Done`.

A task due *today* is not overdue. A task with no `DueDate` is never overdue.

## Acceptance criteria

**The rule**

- **AC1** Given a task due three days ago and still `InProgress`, when the daily scan runs, then it is flagged overdue.
- **AC2** Given a task due today (Vietnam time) and not `Done`, when the scan runs, then it is not flagged.
- **AC3** Given a task due yesterday and already `Done`, when the scan runs, then it is not flagged.
- **AC4** Given a task with no due date, however old, when the scan runs, then it is never flagged.
- **AC5** Given a task in `Backlog` whose due date has passed, when the scan runs, then it **is** flagged (D6).
- **AC6** Given it is 00:30 on 21 October in Vietnam — still 20 October in UTC — when the rule is evaluated for a task due 20 October, then it is overdue, because "today" follows the configured zone and not the server's clock (D4).

**Clearing**

- **AC7** Given an overdue task, when its due date is extended to a future date, then the flag is cleared within that same request — not at the next scan.
- **AC8** Given an overdue task, when it is approved into `Done`, then the flag is cleared regardless of how late it was.
- **AC9** Given an overdue task, when its due date is removed entirely, then the flag is cleared (AC4 applies).
- **AC10** Given a `Done` task that was never overdue, when a Manager reopens it and its due date is in the past, then it is flagged in that same request ([F01](f01-workflow.md) AC23).
- **AC11** Given an overdue task that is reopened, when the reopen completes, then the flag reflects the current due date rather than being carried over blindly (D2).

**Setting a deadline (D8)**

- **AC12** Given a caller sets a due date earlier than today, when the request is made, then it is refused as a business failure with its own error code, and neither the date nor the flag changes.
- **AC13** Given a caller sets the due date to today, when the request is made, then it is accepted and the task is not overdue.
- **AC14** Given a task whose due date is already in the past, when any other field is updated, then the request succeeds — the past date is not re-validated because it did not change (D8).
- **AC15** Given a caller without `UpdateTasks`, when they attempt to change a due date, then the response is forbidden and neither the date nor the flag changes.

**The job**

- **AC16** Given several tasks became overdue, when the scan runs, then each is flagged and each gets exactly one history entry whose actor is `System`.
- **AC17** Given the scan already ran today, when it runs again, then no task is flagged twice and no duplicate history entry is written.
- **AC18** Given no task qualifies (the empty case), when the scan runs, then it completes successfully, changes nothing, writes no history, and logs the number examined.
- **AC19** Given the service was down for two days, when the scan next runs, then every task now past due is flagged — the rule is a comparison against today, not a per-day delta, so no day can be "missed".
- **AC20** Given one task fails to update during the scan, when the scan finishes, then the remaining tasks are still flagged and the failure is logged with its cause (D3).
- **AC21** Given the scan runs while a user is moving the same task, when both complete, then the task is left in a consistent state and the history shows exactly what happened.
- **AC22** Given the scan runs outside any HTTP request, when its history entries are written, then the actor is recorded as `System`, not as user `0` or an empty actor.
- **AC23** Given a task was flagged, had its deadline extended, and has now passed the new deadline, when the scan runs, then it is flagged again and a **second** `OverdueMarked` entry is written (D7).
- **AC24** Given two scheduler instances in a cluster, when the daily trigger fires, then the scan body executes once, not once per instance.

**Missing cases the BRS does not cover**

- **AC25** Given a soft-deleted task that is past due, when the scan runs, then it is not flagged and no history entry is written for it.
- **AC26** Given existing tasks already past due when this feature ships, when the first scan runs, then each is flagged once and gets one `System` entry — not one entry per day of lateness.

## Technical notes

- Quartz is already configured with a clustered ADO job store (`ScheduleInjection`), so the scan needs `[DisallowConcurrentExecution]` (AC24) and must work in batches rather than loading every task at once. The scaffold's `ExampleJob` contains an infinite `while (true)` loop — it is a demo, not the pattern to copy.
- The job is scheduled from a singleton: it must resolve scoped services inside an explicitly created scope (`backend.md` §10), and it has no `HttpContext`, which makes AC22 a real risk rather than a theoretical one.
- Idempotency (AC17) comes from the predicate: select only tasks where the rule holds **and** `IsOverdue` is still false. Never "flag everything due before today" unconditionally. D7 is compatible with this, because clearing the flag is what makes a second marking possible.
- "Today in Asia/Ho_Chi_Minh" is computed once per scan and passed down, not recomputed per row — otherwise a scan spanning midnight would use two different dates. The zone comes from configuration (D4) through a declared key constant, so a deployment in another zone needs no code change.
- With `DueDate` as `DateOnly` the comparison is a plain date comparison in SQL — no offset arithmetic, no "start of day" conversions, and no dependence on the server's locale.
- The candidate query needs an index covering `DueDate`, `Status` and `IsOverdue`.
- D1's stored flag must be maintained in exactly one place that both the job and the write paths call, or AC7 and AC16 will drift apart.

---

# Technical contract (designed 2026-11-05)

Everything above is the decided behaviour and does not change. This section is the technical contract that makes it implementable, following [F01](f01-workflow.md) and [F02](f02-capacity.md)'s *Technical contract*. Items marked [SIGN-OFF] await the user's approval; slices are in `docs/ROADMAP.md` → *Unit 5*; ADR [0003](../adr/0003-f05-overdue-scan-and-quartz-schema.md) is **Proposed**.

## T0. Already shipped by F01 (audit 2026-11-05)

`TaskItem.IsOverdue`, the single predicate `TaskOverdueRule.IsOverdue` (`BE/TaskFlow.Common/Constants/TaskOverdueRule.cs`), the zone key `ConfigKeys.Workflow.TimeZone`, and the synchronous recompute on create, due-date change, move→`Done` and reopen (`TaskService.cs`), including reopen's `OverdueMarked` entry with the reopening user as actor (F01 T7). AC7–AC15 hold in code today; AC10 and AC12 are tested, the rest get regression tests in slice 5.1. Missing: the scan (AC1–AC6, AC16–AC26), its index, the Quartz wiring, R11's Quartz schema fix, the `isOverdue` list filter, the overdue view.

## T1. The scan service

- `BE/TaskFlow.BusinessLogic/Services/TaskOverdueScanService.cs` — `ITaskOverdueScanService.Run(DateOnly todayInZone, CancellationToken cancellationToken)` → `Task<TaskOverdueScanResultDto>` (`Examined`, `Flagged`, `Skipped`, `Failed`; DTO in `Dtos/Tasks/`). Scoped, registered in `CoreDependencyConfiguration.AddServices` (not behind `skipQuartz`, so tests resolve it). No HTTP surface.
- `todayInZone` is computed **once** by the caller and passed down (Technical notes, AC6). It is also the test seam: tests pass a fixed date. No `TimeProvider` is introduced.
- **Candidates** — no tracking, ids only, keyset batches of 100: `!IsOverdue && Status != Done && DueDate != null && DueDate < today && Id > lastId`, `ORDER BY Id`. The query filter excludes soft-deleted rows (AC25). Keyset, not offset: flagged rows leave the predicate and failed rows stay, so offset paging would skip rows and "take the first 100 again" would never terminate.
- **Per candidate, its own commit (D3):** load tracked by id (as `Move` does) → re-check `TaskOverdueRule.IsOverdue(task.DueDate, task.Status, today) && !task.IsOverdue` (the row may have changed since the candidate read; the SQL is only a pre-filter, the predicate lives in one place) → `IsOverdue = true`, `Version = Guid.NewGuid()`, add `TaskHistory { Event = OverdueMarked, ActorUserId = null }` → one `SaveChanges()` (task + entry atomic, F04 AC10) → `ChangeTracker.Clear()` in `finally`.
- **Outcomes per row:** written → `Flagged`; re-check false → `Skipped`; `DbUpdateConcurrencyException` or `TaskConcurrencyHelper.IsDeadlock` → `Skipped`, `LogWarning(ex, …)` (a concurrent user write won, AC21; ROADMAP R13); any other exception → `Failed`, `LogError(ex, "Overdue scan failed to flag task {TaskId}", id)`, continue (AC20). Cancellation is checked between rows.
- **End of run:** `LogInformation("Overdue scan for {Today} examined {Examined}, flagged {Flagged}, skipped {Skipped}, failed {Failed}", …)` — the empty case logs `Examined = 0` (AC18).
- **Actor (AC22, F04 AC8):** `ActorUserId = null` written explicitly. `UnitOfWork` stamps `CreatedBy/UpdatedBy = 0` (no `HttpContext`); that value is never read as the actor (F04 T1).
- **Rejected — one bulk `UPDATE … WHERE`** (`ExecuteUpdate`): bypasses `UnitOfWork` stamping and the `Version` token, cannot write one history row per task without raw `INSERT … SELECT`, and one bad row fails the whole set (D3, AC20). Per-row commits cost one load + one commit per flagged task — at one-shared-board volume, seconds on the first run (AC26), near zero on later runs.

## T2. Idempotency and concurrency (AC17, AC21, AC23, AC24)

1. **Predicate** — only `IsOverdue = false` rows qualify, so a second run the same day finds nothing (AC17). Clearing the flag (due-date change, `Done`) is what makes a second marking possible (D7, AC23). The rule compares against today, not a per-day delta, so downtime cannot skip a day (AC19, AC26).
2. **Row token** — every flag bumps `TaskItem.Version` under F01 T2's guard. Scan vs user move on the same task (AC21): if the user commits first, the scan's `UPDATE … WHERE Version = @original` affects 0 rows (or deadlocks, 1213) → `Skipped`, nothing written. If the scan commits first, the user's request carries the old version → `Task.Move.Conflict` → the client refetches. Either way the task and its history agree.
3. **Scheduler** — the clustered ADO job store fires a trigger on one node, and `[DisallowConcurrentExecution]` blocks overlapping executions across the cluster (AC24). Even if two runs did overlap, layers 1–2 still give each task exactly one entry; slice 5.1 tests that directly.
- **[SIGN-OFF] Consequence of 2:** a board card left open since before 00:05 for a task the scan flagged gets one `Conflict` on its next write, then refetches. Same behaviour as any concurrent edit (F01 T7).

## T3. Index

`IX_Tasks_IsDeleted_IsOverdue_Status_DueDate` on `(IsDeleted, IsOverdue, Status, DueDate)`, `MyDbContext` migration `AddTasksOverdueScanIndex` — **Additive** (`CREATE INDEX` only, online InnoDB, board-sized table). It serves the scan (equality on `IsDeleted` and `IsOverdue = 0`, then `Status`, then `DueDate`) and the `isOverdue=true` filter (T8, prefix `IsDeleted, IsOverdue = 1`). This refines F01 T1's sketch `(IsDeleted, Status, IsOverdue, DueDate)`: `Status != Done` is a range, so columns after it cannot narrow the scan, and `Done` tasks with past due dates accumulate forever. Record `EXPLAIN` for the candidate query in the slice report. The keyset `ORDER BY Id` sorts the small candidate set in memory — accepted.

## T4. "Today" in the configured zone (D4, AC6)

- New pure helper `DateTimeHelper.ToDateInZone(this DateTimeOffset source, string timezoneName) → DateOnly`, beside `ToTimezone`. `TaskService.TodayInZone()` delegates to it (one-line body change), and the job uses it, so there is one definition of today.
- `Program.cs` fails startup when `Workflow:TimeZone` is missing or does not resolve (`FindSystemTimeZoneById`), the same way `Cors:AllowedOrigins` and `Workflow:MaxInProgressPerAssignee` are checked.

## T5. Quartz wiring (D5, AC24)

- **Job** — `BE/TaskFlow.BusinessLogic/Schedulers/BackgroundJobs/OverdueScanJob.cs` (folder of the removed `ExampleJob`), `[DisallowConcurrentExecution]`. Constructor takes singletons only — `ILogger<OverdueScanJob>`, `IConfiguration`, `IServiceScopeFactory` — because `JobFactory` resolves jobs from the root provider. `Execute`: `today = DateTimeHelper.GetDtOffset().ToDateInZone(zone)` once → `await using var scope = scopeFactory.CreateAsyncScope()` → resolve `ITaskOverdueScanService` → `Run(today, context.CancellationToken)`. `catch (Exception ex)` → `LogError(ex, "Overdue scan failed")`, no rethrow (no Quartz refire loop; the next run is tomorrow and AC19 covers it).
- **Scheduling** — `BE/TaskFlow.BusinessLogic/HostedServices/OverdueScanHostedService.cs` (`IHostedService`). `StartAsync`: `ScheduleJob<OverdueScanJob>("OverdueScan", "0 5 0 * * ?", timeZone: <zone from Workflow:TimeZone>)`; a `false` result is logged at `Error`. `StopAsync`: shut the scheduler down, waiting for the running job. The cron is a named constant in the hosted service (D5 fixes the time; only the zone is configuration, D4).
- **Registration** — `AddTransient<OverdueScanJob>()` and `AddHostedService<OverdueScanHostedService>()` inside `ScheduleInjection.AddQuartzScheduler`, so the `Testing` host never schedules.
- **[SIGN-OFF] `IScheduleService` cron overload** (no callers today) — gains `TimeZoneInfo? timeZone = null`; the trigger is built `WithCronSchedule(cron, b => b.InTimeZone(timeZone).WithMisfireHandlingInstructionFireAndProceed())` by one static method (`ScheduleService.BuildCronTrigger`) so a test can assert its next fire time. **Schedule only when missing or changed:** if the existing trigger is a cron trigger with the same expression and zone id, keep it; otherwise `scheduler.ScheduleJob(jobDetail, [trigger], replace: true)`. The `DeleteJob`-then-schedule step goes away: it raced between two starting nodes, could delete a job running on another node, and threw away the persisted trigger, so a missed 00:05 was never made up. With the trigger kept, a run missed while every node was down fires once when the scheduler next starts. The two `LogError("… {ex}", ex)` calls in this file become `LogError(ex, "…")` (known defect, in the method being touched).
- **Order at startup** — `Program.cs` runs `RunQuartzMigration()` before `app.Run()`, and hosted services start inside `app.Run()`, so the schema is migrated before the trigger is written.

## T6. `QuartzContext` migration (ROADMAP R11)

- Generate with the Quartz migration command (`--context QuartzContext --output-dir Quartz-Migrations`), name `AlignQuartzSchemaWith3_22`. `QuartzContext` has no model, so `Up`/`Down` are hand-written `migrationBuilder.Sql`, matching `20240828071644_InitDatabase.cs`:
  - Up: `ALTER TABLE QRTZ_TRIGGERS ADD COLUMN MISFIRE_ORIG_FIRE_TIME BIGINT NULL, ADD COLUMN EXECUTION_GROUP VARCHAR(200) NULL, ADD COLUMN PREFERRED_NODE VARCHAR(200) NULL, ADD COLUMN PREFERRED_NODE_AUTO BOOLEAN NOT NULL DEFAULT FALSE;` then `ALTER TABLE QRTZ_FIRED_TRIGGERS ADD COLUMN EXECUTION_GROUP VARCHAR(200) NULL;`
  - Down: drop the same five columns.
  - Source: official Quartz.NET `database/tables/tables_mysql_innodb.sql` at `v3.22.0` (diffed against `v3.13.1`, 2026-11-05).
- **Safety class: Additive.** Nullable columns, plus one `NOT NULL` with a default; both tables are empty (nothing has been scheduled since R5, and inserts fail against the current schema anyway); MySQL 8.4 adds these columns instantly. MySQL DDL is not transactional: if the second statement fails, the first stays applied with no history row, and the fix is manual. Accepted for two empty tables. `Down()` is valid only together with a Quartz package downgrade.
- **No `EnableRetryOnFailure`** — `QuartzContext`'s `UseMySql` (`ScheduleInjection.cs:65-66`) has none; keep it that way.
- **`AutoMigration=true`** — running the API applies this to the `quartz` database. Applying it, even locally, needs the user's approval (`workflow.md` §3). Where `AutoMigration` is off, it must be applied before the new code starts, or scheduling fails (logged `Error`) and the API runs **without** the scan. Concurrent migration by several instances is undefined until `/ship setup` (`database.md` §6: startup migration assumes a single instance).
- EF rewrites `QuartzContextModelSnapshot.cs` (its `Tricor.BillingProcess…` namespace is scaffold residue). Accept what EF generates; do not hand-edit. Read `dotnet ef migrations script --idempotent --context QuartzContext`.
- **Rule from now on:** any Quartz package upgrade diffs the official MySQL script and adds a `QuartzContext` migration for any difference (ADR 0003).

## T7. D8 on `PUT …/due-date` [SIGN-OFF]

`UpdateDueDate` today refuses any non-null past date, including the date the task already has (`TaskService.cs:537-541`). D8 says the check runs **only when the due date changes**, so the guard becomes `requestDto.DueDate.HasValue && requestDto.DueDate != task.DueDate && requestDto.DueDate < today`. That is a behaviour change on an existing endpoint, not a wire change. Out of scope here: a no-op due-date write still records a `DueDateChanged` entry with equal values (F04 matter, noted only).

## T8. API contract — the `isOverdue` filter

| Verb + route | Policy | Request | Success | Errors |
|---|---|---|---|---|
| `GET api/v1/tasks?status=&isOverdue=&start=&length=` | `ViewTasks` | adds `IsOverdue` `bool?` to `TaskGetListReq` (optional; absent = no filter) | `PaginationResultRes<List<TaskSummaryRes>>`, **shape unchanged** | `Task.GetList.Exception`; a non-boolean value → R1 envelope with `validates` |

- The filter `IsOverdue == value` is applied before ordering; ordering follows F03 unchanged (no `status` → active-column rule across statuses, the approved F03 sign-off; `status=Done` → `Done` window, so `status=Done&isOverdue=true` is always empty).
- `ITaskService.GetList` gains `bool? isOverdue` (internal). Additive, non-breaking; announced here as F01 T5 anticipated.
- **No other wire change:** `isOverdue` is already on `TaskSummaryRes`/`TaskDetailRes`; `OverdueMarked` already renders as "System flagged the task as overdue". **F05 adds no error codes** — `Task.Create.DueDateInPast` and `Task.UpdateDueDate.DueDateInPast` exist; the job has no client-facing failure.

## T9. Client

- Card indicator (danger border + clock icon + "Overdue · <date>" chip), detail "Overdue" label and the System timeline icon already exist (slices 1.5–1.6). Unchanged.
- `getOverdueTasks(start, length, signal)` in `FE/src/api/tasks.ts` → `GET /tasks?isOverdue=true&start=&length=`.
- `FE/src/shared/hooks/useOverdueTasksQuery.ts` — `useInfiniteQuery` keyed `['board', 'overdue']`, so every existing `invalidateQueries(['board'])` refreshes it (F03 precedent); `length` 50, sent explicitly. In `shared/` because both `board` and `overdue` read it (`docs/ui.md` import direction).
- `FE/src/features/overdue/OverdueListPage.tsx`, route `/overdue` (`router.tsx`), nav links Board · Overdue in `AppLayout.tsx`. Content: title "Overdue tasks", the wireframe's one-line rule copy, a table Task (`#<id>` + title, link to `/tasks/:id`) · Status · Priority (icon + word) · Due · Assignee, in server order. States: loading, error with retry, empty ("Nothing is overdue."), success; "Show more" (real `<button>`) while loaded < `pagination.totalItems`; the table scrolls inside its container at 390px.
- Board chip "N overdue" becomes a link to `/overdue` and takes its count from the query's `pagination.totalItems` (today it counts page 1 only, `BoardPage.tsx:216-217`).
- **[SIGN-OFF] Not built:** "Due today — not overdue yet" (needs a server-side today/due-date filter, not in F05), "Flag cleared this week" (`docs/ui.md` says leave out), "Last check" (no endpoint), "Late by N days" / `Overdue 1d` (needs today in the configured zone; the browser's date only approximates it, `TaskCard.tsx:15-21`).

## T10. Risks

| Risk | Impact | Mitigation |
|---|---|---|
| Quartz schema behind the package (R11) | Any scheduling fails with a SQL error | T6, slice 5.2; startup logs `Error` if scheduling fails |
| Trigger in the server's zone | Scan at 07:05 VN on a UTC host | `InTimeZone` from config (T5); next-fire-time test |
| Scan vs user write | Lost update or duplicate entry | Per-row `Version` token + re-check (T2); race test on real MySQL |
| Two nodes / double fire | Duplicate `OverdueMarked` | Cluster + `[DisallowConcurrentExecution]` + predicate; concurrent-run test |
| Migration not applied where `AutoMigration=false` | API runs without the scan, silently except one `Error` log | Release step under `/ship setup`; `/healthz` has no checks (R8) — accepted |
| Node dies mid-scan | Remaining tasks wait for the next run | Accepted: idempotent, next run ≤ 24 h; no `RequestRecovery` |
| Non-transactional DDL | Half-applied Quartz migration | Accepted: two statements on empty tables; manual fix documented |
| Candidate SQL vs `TaskOverdueRule` drift | Scan misses or over-selects | Per-row re-check with the rule before every write; test across every status × date combination |
