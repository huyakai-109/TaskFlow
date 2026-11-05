# 0003 — F05 overdue scan: per-task commits, idempotent by predicate; Quartz schema tracked by additive migrations

Status: **Accepted** (design by `architect`, 2026-11-05; approved by the user 2026-11-05)

## Context

F05 needs a daily 00:05 Asia/Ho_Chi_Minh scan that flags every past-due, not-`Done` task exactly once, with one `System` history entry per task, isolated failures (D3), safety against concurrent user writes (AC21) and a single execution across a cluster (AC24). The scaffold's Quartz store is clustered ADO on MySQL, but its committed `QuartzContext` schema is the 3.13 shape while the package is 3.22.7 (ROADMAP R11), and `IScheduleService`'s cron overload ignores time zones and deletes and recreates its job on every start.

## Decision

- The scan is a scoped service given "today"; it reads candidate ids in keyset batches and commits **one task at a time** (task + `OverdueMarked` in one `SaveChanges()`), re-checking `TaskOverdueRule` and writing under `TaskItem.Version`. Conflicts and deadlocks are skipped; other failures are logged and skipped.
- Idempotency is the predicate (`IsOverdue = false`) + the row token + clustered Quartz with `[DisallowConcurrentExecution]` — no run-ledger table, no lock row.
- The cron trigger is built in the configured zone and is created only when missing or changed (`replace: true`), never deleted on start, so a missed run fires once on recovery.
- The Quartz schema follows the package version through **hand-written, additive `QuartzContext` migrations**; every Quartz upgrade diffs the official MySQL script.

## Consequences

- One load + one commit per flagged task — fine at one-board volume.
- A card left open across 00:05 for a newly flagged task gets one `Conflict`.
- A node dying mid-scan leaves the remainder for the next run (≤ 24 h).
- Quartz DDL is not transactional; a half-applied migration needs a manual fix.
- Scheduling fails loudly (logged `Error`) but does not stop the API if the Quartz schema is behind.

## Alternatives rejected

- **Bulk `UPDATE … WHERE` + `INSERT … SELECT`** — bypasses `UnitOfWork` stamping and the token, raw SQL, all-or-nothing (breaks D3/AC20).
- **Run-ledger table keyed by date** — extra schema for what the predicate already guarantees.
- **Drop and recreate the Quartz tables from the 3.22 script** — Destructive class for no gain (tables are empty, but the precedent is wrong).
- **`RAMJobStore`** — loses clustering (AC24) and persistence.
- **Keep delete-and-recreate on start** — races between nodes and discards missed fires.
