# F03 — Priority-Based Auto-Sorting

| | |
|---|---|
| **Source** | `docs/brs/taskflow-brs.md` § F03 |
| **Status** | Planned — all open questions resolved 2026-10-20 |
| **Depends on** | [F01](f01-workflow.md) — task model, `CompletedAt`, permissions |

**Goal** — every board column surfaces the most important and most time-sensitive work first, with no manual sorting by the user.

## Scope

**In scope** — the order of tasks the API returns: priority first, then due date, with undated tasks last inside their priority group; the `Done` column's separate rule; per-column pagination.

**Out of scope** — client-chosen sort columns, manual drag-to-reorder within a column ([F01](f01-workflow.md) D9), saved views, per-user preferences, any visual treatment.

## Decisions

- **D1** the API returns the board **grouped by status**, one page per column. There is no client-supplied sort parameter in the MVP — which also keeps the board away from `QueryHelper.ApplySorting`'s unknown-column exception.
- **D2** priority rank is `Urgent` (highest) → `High` → `Normal`, stored so the rank is explicit rather than inferred from the enum's alphabetical order.
- **D3** within one priority and one due date, the tie is broken by `CreatedAt` ascending, then `Id` ascending — required for stable pagination.
- **D4** **the `Done` column is an explicit exception**: it is ordered by `CompletedAt` descending (most recently completed first) and limited to the 20 newest. Priority and due date play no part there.
- **D5** every other column returns 50 tasks per page, with further pages fetched as the user scrolls.
- **D6** ordering applies to whatever set the caller asked for; filtering never changes the order rule.

## Ordering rule

For `Backlog`, `Todo`, `InProgress` and `Review`:

1. `Urgent`, then `High`, then `Normal`.
2. Inside a priority group: earlier `DueDate` first.
3. Inside a priority group: tasks with no `DueDate` come after all tasks that have one.
4. Inside the same priority and the same due date (or both undated): `CreatedAt` ascending, then `Id` ascending.

For `Done`: `CompletedAt` descending, 20 rows (D4).

## Acceptance criteria

**From the BRS scenarios**

- **AC1** Given two `Urgent` tasks in a column, one due tomorrow and one due next week, when the column is returned, then the one due tomorrow is first.
- **AC2** Given one `High` task with no due date and one `Normal` task due today in the same column, when it is returned, then the `High` task is first — priority outranks due date.
- **AC3** Given a `Normal` task below several others, when its priority is changed to `Urgent`, then the very next read returns it at the top of its column, with no other action by any user.

**Ordering detail**

- **AC4** Given two tasks of the same priority, one with a due date and one without, when the column is returned, then the dated task comes first.
- **AC5** Given tasks across all three priorities with mixed due dates, when a column is returned, then every `Urgent` precedes every `High`, and every `High` precedes every `Normal`, regardless of due dates.
- **AC6** Given two tasks with the same priority and the same due date, when the column is requested twice, then both responses place them in the same order (D3).
- **AC7** Given tasks inserted in an order unrelated to priority, when the column is returned, then the order reflects the rule and not the insertion or id sequence.

**The `Done` column (D4)**

- **AC8** Given several completed tasks, when the `Done` column is returned, then they are ordered by `CompletedAt` descending — the most recently approved first — irrespective of their priority.
- **AC9** Given 25 completed tasks, when the `Done` column is returned, then exactly the 20 most recently completed are present.
- **AC10** Given an `Urgent` task completed last week and a `Normal` task completed today, when the `Done` column is returned, then the `Normal` one is first (D4 overrides the priority rule).
- **AC11** Given a task is reopened and later approved again, when the `Done` column is returned, then it appears at its new `CompletedAt` position, not its original one.

**Pagination and the board shape**

- **AC12** Given a board request, when the response is inspected, then tasks are grouped per status with each column paginated independently (D1).
- **AC13** Given a column holds more tasks than one page, when page 1 is requested, then it contains that column's highest-ranked tasks — ordering is applied before pagination, never after.
- **AC14** Given a caller pages through a column without any data changing, when the pages are concatenated, then no task is missing and none appears twice (D3 makes this hold).
- **AC15** Given a column with 50 or more tasks, when the next page is requested as the user scrolls, then it continues from where the previous page ended with no overlap (D5).

**Missing cases the BRS does not cover**

- **AC16** Given a column contains no tasks (the empty case), when the board is returned, then that column is present and empty with zero totals and a success response — not an error, not a null, and not an omitted column.
- **AC17** Given soft-deleted tasks exist, when a column is returned, then they are absent and occupy no position.
- **AC18** Given a caller without `ViewTasks`, when they request the board, then the response is forbidden and no task data is returned.
- **AC19** Given any authenticated caller with `ViewTasks`, when they request the board, then they see every task on it — there is no team filtering and no per-user visibility rule ([F01](f01-workflow.md) D8).
- **AC20** Given a task is moved between columns by drag-and-drop, when the board is read again, then the task sits in its new column at the position the ordering rule gives it — not where it was dropped ([F01](f01-workflow.md) D9).

## Technical notes

- The ordering must be expressed in SQL, not applied in memory after materialising rows, or AC13 cannot hold.
- "Undated last" needs an explicit expression: MySQL sorts `NULL` **first** on ascending order, so a plain `ORDER BY DueDate` puts undated tasks at the top — the opposite of rule 3. Order by "has no due date" first, then by the date.
- `DueDate` is a `DateOnly`, so the comparison is a plain date comparison with no timezone arithmetic; only [F05](f05-overdue.md)'s "is it past?" question needs the configured zone.
- Composite indexes covering the sort keys per column: `(Status, Priority, DueDate)` for the active columns and `(Status, CompletedAt)` for `Done`.
- The scaffold's `Pagination<T>` executes its query in the constructor; the ordering must be on the `IQueryable` *before* it is handed over. Five columns means five paged queries — keep them to one round trip each, and do not fetch a column the caller did not ask for.
- The `Done` column reads `CompletedAt`, written only by [F01](f01-workflow.md). Never fall back to `UpdatedAt`: any edit to a completed task would reshuffle the column.
