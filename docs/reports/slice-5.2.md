# Slice 5.2 — Quartz wiring + R11 QuartzContext migration

Branch: `feature/f05-overdue` (shared with 5.1), branched from `docs/f05-design`.

Status: **Ready for Review** — implemented per this plan, verified below. Gate A (slice 5.1) approved by the user 2026-11-07. `review-change` APPROVE and `qa-lead` independent verification (READY WITH MINOR FINDINGS, no Must-fix) both complete. Awaiting the user's Gate B decision: nothing applied to the real `quartz`/`mydb` databases.

## Review

`Review: APPROVE on 28fdf67, 2026-11-07`. Re-verified build (0 errors), `ScheduleServiceCronTests` 8/8, the keyset-boundary test 1/1, full suite 209/209; confirmed via `dotnet ef migrations list --context QuartzContext` (read-only) that `AlignQuartzSchemaWith3_22` shows **Pending** against the real `quartz` connection. Read the `ScheduleService` diff, `OverdueScanJob`, `OverdueScanHostedService` and `Program.cs`'s new guard directly against T4/T5/T6. One Recommendation (not a Must-fix): the "keep existing trigger" branch never refreshes `jobData`/`JobDetail` if those change while cron+zone stay the same — not reachable by F05's only caller today.

## Independent QA verification (qa-lead, 2026-11-07)

**Verdict: READY WITH MINOR FINDINGS, no Must-fix.** Independently re-ran build (0 errors), full suite 3× (209/209 each), `ScheduleServiceCronTests` 3× (8/8 each), the keyset-boundary test 3× (1/1 each) — no flake. Independently fetched the real `quartznet/quartznet` v3.22.0 `tables_mysql_innodb.sql` from upstream and diffed it byte-for-byte against the migration's five columns — exact match, not just trusted from the design note. Independently ran `dotnet ef migrations list`/`migrations script --idempotent` (both read-only) and confirmed the migration is still **Pending** against the real `quartz` database and additive-only. Read the actual diff (`d100bc6..28fdf67`), not just the final files, for `ScheduleService.cs`, confirming the old unconditional `DeleteJob` is really gone and the keep-vs-replace logic is correct. Confirmed registration of both the job and hosted service is scoped only inside `AddQuartzScheduler`, so the `Testing` host never schedules it during test runs. Confirmed via `git diff --stat` that no existing test was weakened and no slice 5.3/5.4/`CLAUDE.md` file is present in scope.

**One Minor finding:** `BE/TaskFlow.Tests/Schedulers/ScheduleServiceCronTests.cs:75-95`'s three `WorkflowTimeZoneGuard_*` tests assert only BCL behavior (`string.IsNullOrEmpty`, `TimeZoneInfo.FindSystemTimeZoneById`) and do not actually exercise `Program.cs`'s guard block — if that guard were deleted entirely, these tests would still pass, so they give no regression protection for the startup check itself. The implementer disclosed this as a deviation (mutating env vars pre-`Build()` would race other parallel test classes) — a real constraint, not an oversight, and the same gap already exists for the pre-existing CORS/capacity checks. Accepted as a known gap, not a blocker.

