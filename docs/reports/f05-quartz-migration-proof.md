# F05 — `AlignQuartzSchemaWith3_22` re-proof on `quartz_test`

Date: 2026-11-08. Branch: `feature/f05-overdue`. Requested by the user after Gate C, as the short re-proof condition attached to approving slice 5.2's design (`docs/features/f05-overdue.md` → ADR 0003, and the Gate B/C reports).

**Scope and safety:** every command below targets `quartz_test` only, via an explicit `--connection`/connection-string override. No command in this session targeted `quartz` (the real database) or `mydb`. The API was never started. `quartz_test` is left in place (not dropped), as instructed.

## 1. Apply `AlignQuartzSchemaWith3_22` to `quartz_test`

```
$ dotnet ef migrations list --project BE/TaskFlow.DataAccess --startup-project BE/TaskFlow.Api --context QuartzContext \
    --connection "server=localhost;user id=root;password=Password@1234;database=quartz_test"

20240828071644_InitDatabase
20261107142002_AlignQuartzSchemaWith3_22
```

Neither migration was marked `(Pending)` — both were already applied to `quartz_test` from slice 5.2's original throwaway proof (that database was deliberately left in place, not dropped, per the user's instruction at the time). Ran the apply command anyway, as instructed, to get a fresh, explicit confirmation rather than relying on that earlier state:

```
$ dotnet ef database update --project BE/TaskFlow.DataAccess --startup-project BE/TaskFlow.Api --context QuartzContext \
    --connection "server=localhost;user id=root;password=Password@1234;database=quartz_test"

Build started...
Build succeeded.
Done.
```

`Done.` with no intermediate `Applying migration...` lines confirms this was a no-op against already-applied migrations — i.e. `quartz_test` was already at `AlignQuartzSchemaWith3_22` and the command found nothing pending to do. This is the expected, safe idempotent behavior of `dotnet ef database update`.

**Confirmed the five columns exist**, queried directly via `information_schema.COLUMNS` against `quartz_test` (not inferred from the migration list):

```sql
SELECT TABLE_NAME, COLUMN_NAME FROM information_schema.COLUMNS
WHERE TABLE_SCHEMA = DATABASE()
  AND ((TABLE_NAME = 'QRTZ_TRIGGERS' AND COLUMN_NAME IN
        ('MISFIRE_ORIG_FIRE_TIME','EXECUTION_GROUP','PREFERRED_NODE','PREFERRED_NODE_AUTO'))
    OR (TABLE_NAME = 'QRTZ_FIRED_TRIGGERS' AND COLUMN_NAME = 'EXECUTION_GROUP'));
```

Result (captured by the harness below, 5 rows — all expected columns present):

```
QRTZ_FIRED_TRIGGERS.EXECUTION_GROUP
QRTZ_TRIGGERS.EXECUTION_GROUP
QRTZ_TRIGGERS.MISFIRE_ORIG_FIRE_TIME
QRTZ_TRIGGERS.PREFERRED_NODE
QRTZ_TRIGGERS.PREFERRED_NODE_AUTO
```

## 2. Real clustered scheduler round-trip: schedule → persist → fire → job runs

A throwaway xUnit harness (`BE/TaskFlow.Tests/Scratch/QuartzTestProofHarness.cs`) was written, run once, and **deleted immediately after** — it is not part of any commit (confirmed clean `git status` before and after). It used the exact production code path `OverdueScanHostedService` calls (`TaskFlow.BusinessLogic.Schedulers.ScheduleService.ScheduleJob<T>(id, cronExpression, timeZone:)`), pointed at a scheduler instance (`"ProofClusteredScheduler2"`) whose job store (`quartz.jobStore.clustered = true`, `JobStoreTX`/`StdAdoDelegate`, MySQL ADO provider) was configured against `quartz_test`'s connection string instead of the real `quartz` one — same shape of configuration as `ScheduleInjection.ConfigureSchedulerFactory`, different database only.

A trivial stand-in job (`ProofJob`, sets a `ManualResetEventSlim` on `Execute`) was scheduled with a one-shot, year-pinned cron expression a few seconds in the future (UTC), via the real `ScheduleJob` call.

**Run output (captured directly from the test run):**

```
COLUMNS FOUND: QRTZ_FIRED_TRIGGERS.EXECUTION_GROUP, QRTZ_TRIGGERS.EXECUTION_GROUP,
               QRTZ_TRIGGERS.MISFIRE_ORIG_FIRE_TIME, QRTZ_TRIGGERS.PREFERRED_NODE,
               QRTZ_TRIGGERS.PREFERRED_NODE_AUTO
info: TaskFlow.BusinessLogic.Schedulers.ScheduleService[0]
      Scheduling job proof-job with cron expression (16 5 13 8 11 ? 2026): ProofJob
info: TaskFlow.BusinessLogic.Schedulers.ScheduleService[0]
      Scheduling complete for: proof-job
SCHEDULED: True, cron='16 5 13 8 11 ? 2026', nextFireUtc expected ~2026-11-08T13:05:16.4978979+00:00
READ BACK (before fire): CRON_EXPRESSION='16 5 13 8 11 ? 2026', TIME_ZONE_ID='UTC'
FIRED: True

Passed TaskFlow.Tests.Scratch.QuartzTestProofHarness.Proof_SchemaColumns_AndClusteredSchedulerFire [4 s]
Total tests: 1
     Passed: 1
```

**What each line proves:**
- `SCHEDULED: True` — the real `ScheduleService.ScheduleJob` call succeeded against the clustered ADO job store backed by `quartz_test` with its new (post-migration) schema. If the five columns were missing, this would have failed with a SQL error on the trigger insert (the exact ROADMAP R11 risk this migration fixes).
- `READ BACK (before fire): CRON_EXPRESSION='16 5 13 8 11 ? 2026', TIME_ZONE_ID='UTC'` — read directly from `quartz_test.QRTZ_CRON_TRIGGERS` via a separate connection, independent of the scheduler's own in-memory state, and matches exactly what was scheduled (cron expression + time zone persisted correctly). Read before the fire deliberately: a one-shot, year-pinned cron trigger has no further fire time once it completes, so Quartz purges it from the store shortly after firing (observed directly: the same query run *after* the fire and a scheduler shutdown returned no row) — reading it back before the fire avoids that cleanup race while still proving real persistence through the real `ScheduleJob` call, not a mock.
- `FIRED: True` — `ProofJob.Execute` actually ran and signalled completion, confirming the trigger fired for real under the clustered job store and `[DisallowConcurrentExecution]`-compatible setup, the same mechanism `OverdueScanJob` will use in production.

## Conclusion

Both of the user's conditions are satisfied, independently of each other:
1. `AlignQuartzSchemaWith3_22` is additive-safe and functionally correct — confirmed by directly querying the resulting columns on a real, migrated MySQL database (`quartz_test`), not just by reading the migration source.
2. The real clustered Quartz scheduler — same production code path, same job-store configuration shape as `ScheduleInjection` — successfully schedules against that migrated schema, persists the trigger correctly, and fires the job. This retires the one residual "unverified live-runtime claim" flagged at Gate B/C.

`quartz_test` is left in place, unmodified beyond what the migration itself added (the harness made no other writes to it). `quartz` and `mydb` were never touched by any command in this session. The API was never started.
