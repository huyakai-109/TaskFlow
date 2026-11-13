# F10 — Board sort and read-model additions (closes COMING-SOON B2–B6, SPEC §10.9)
Status: Proposed 2026-11-13 · No migration

## 1. Board sort
`sort` query param `[EnumDataType(typeof(TaskSortOrder))]` on `GET /tasks/board` and `GET /tasks`: `priority` (default; F03 rule) · `dueDate` (`DueDate IS NULL` asc, `DueDate` asc, `Priority` asc, `CreatedAt` asc, `Id` asc) · `newest` (`CreatedAt` desc, `Id` desc). `Done` ignores it (F03 D4). `BoardRes` + `sort` (echo). The client sends the same `sort` to `GET /tasks` for page 2+ and keys the query by it; the choice lives in the URL (`?sort=`), never stored server-side. One switch in `TaskService` (extends `ApplyColumnOrder`), never `QueryHelper.ApplySorting`. Rejected: `updatedAt` (changes on every edit → unstable paging; F03 forbids `UpdatedAt`), assignee name (join + display-name semantics; My tasks/Team capacity already cover it), title (low value), arbitrary columns (F03 D1). In-memory tie-break at board scale is accepted (F03 precedent); no new index. AC: each order is stable across pages; an unknown value → validation error; `Done`'s order is identical for every `sort` value.

## 2. `assigneeId` filter on `GET /tasks`
`assigneeId` long? ≥1, composes with `status`/`isOverdue`/`sort`, uses `IX_Tasks_IsDeleted_AssigneeId_Status`. My tasks switches from board-cache filtering to this (removes SPEC §10.9's caveat).

## 3. `lastTransition` on `TaskSummaryRes`/`TaskDetailRes` (additive)
`{ from, to, actor: { id, userName, displayName } | null, at } | null` — the latest `StatusChanged`/`Reopened` entry whose `ToValue` equals the task's current status; `null` when none (a task that never moved). One batched query per page (no N+1). Client meanings: `Done` → "Approved by"; `InProgress` with `from=Review` → "Sent back by"; `Review` → "Submitted by". A historical fact, not a reviewer role — COMING-SOON C1 stands.

## 4. `GET /workflow` (ViewTasks)
`ResultRes<WorkflowRes { maxInProgressPerAssignee, timeZone, today: "yyyy-MM-dd", overdueScan: { lastFiredAt, nextFireAt } }>`. Scan times come from the Quartz trigger (previous/next fire, via a new read method on `IScheduleService`); `null` when not scheduled (Testing). "Last fired" is not proof of success — UI copy stays "Last check". Error `Workflow.Get.Exception`. Replaces the client literal `IN_PROGRESS_LIMIT`; `today` replaces browser-date approximations (F05 T9).

## 5. `GET /users/workload` (ViewTasks)
`ResultRes<List<{ userId, todo, inProgress, review }>>` — one `GROUP BY` over non-deleted tasks with an assignee. Replaces client-side derivation from the board's page 1 (removes the COMING-SOON A2/A8/A9 "may be incomplete" caveats). Error `User.Workload.Exception`.

## 6. `GET /me/summary` (ViewTasks)
`ResultRes<{ inProgress, completedThisWeek, weekStart, weekEnd }>` — completed = Status `Done`, `AssigneeId` = caller, `CompletedAt` in `[Monday 00:00, next Monday 00:00)` in the configured zone. Correct because assignment is locked in Review/Done (F01 D12). Error `Profile.Summary.Exception`.