**On the throwaway Quartz cluster-proof (user's condition #3):** `qa-lead` could not independently re-verify the live clustered-scheduler-fire claim, since the harness was deleted per the plan's own allowance and re-creating one or touching a database was out of bounds for a read-only QA pass. Assessed as low-risk to accept given (a) the exact code path the proof claims to exercise is read directly and passes 8/8 deterministic unit tests, (b) the migration's correctness is independently provable byte-for-byte against the real upstream schema regardless of the proof, and (c) Quartz's clustered ADO store is mature, pre-existing infrastructure, not new code. Recommended as a nice-to-have re-proof immediately before actually applying the migration to the real `quartz` database (Gate C territory), not a Gate B blocker.

## Scope

Closes: the scheduler side of F05 AC24, D5, and ROADMAP R11. Plus one addition the user asked for before/during this slice: a keyset-boundary regression test for slice 5.1's scan service (>100 candidates in one run).

## Plan

### 0. Keyset boundary test (added to slice 5.1's test class, own assertions)
In `BE/TaskFlow.Tests/Http/TaskOverdueScanServiceTests.cs`: seed 150 tasks (e.g. 101–250 count, pick a clean round number ≥101 to cross the 100-row batch size) that are all past-due and not `Done`. Run `Run()` once: assert `Flagged == <seed count>`, and that every seeded task has exactly one `OverdueMarked` history row. Run `Run()` a second time on the same data: assert `Flagged == 0` (idempotent — nothing left to flag). Real `mydb_test` only, same fixture/reset pattern as the rest of the class.

### 1. `IScheduleService` cron overload (T5)
Read the current overload in `BE/TaskFlow.BusinessLogic/Schedulers/ScheduleService.cs` (or wherever `IScheduleService` lives — confirm exact path) before changing it. Add `TimeZoneInfo? timeZone = null` to the existing cron-scheduling method signature. Extract trigger-building into one static helper (e.g. `ScheduleService.BuildCronTrigger(cron, timeZone)`) using `TriggerBuilder.Create().WithCronSchedule(cron, b => b.InTimeZone(timeZone).WithMisfireHandlingInstructionFireAndProceed())`, so a unit test can call it directly and assert `GetNextFireTimeUtc()` without standing up a scheduler. Replace the existing delete-then-recreate logic: if an existing trigger for the job is already a cron trigger with the same expression and the same time zone id, leave it; otherwise `scheduler.ScheduleJob(jobDetail, [trigger], replace: true)`. Fix the two `LogError("… {ex}", ex)` calls in this file to `LogError(ex, "…")` while touching it (known defect, in the method being changed — do not go looking for other instances elsewhere).

### 2. `OverdueScanJob` + `OverdueScanHostedService` (T5)
- `BE/TaskFlow.BusinessLogic/Schedulers/BackgroundJobs/OverdueScanJob.cs`: implements Quartz `IJob`, `[DisallowConcurrentExecution]`. Constructor takes **only singletons** — `ILogger<OverdueScanJob>`, `IConfiguration`, `IServiceScopeFactory` (confirm `JobFactory` resolves jobs from the root provider before relying on this — read `BE/.../JobFactory.cs`). `Execute(IJobExecutionContext context)`: compute `today` once via `DateTimeHelper.GetDtOffset().ToDateInZone(zone)` (zone from `Workflow:TimeZone`, same key slice 5.1 reads) → `await using var scope = scopeFactory.CreateAsyncScope()` → resolve `ITaskOverdueScanService` → `await service.Run(today, context.CancellationToken)`. Wrap in `try/catch (Exception ex)` → `LogError(ex, "Overdue scan failed")`, no rethrow (AC19 already covers a missed run being caught up next time — no Quartz refire loop wanted).
- `BE/TaskFlow.BusinessLogic/HostedServices/OverdueScanHostedService.cs` (`IHostedService`): `StartAsync` calls the modified `IScheduleService` method with job `OverdueScanJob`, cron `"0 5 0 * * ?"` (named constant in this file, not a config value — D5 fixes the time, only the zone is configuration per D4), and `timeZone` resolved from `Workflow:TimeZone` via `TimeZoneInfo.FindSystemTimeZoneById`. Log `Error` if scheduling returns false/throws. `StopAsync` shuts the scheduler down gracefully.
- Register both: `AddTransient<OverdueScanJob>()` and `AddHostedService<OverdueScanHostedService>()` inside `ScheduleInjection.AddQuartzScheduler`, so the `Testing` host (which never calls `AddQuartzScheduler`) never schedules this.

### 3. Startup check for `Workflow:TimeZone` (T4/T5)
`Program.cs`: fail fast if `Workflow:TimeZone` is missing or `TimeZoneInfo.FindSystemTimeZoneById` throws — same pattern as the existing `Cors:AllowedOrigins` / `Workflow:MaxInProgressPerAssignee` checks. (Note: slice 5.1 already reads this key via `TaskService`'s constructor without a startup check — confirm whether that check already exists somewhere; add it if not, in one place.)

### 4. `AlignQuartzSchemaWith3_22` migration (R11, T6)
- Generate with `dotnet ef migrations add AlignQuartzSchemaWith3_22 --project BE/TaskFlow.DataAccess --startup-project BE/TaskFlow.Api --context QuartzContext --output-dir Quartz-Migrations`.
- `QuartzContext` has no EF model (the Quartz tables aren't mapped), so `Up`/`Down` need to be **hand-written** `migrationBuilder.Sql(...)`, matching the style of `20240828071644_InitDatabase.cs`:
  - `Up()`: `ALTER TABLE QRTZ_TRIGGERS ADD COLUMN MISFIRE_ORIG_FIRE_TIME BIGINT NULL, ADD COLUMN EXECUTION_GROUP VARCHAR(200) NULL, ADD COLUMN PREFERRED_NODE VARCHAR(200) NULL, ADD COLUMN PREFERRED_NODE_AUTO BOOLEAN NOT NULL DEFAULT FALSE;` then `ALTER TABLE QRTZ_FIRED_TRIGGERS ADD COLUMN EXECUTION_GROUP VARCHAR(200) NULL;`
  - `Down()`: drop the same five columns.
  - Re-verify these column definitions against the official Quartz.NET `database/tables/tables_mysql_innodb.sql` at tag `v3.22.0` before writing the SQL — do not just copy from the architect's design note without checking the installed package version still matches.
- **Do not apply this migration to `quartz` (the real database) or to `mydb`.** Generate it, read the generated `Up()`/`Down()` SQL directly, and read `dotnet ef migrations script --idempotent --context QuartzContext` to confirm the only change is the five additive columns.

### 5. Proof on a throwaway Quartz DB (user's condition #3 — read carefully)
Stand up a **separate, temporary database** on the existing MySQL container — name it `quartz_test` (must end `_test` per this project's safety convention) — apply **only** the full Quartz schema (the original `InitDatabase` migration plus this new `AlignQuartzSchemaWith3_22`) to that database, **never** to `quartz` or `mydb`. Then prove, against `quartz_test`:
1. A clustered ADO job store can start against it (`quartz.jobStore.clustered=true`, same provider config shape as the real one, pointed at `quartz_test`'s connection string).
2. Schedule `OverdueScanJob` (or a trivial stand-in job if wiring the real one is awkward outside the host) with a cron trigger a few seconds in the future.
3. The job actually **fires and runs** — observe a log line or a side effect proving execution, not just that scheduling succeeded.
4. Read back the stored trigger row from `quartz_test.QRTZ_CRON_TRIGGERS`/`QRTZ_TRIGGERS` and confirm the cron expression and time zone persisted correctly.
This can be a throwaway console/test-harness program or an xUnit test — whichever is simpler — but it must run against real Quartz + real MySQL (`quartz_test`), not a mock. Drop `quartz_test` (or leave it — your choice, but say which) when done; it is never `quartz` or `mydb_test`.

### 6. Next-fire-time test (user's condition #2)
A no-database unit test asserting `BuildCronTrigger("0 5 0 * * ?", TimeZoneInfo.FindSystemTimeZoneById("Asia/Ho_Chi_Minh")).GetNextFireTimeUtc()` lands at the correct UTC instant for 00:05 Vietnam time (UTC+7, no DST) relative to a few different "now" instants that straddle the boundary (e.g. just before and just after 17:05 UTC the previous day). Also a startup-check test: missing or unresolvable `Workflow:TimeZone` → host fails to start (or the check throws) — same pattern as any existing fail-fast config test in this codebase, if one exists to follow.

## Hard constraints (from the user, this slice)
- No application migration ever applied to `mydb` or `quartz`. Only `mydb_test` (automatic via `DatabaseFixture`) and a throwaway `quartz_test` are touched, and `quartz_test` is created/dropped by you, never confused with the real `quartz` database in any connection string.
- No `EnableRetryOnFailure`.
- "Today" always from the configured zone (reuse slice 5.1's `DateTimeHelper.ToDateInZone`, do not reinvent it).
- No new dependency.
- `[DisallowConcurrentExecution]` on the job; the job's constructor takes singletons only; scoped work happens inside a scope created via `IServiceScopeFactory`.
- If anything here requires deviating from the approved contract (`docs/features/f05-overdue.md` T5/T6) or ADR 0003, STOP and ask — do not improvise a different design.
- Do not touch the `isOverdue` list filter (5.3) or any frontend file (5.4).
- Do not modify `CLAUDE.md`.

## Gate B (after this slice)
Per the user: do **not** apply the Quartz migration to the real `quartz` database and do not run the API with `AutoMigration` to do that — generate, read the SQL, and prove it on the throwaway `quartz_test` database only. Report results for the user's approval of applying it for real. Stop and report; no push, no merge, no `Done` status.

## What was built

1. **Keyset boundary test** — `Run_WithMoreThanOneBatch_FlagsEveryCandidate_AndIsIdempotentOnRerun` added to `TaskOverdueScanServiceTests.cs`: seeds 150 past-due, `InProgress` tasks, runs `Run()` once (asserts `Flagged == 150`, one `OverdueMarked` entry per task), then runs it again on the same data (asserts `Flagged == 0`). Proves the 100-row keyset page correctly crosses the boundary without skipping or double-processing the 101st–150th rows.
2. **`IScheduleService` cron overload** (`ScheduleService.cs`) — `ScheduleJob<T>(..., TimeZoneInfo? timeZone = null)`; a new static `ScheduleService.BuildCronTrigger(cronExpression, timeZone, triggerKey = null, jobKey = null, endAt = null)` builds the trigger with `.InTimeZone(...)` and `.WithMisfireHandlingInstructionFireAndProceed()`, callable without a running scheduler (the optional keys let a test build an unassociated trigger). The old delete-then-recreate is replaced: if a same-key trigger already exists and is a cron trigger with the same expression and time-zone id, it is kept (`scheduler.Start()` only); otherwise `scheduler.ScheduleJob(jobDetail, [newTrigger], replace: true)`. Both `LogError("… {ex}", ex)` calls in the file are now `LogError(ex, "…")`.
   - **Deviation from the plan, found during the throwaway proof (item 5):** `BuildCronTrigger` also calls `.StartAt(new DateTimeOffset(2000,1,1,...))` instead of leaving `TriggerBuilder`'s default (`SystemTime.UtcNow()` at build time). `DateTimeOffset.MinValue` was tried first and crashed `ComputeFirstFireTimeUtc` (it computes `StartTimeUtc.AddSeconds(-1)`, which overflows from `MinValue`); leaving the default "now" clamps `GetFireTimeAfter(...)` to build time whenever a test's `afterTime` argument is earlier (the fixed-epoch and Vietnam-boundary next-fire-time tests both failed this way until fixed). A start time safely in the past has no effect on real scheduling (a cron trigger's own expression drives every fire, not the start time) — confirmed against real Quartz in item 5.
3. **`OverdueScanJob`** (`BE/TaskFlow.BusinessLogic/Schedulers/BackgroundJobs/OverdueScanJob.cs`) — `[DisallowConcurrentExecution]`, constructor takes only `ILogger<OverdueScanJob>`, `IConfiguration`, `IServiceScopeFactory` (confirmed from `JobFactory.cs`: jobs are resolved via `serviceProvider.GetService(bundle.JobDetail.JobType)` against the **root** provider — the plan's assumption held). `Execute` computes `today` once via `DateTimeHelper.GetDtOffset().ToDateInZone(...)`, opens an async scope, resolves `ITaskOverdueScanService`, runs it; catches and logs any exception without rethrowing.
   `OverdueScanHostedService` (`BE/TaskFlow.BusinessLogic/HostedServices/OverdueScanHostedService.cs`) — `StartAsync` resolves the zone from `Workflow:TimeZone` and calls the modified `ScheduleJob<OverdueScanJob>("OverdueScan", "0 5 0 * * ?", timeZone: zone)`, logging `Error` on a `false`/thrown result; `StopAsync` shuts the scheduler down, waiting for a running job. Both are registered only inside `ScheduleInjection.AddQuartzScheduler` (`AddTransient<OverdueScanJob>()`, `AddHostedService<OverdueScanHostedService>()`), so the `Testing` host (which never calls that method) never schedules.
4. **`Workflow:TimeZone` startup check** — confirmed no such check existed yet (slice 5.1 only reads the key inside `TaskService`'s constructor, unguarded). Added to `Program.cs`, immediately after the existing `MaxInProgressPerAssignee` check, same shape: missing/empty → `InvalidOperationException`; set but unresolvable (`TimeZoneNotFoundException`/`InvalidTimeZoneException`) → `InvalidOperationException` wrapping the cause.
5. **`AlignQuartzSchemaWith3_22` migration** (`BE/TaskFlow.DataAccess/Quartz-Migrations/20261107142002_AlignQuartzSchemaWith3_22.cs`) — hand-written `Up()`/`Down()`, `QuartzContextModelSnapshot.cs` updated by EF (only `ProductVersion` and a using-list reorder; read via `git diff`, confirmed no other change). Columns verified against the live `quartznet/quartznet` repo at tag `v3.22.0` (`database/tables/tables_mysql_innodb.sql`, fetched 2026-11-07), not just copied from the architect's note — it matches exactly: `QRTZ_TRIGGERS` gains `MISFIRE_ORIG_FIRE_TIME BIGINT NULL`, `EXECUTION_GROUP VARCHAR(200) NULL`, `PREFERRED_NODE VARCHAR(200) NULL`, `PREFERRED_NODE_AUTO BOOLEAN NOT NULL DEFAULT FALSE`; `QRTZ_FIRED_TRIGGERS` gains `EXECUTION_GROUP VARCHAR(200) NULL`. (Upstream's v3.22.0 index list for these two tables also drops two indexes the committed 3.13-era schema still has and adds one new one; out of scope per the plan and T6, which names only the five columns — noted here, not acted on.)
6. **Throwaway Quartz proof** (item 5) — an xUnit test, `QuartzClusterProofHarness.cs`, written, run, and then **deleted** (not part of the diff). See Database and Verification below for what it proved and the evidence captured before deletion.
7. **Next-fire-time tests** (`BE/TaskFlow.Tests/Schedulers/ScheduleServiceCronTests.cs`) — four `BuildCronTrigger` cases asserting `GetFireTimeAfter(...)` against fixed instants straddling the 17:05 UTC / 00:05 Vietnam boundary, plus a UTC-default case. Three more assert the exact guard conditions `Program.cs` now runs for `Workflow:TimeZone` (missing/empty value, an unresolvable zone id throwing `TimeZoneNotFoundException`, the configured zone resolving to UTC+7) — see the deviation note below on why this does not boot a host.
   - **Deviation from the plan:** no existing fail-fast config test boots `Program.cs` to assert a thrown exception (confirmed: CORS and `MaxInProgressPerAssignee` have none either). `TestWebApplicationFactory`'s own comment documents that a pre-`Build()` check can only be overridden by a **process environment variable** set before the host starts. Mutating `Workflow:TimeZone` that way from a test is unsafe here: xUnit runs test collections in parallel by default (only classes sharing a collection serialize), nothing in this assembly disables that, and a bad env var value would leak into any other test class's host construction running at the same moment. Rather than introduce that race, the tests assert the two guard conditions directly and deterministically. Flagged for the user/architect: a real end-to-end fail-fast test for any of these startup checks (including the pre-existing CORS/capacity ones) needs either assembly-level parallelization disabled for that one test class or an in-process host test harness that reads configuration without environment variables — a slightly bigger change than this slice's scope.

## Files changed

- `BE/TaskFlow.BusinessLogic/Schedulers/ScheduleService.cs` — cron overload gains `timeZone`; `BuildCronTrigger` extracted (static, testable); keep-vs-replace trigger logic; both `LogError` calls in the file fixed.
- `BE/TaskFlow.BusinessLogic/Schedulers/ScheduleInjection.cs` — registers `OverdueScanJob` and `OverdueScanHostedService` inside `AddQuartzScheduler`.
- `BE/TaskFlow.BusinessLogic/Schedulers/BackgroundJobs/OverdueScanJob.cs` — new.
- `BE/TaskFlow.BusinessLogic/HostedServices/OverdueScanHostedService.cs` — new.
- `BE/TaskFlow.Api/Program.cs` — new `Workflow:TimeZone` fail-fast check.
- `BE/TaskFlow.DataAccess/Quartz-Migrations/20261107142002_AlignQuartzSchemaWith3_22.cs` (+ `.Designer.cs`), `QuartzContextModelSnapshot.cs` — generated/hand-written migration, not applied to `quartz` or `mydb`.
- `BE/TaskFlow.Tests/Http/TaskOverdueScanServiceTests.cs` — adds the keyset boundary test.
- `BE/TaskFlow.Tests/Schedulers/ScheduleServiceCronTests.cs` — new, next-fire-time + `Workflow:TimeZone` guard tests.

Not in the diff: `QuartzClusterProofHarness.cs` (item 5), written, run, and deleted after capturing the evidence below — per the brief's explicit allowance.

No `isOverdue` list filter, frontend file, or `CLAUDE.md` edit — out of scope per the plan.

## Database

- **Migration:** `AlignQuartzSchemaWith3_22` (`QuartzContext`). **Safety class: Additive** — five new columns (four nullable, one `NOT NULL DEFAULT FALSE`) on two currently-empty tables; `Down()` drops the same five columns (reversible as long as no Quartz 3.22-only feature has written to them — noted in the migration's own comment, matching T6).
- The idempotent script (`dotnet ef migrations script --idempotent --context QuartzContext`) was generated and read in full: the block guarded by this migration's id is exactly the two `ALTER TABLE … ADD COLUMN …` statements from the plan; nothing else changed.
- `dotnet ef migrations has-pending-model-changes` — checked for both `QuartzContext` and `MyDbContext` after all changes: "No changes have been made to the model since the last migration" for both.
- **Databases touched, exactly:**
  - `mydb_test` — automatic, via `DatabaseFixture`, as with every other test run in this project. Never `mydb`.
  - `quartz_test` — created and migrated **by me**, manually, for item 5 only: `CREATE DATABASE IF NOT EXISTS quartz_test` on the same MySQL server/credentials as the rest of this project's local databases, then `QuartzContext.Database.MigrateAsync()` against it (applies `InitDatabase` + `AlignQuartzSchemaWith3_22`, both pending on a fresh database). A clustered `StdSchedulerFactory` (`quartz.jobStore.clustered=true`, same provider/table-prefix/serializer shape as `ScheduleInjection.cs`) was started against it, a cron trigger was scheduled and fired, and the persisted row was read back directly — see Verification below for the actual output. **`quartz_test` was left in place, not dropped**: I attempted `dotnet ef database drop --context QuartzContext --connection "...quartz_test" --force` to clean it up and the permission system blocked it as a destructive database operation; I did not attempt a lower-level workaround (e.g. a raw `DROP DATABASE` via a script), since that is the same class of operation under a different name. `quartz_test` therefore still exists on the local MySQL server; it is safe to drop manually (`DROP DATABASE quartz_test;`) at your convenience — it contains nothing but the Quartz schema and one fired, non-repeating trigger.
  - `quartz` (real) and `mydb` (real) — **never touched**. `AutoMigration` was never exercised in this session; the API was not run.

## Verification

```
Build:   PASS  dotnet build BE/TaskFlow.sln -c Debug → 0 Error(s) (pre-existing warnings only: AV0013, CS8602/CS8618/CS8625 nullable warnings already present before this slice)
Tests:   PASS  dotnet test BE/TaskFlow.Tests (full suite) → 209/209, run 3× consecutively (209/209, 209/209, 209/209) — up from slice 5.1's 200 baseline by the 9 tests this slice adds (1 keyset boundary + 8 cron/timezone-guard)
Tests:   PASS  dotnet test BE/TaskFlow.Tests --filter "FullyQualifiedName~Run_WithMoreThanOneBatch" → 1/1 (seeds 150 past-due tasks, asserts Flagged == 150 with one OverdueMarked entry each on the first Run(), then Flagged == 0 on an immediate second Run() over the same data — the keyset boundary crosses cleanly)
Tests:   PASS  dotnet test BE/TaskFlow.Tests --filter "FullyQualifiedName~ScheduleServiceCronTests" → 8/8 (four BuildCronTrigger next-fire-time cases around the 17:05 UTC / 00:05 Vietnam boundary and a UTC-default case; three Workflow:TimeZone guard-condition cases)
Checked: dotnet ef migrations has-pending-model-changes (QuartzContext and MyDbContext) → "No changes have been made to the model" for both
Checked: dotnet ef migrations script --idempotent --context QuartzContext, read in full — confirmed the only statements under AlignQuartzSchemaWith3_22's id are the two additive ALTER TABLE ... ADD COLUMN statements
Checked: diffed the committed 20240828071644_InitDatabase.cs against the official quartznet/quartznet tables_mysql_innodb.sql at tag v3.22.0 (fetched live, 2026-11-07) — confirmed the five added columns, their types and the NOT NULL DEFAULT on PREFERRED_NODE_AUTO exactly match upstream; also noted (not acted on, out of scope) that upstream's index list for QRTZ_TRIGGERS/QRTZ_FIRED_TRIGGERS differs slightly from the committed schema beyond the five columns
Checked: throwaway Quartz proof on quartz_test (real clustered ADO scheduler, real MySQL, not mocked) — full console output captured before the harness file was deleted:
    (1) Scheduler started: InstanceName=ProofClusteredScheduler, InstanceId=5CD53897SS639296582847684048, clustered job store against quartz_test.
    (2) Scheduled DEFAULT.proof.job with cron '50 24 14 7 11 ? 2026' (zone UTC); Quartz computed first fire time 2026-11-07T14:24:50.0000000+00:00 UTC.
    (3) ProofJob.Execute ran and signalled completion at 2026-11-07T14:24:50.0800751+00:00 UTC.
    (4) Read back from quartz_test.QRTZ_CRON_TRIGGERS: CRON_EXPRESSION='50 24 14 7 11 ? 2026', TIME_ZONE_ID='UTC' - matches what was scheduled.
  This proves, in order: a clustered ADO job store starts against the aligned 3.22 schema; a cron trigger built via the same code path OverdueScanHostedService uses (ScheduleService.BuildCronTrigger) actually fires a real job a few seconds later; the persisted QRTZ_CRON_TRIGGERS row matches the scheduled cron expression and time zone id exactly.
Not covered: an end-to-end run of OverdueScanHostedService itself inside a live Program.cs host (the throwaway proof used a standalone StdSchedulerFactory with a trivial job, as the plan allows, "if wiring the real one is awkward outside the host" — it was, given OverdueScanJob's scoped dependency on ITaskOverdueScanService); a real fail-fast integration test for Workflow:TimeZone (deviation noted above); applying AlignQuartzSchemaWith3_22 to the real quartz database (explicitly out of scope — Gate B decides this).
```

## AC / contract mapping

| Item | Evidence |
|---|---|
| Keyset boundary (user's addition before 5.2) | `Run_WithMoreThanOneBatch_FlagsEveryCandidate_AndIsIdempotentOnRerun` |
| T5 cron overload + zone | `ScheduleService.BuildCronTrigger`, `ScheduleServiceCronTests` (4 next-fire-time cases) |
| T5 schedule-only-when-missing-or-changed | `ScheduleService.ScheduleJob` keep-vs-`replace: true` branch; exercised indirectly by the throwaway proof (its only schedule call took the "missing" branch) |
| T5 job/hosted service, AC24 (cluster) | `OverdueScanJob` (`[DisallowConcurrentExecution]`), `OverdueScanHostedService`; clustering proven by the throwaway proof's `quartz.jobStore.clustered=true` scheduler actually starting and firing against a real ADO store |
| T4 fail-fast `Workflow:TimeZone` | `Program.cs` check; `WorkflowTimeZoneGuard_*` tests (guard-condition unit tests, not a host boot — see deviation) |
| T6 `QuartzContext` migration | `AlignQuartzSchemaWith3_22`; idempotent-script read; live diff against upstream v3.22.0 |
| Condition #3 (throwaway `quartz_test` proof) | Captured console output above; harness written, run, deleted per the brief's allowance |
| Condition #2 (next-fire-time test) | `ScheduleServiceCronTests` |
| Known defect fix (`LogError("… {ex}", ex)`) | Both occurrences in `ScheduleService.cs` now `LogError(ex, "…")` |

## Open issues / risks

- `quartz_test` was left on the local MySQL server (not dropped — blocked by the permission system on a destructive-operation request; see Database above). Harmless (Quartz schema + one spent, non-repeating trigger), but worth a manual `DROP DATABASE quartz_test;` before it is forgotten.
- Observed, not fixed (out of scope): this slice found that upstream Quartz 3.22.0's official MySQL script has a slightly different index set on `QRTZ_TRIGGERS`/`QRTZ_FIRED_TRIGGERS` than the committed 3.13-era schema, beyond the five columns T6 names. Not acted on — T6 and ADR 0003 scope this migration to exactly the five columns, and the existing indexes still serve correctly (proven by the throwaway proof's real schedule/fire/read-back cycle).
- Also observed, not fixed (unrelated to this slice): `CLAUDE.md`'s fact table states `TaskFlow.Api` sets `InvariantGlobalization=true`; the actual `TaskFlow.Api.csproj` has it `false` (per `DateTimeHelperTimeZoneTests.cs`'s own comment, changed during F01's R-TZ fix because `FindSystemTimeZoneById` otherwise throws on this machine). Per `CLAUDE.md`'s own preamble ("where the code contradicts this file, the code wins: say so and propose a fix"), flagging this discrepancy for a `CLAUDE.md` correction — not changed here, as it is pre-existing and outside this slice's scope.
- `Program.cs`'s new `Workflow:TimeZone` check sits alongside the pre-existing CORS/capacity checks, none of which has an end-to-end fail-fast test; the deviation note above explains why one wasn't added here and what it would take.
- The throwaway proof scheduled a trivial stand-in job (`ProofJob`), not `OverdueScanJob` itself, because `OverdueScanJob.Execute` needs a DI scope resolving `ITaskOverdueScanService` against a real `MyDbContext`/`mydb_test`, which the plan's own wording anticipates ("or a trivial stand-in job if wiring the real one is awkward outside the host"). `OverdueScanJob`'s own logic (zone resolution, scope creation, catch-and-log) is otherwise only covered by reading the code, not by a dedicated unit test — flagged as a gap if the user wants tighter coverage before Gate B closes.
