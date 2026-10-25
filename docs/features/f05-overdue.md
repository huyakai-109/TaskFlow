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
