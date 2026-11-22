# F09 — Reports (per-member performance, replaces Activity)

| | |
|---|---|
| **Source** | User decision 2026-11-21 (Activity replacement, performance appraisal). Not a BRS section; reads F01/F02/F04/F05 data only. |
| **Status** | **Proposed** 2026-11-21 — technical contract by architect; ADR [0008](../adr/0008-reports-replace-activity.md). Awaiting the user's sign-off on the open questions at the end. |
| **Overrides** | `CLAUDE.md` *Out of scope* "dashboards, exports" — lifted for this page and its CSV only. [f02-capacity.md](f02-capacity.md) *Out of scope* "reporting on team load" — lifted (read-only reporting; no warning-before-limit). [f04-activity-feed.md](f04-activity-feed.md) — **superseded and removed**. [f04-audit.md](f04-audit.md) R2's lifting of "a cross-task activity feed" is withdrawn with it. |
| **Depends on** | F01 transitions and the R2 holder-only rule plus Review/Done assignment lock · F02 limit key · F04 trail (8 events) · F05 `OverdueMarked` · F08 `DisplayName`/avatar/`DeactivatedAt` · F10 `GET /workflow` (`today`, `timeZone`, limit) |

## Goal

A Manager or Admin can see how each team member and the whole team delivered over any day, week, month, quarter or custom range — throughput, speed, reliability, quality and load side by side, with the correlations between them — and any member can see the same for themselves, without a composite score or a ranking.

## Scope

In: scorecard + 5 charts (on-time vs late per bucket; cycle-time trend median + p85; member × week workload heatmap; workload vs rework scatter; member vs team median), drill-down to the underlying tasks, CSV export, fairness caveats, insufficient-data states; removal of Activity (API + client).
Out: any composite score, ranking, sorting members by a metric, targets/thresholds per person, story points/estimates, comments or notes on a report, scheduled/emailed reports, PDF, saved views, real-time refresh, recording any new event, business-hours/holiday calendars, per-team scoping (one shared board).

## Rules

**R1 Scope and permission.** `ViewReports` (Manager, Admin): team scope and any single user. Without it (Member): only the caller's own `userId`. Anything else → `Report.<Op>.SelfOnly` (400), checked before user existence so it reveals nothing. Team endpoint: policy `ViewReports` (403). Export: policy `ExportReports` (403).

**R2 Holder.** `holder(task, e)` = the task's assignee immediately *before* entry `e` in `(CreatedAt, Id)` order: the `FromValue` of the first `AssigneeChanged` at or after `e`, else the task's current `AssigneeId`. `null` = unassigned. Every metric below is attributed to `holder` at its defining entry, **never** to `ActorUserId`.

**R3 Counted completion.** A `StatusChanged → Done` entry counts if no `Reopened` entry follows it on the same task. Each task counts at most once, at its last such `Done`. Credited to `holder` at that entry. **Known property, not a defect:** because only the *last* `Done` counts, reopening a previously-finished task removes its earlier completion from every bucket that had counted it — a period's figures can therefore change after the fact, including periods already shown to someone. This is intentional (R1 of ADR 0008's Decision §2) and is stated on the page itself (SPEC §11.5 "Reopened work") so nobody reads a dated screenshot as final.

**R4 Delivery.** `done` = counted completions in the bucket. `byPriority` uses the priority in effect at that entry (rebuilt backward from `PriorityChanged`, like R2).

**R5 Speed.** Cycle time = counted `Done` minus the task's **first** `→InProgress` entry. Lead time = counted `Done` minus the `Created` entry. Both in whole seconds (truncated). Bucketed by the completion. Tasks with no `→InProgress` have no cycle time (still have a lead time). Median: conventional (for even n, the mean of the two middle values, truncated to whole seconds). p85: nearest rank, value at rank `ceil(0.85·n)` in ascending order. **Never a mean.**

**R5b Review wait (addresses gap G3).** For the counted completion, let *e* be the `StatusChanged InProgress→Review` entry immediately preceding it (always exists — F01's state machine cannot reach Done except through Review). `reviewWaitSeconds = completionTime − e.CreatedAt`. This isolates the time the final review pass took from the holder's own cycle time, so a slow reviewer is no longer silently blamed on the holder. Reported as a `Distribution` (median + p85, same rules as R5) alongside `cycleTime`/`leadTime` under `speed`, credited to `holder` at *e* (same person as the completion credit, by construction). Not charted — scorecard only, so it adds no new chart type to the locked chart list (ADR 0008 Decision §6/Consequences); its caveat text sits directly under the cycle-time chart instead (SPEC §11.5).

**R6 Reliability.** For each counted completion: `dueDate` in effect at that entry (rebuilt backward from `DueDateChanged`). No due date → `noDueDate`. Otherwise `onTime` if the completion's local date (zone) ≤ due date, else `late`. `onTimeRate = onTime / (onTime + late)`. `overdueMarked` = `OverdueMarked` entries (any actor, including the reopen case) credited to `holder`. Unassigned at the mark → team-level `unassignedOverdueMarked` only.

**R7 Quality.** A *submission* is a `StatusChanged InProgress→Review` entry **only** — a `Reopened` entry is never a submission, so it cannot also feed `reworkRate`'s `sentBack`; it is counted exactly once, in `reopenedAfterDone`, never twice. A submission's *outcome* is the next entry that leaves Review: `→InProgress` = sent back, `→Done` = accepted, none yet = pending (excluded). A `Reopened`→`Review→InProgress` round trip that happens between two submissions (e.g. a reopen-then-sent-back-again cycle with no intervening `InProgress→Review`) has no submission to attach to and is ignored by `reworkRate` — it is still captured by `reopenedAfterDone`. `reworkRate = sentBack / (sentBack + accepted)` over submissions in the bucket (bucketed by submission time, credited to `holder` at the submission). `reopenedAfterDone` = `Reopened` entries credited to `holder`.

**R8 Load.** `avgInProgress` = time-weighted mean number of tasks the user holds in `InProgress` over the bucket's measured window `[max(bucketStart, dataAvailableSince), min(bucketEnd, now))`. A task is InProgress from a `→InProgress` entry until the next `StatusChanged` out of InProgress or `Deleted`. Holder changes inside the interval split it. `capacityUtilization = avgInProgress / Workflow:MaxInProgressPerAssignee` (current value — gap G1). Team value = mean of the listed members' values.

**R9 Take-overs.** `AssigneeChanged` where `ActorUserId = ToValue`, `FromValue` not null and `≠ ToValue`. Gives `takeOvers` to `ToValue` and `takenFrom` to `FromValue`, bucketed by the entry.

**R10 Deleted tasks.** Entries of soft-deleted tasks that were recorded before their `Deleted` entry count (the trail survives, F04 D6). An InProgress interval ends at `Deleted`. Drill-down shows them as deleted and not linked. (Open question Q2.)

**R11 Users.** Deactivated users keep their credit, appear in team views with `isActive:false`, and can be selected. Soft-deleted users count in team totals and are resolved by name with `IgnoreQueryFilters()` (Activity R6 precedent), but `userId` = a soft-deleted or non-existent user → `UserNotFound`. Team member list = every active user plus any inactive or deleted user with activity in range, ordered by `displayName ?? userName` (case-insensitive), then `id` — **never by a metric**. No endpoint accepts a sort parameter.

**R12 Team median and contributors.** For the comparison: the median (conventional) of per-member values, over members whose value is non-null. Team totals (team scope) are pooled over all tasks, not medians of members. `contributors` (on both `TeamMedian` and the team scorecard's `team` object) = the count of roster members (R11) with **at least one counted completion (R3) in the range** — `done ≥ 1` — never the full roster size. A member with zero completions in range (including one with other activity, e.g. only a take-over or only time spent InProgress) does not count toward `contributors`. Pooled `takeOvers`/`takenFrom` at team scope are summed across the whole roster regardless of `contributors` (a take-over needs no completion to have happened).

**R13 Time buckets.** Zone from `Workflow:TimeZone`. `from`/`to` are inclusive local dates. Buckets are aligned to calendar boundaries: day; week Monday–Sunday (B7); month; quarter (Jan/Apr/Jul/Oct). The first and last bucket are clipped to `[from, to]` and flagged `isPartial`. A bucket whose measured window is shortened by `dataAvailableSince` or `now` is also `isPartial`. An event belongs to the bucket containing its local date. No DST: Asia/Ho_Chi_Minh is UTC+7 with no DST since 1975. Conversions still go through `DateTimeHelper.ToDateInZone`/`LocalStartOfDayToUtc` — never a literal offset.

**R14 Limits.** `from ≤ to`; `to ≤ today` (zone); `to − from + 1 ≤ 366` days; granularity `Day` only when the range is ≤ 92 days. The team endpoint is always weekly (≤ 53 weeks).

**R15 Data available since.** `dataAvailableSince` = local date of the earliest `TaskHistories.CreatedAt`, or `null` when the table is empty. Buckets entirely before it: `hasData:false`, all metrics `null`.

**R16 Small samples.** `ReportRules.SmallSampleThreshold = 5` (constant in `TaskFlow.Common`, echoed in every response). A rate or distribution with `0 < n < 5` → `lowSample: true`. `n = 0` → value `null`. `p85` is `null` whenever `n < 5`. Counts are never suppressed.

**R17 Read-only and logging.** No endpoint writes anything. Report payloads (metric values, task lists, CSV rows) are never logged. Every call logs the caller id, scope, range, granularity and elapsed ms (`Warning` if > 2 s). `reports/export` additionally logs the subject (`userId`, or `"team"`), `dataset` and the resulting row count — never the exported content.

## Gaps — what cannot be computed accurately today

| # | Metric | Gap | Treatment |
|---|---|---|---|
| G1 | Capacity utilisation | The limit is configuration only; its past values are not stored | Divide by the current value. Caveat shown. Exact only if the limit never changed in the range |
| G2 | Throughput / speed fairness | No task size, points or estimate | Caveat "indicator, not verdict". Not fixable without a new field (out of scope) |
| G3 | Cycle / lead time | No working calendar, pause or blocked state; time waiting in Review for a reviewer used to count against the holder's cycle time | **Addressed:** a separate `reviewWait` metric (R5b) isolates the final review pass from cycle time. The working-calendar/pause gap remains — cycle, lead and review-wait are all wall-clock; caveat stays |
| G4 | Per-member averages around joining/leaving | `DeactivatedAt` is current state only (no activation history); joining = `Users.CreatedAt` | Averages use the full bucket; caveat "joined or left mid-period" |
| G5 | Attribution in data written before the R2 holder-only rule and Review/Done lock | Reassignment during Review was then possible; the cut-over date is not recorded | Moot if no deployment predates R2 (Deployment is TBD). Q13 |
| G6 | Time spent overdue | No overdue-cleared event (F05 R2: never built) | Only the mark count is reported, as decided |

All other scorecard metrics are exact from the stored trail (R2 reconstruction covers initial assignee, priority and due date at any moment).

## Acceptance criteria

Fixture **F** (local times, zone +07; seeded directly with explicit `CreatedAt`, ids ascending in listed order). Users m1, m2 (Member), mgr (Manager). Limit 3.
- T1 created 03-02 09:00, assignee m1, due 03-04. →InProgress 03-02 10:00 · →Review 03-03 10:00 · →Done 03-04 10:00 (actor mgr).
- T2 created 03-02 08:00, m1, due 03-04. →InProgress 03-02 12:00 · →Review 03-03 12:00 · Review→InProgress 03-03 15:00 (actor m2) · →Review 03-04 12:00 · →Done 03-05 09:00 (mgr).
- T3 created 03-01 09:00, m1, no due. →InProgress 03-02 09:00 · AssigneeChanged m1→m2 actor m2 03-03 09:00 · →Review 03-04 09:00 · →Done 03-04 18:00 (mgr).
- T6 created 03-03 08:00, m1, no due. →InProgress 03-03 11:00 · →Review 03-04 09:00 · →Done 03-05 09:00 · Reopened 03-06 09:00 (mgr) · Review→InProgress 03-06 10:00 (mgr) · →Review 03-07 09:00 · →Done 03-07 15:00 (mgr).
- T7 m1, due 03-03, Todo; OverdueMarked (actor null) 03-04 00:05. T8 unassigned, due 03-03; OverdueMarked 03-04 00:05.
- T4 m2 Done 03-08 23:30; T5 m2 Done 03-09 00:10 (both with full trails).

AC1 (delivery, credit) Scorecard m1, 2026-03-02..03-08, Week → `done = 3` (T1, T2, T6). m2 → `done = 2` (T3, T4). mgr → `done = 0` (Done by a Manager credits the holder, not the actor).
AC2 (take-over) m2 `takeOvers = 1`, m1 `takenFrom = 1`. T3 is credited to m2.
AC3 (reopen) T6 counted once, on 03-07. m1 `reopenedAfterDone = 1`. Cycle T6 = 03-03 11:00 → 03-07 15:00 = 100 h = 360 000 s.
AC4 (speed) m1 cycle times {48 h, 69 h, 100 h} → `medianSeconds = 248 400` (69 h), `p85Seconds = null`, `sampleSize = 3`, `lowSample = true`. T1 lead = 49 h. T2 lead = 73 h. m1 review-wait (R5b: Done minus the immediately preceding `InProgress→Review` entry) — T1: `→Review` 03-03 10:00 → `Done` 03-04 10:00 = 24 h; T2: `→Review` 03-04 12:00 (the 2nd entry) → `Done` 03-05 09:00 = 21 h; T6: `→Review` 03-07 09:00 (the entry after the reopen cycle) → `Done` 03-07 15:00 = 6 h → `{21600, 75600, 86400}` → `medianSeconds = 75 600` (21 h), `p85Seconds = null`, `sampleSize = 3`, `lowSample = true`.
AC5 (reliability) m1 `onTime 1` (T1, done 03-04 ≤ due 03-04), `late 1` (T2), `noDueDate 1` (T6). `onTimeRate.value = 0.5`, `denominator 2`, `lowSample true`.
AC6 (overdue) m1 `overdueMarked = 1` (T7). Team `unassignedOverdueMarked = 1` (T8), not credited to anyone.
AC7 (rework, corrected 2026-11-21 — a `Reopened` entry is never a submission, R7) m1 submissions (only `InProgress→Review` entries): T1 `→Review` 03-03 10:00 → `Done` = accepted. T2 `→Review` 03-03 12:00 → `Review→InProgress` 03-03 15:00 = sent back; T2 `→Review` 03-04 12:00 → `Done` = accepted. T6 `→Review` 03-04 09:00 → `Done` 03-05 09:00 = accepted (its `Reopened`/`Review→InProgress` round trip at 03-06 is **not** a submission or an outcome of one — it only increments `reopenedAfterDone`); T6 `→Review` 03-07 09:00 → `Done` 03-07 15:00 = accepted. → `sentBack 1`, `submissionsDecided 5`, `reworkRate 0.2`, `lowSample false` (n = 5, not `< 5`). `reopenedAfterDone` for m1 is unchanged at `1` (T6's `Reopened` entry, R3's known-property caveat applies: this bucket's `done` and the rework figures above already reflect T6 counting once, at 03-07).
AC8 (load) Day bucket 2026-03-02, m1: T1 14 h + T2 12 h + T3 15 h = 41 h / 24 h → `avgInProgress 1.71`, `capacityUtilization 0.5694`.
AC9 (bucket edge) m2, 03-02..03-15, Week: T4 (03-08 23:30 local = 16:30Z) is in week 03-02. T5 (03-09 00:10 local = 03-08 17:10Z) is in week 03-09.
AC10 (partial) Range 03-04..03-10, Week → 2 buckets `[03-04..03-08, isPartial]`, `[03-09..03-10, isPartial]`.
AC11 (since) Empty `TaskHistories` → `dataAvailableSince null`, every bucket `hasData false`, metrics null, success. A range starting before the first entry → earlier buckets `hasData false`, the bucket containing it `isPartial`.
AC12 (scope) Member: no `userId` → `Report.GetScorecard.SelfOnly`. Another user's id (existing or not) → `SelfOnly`. Own id → 200. Member `GET /reports/team` → 403. Member export → 403. Anonymous → 401 on all. Manager and Admin → 200 for team and any user.
AC13 (validation) `from > to` → `InvalidRange`. 367 days → `RangeTooLarge`. Day over 93 days → `TooManyBuckets`. `to` after today → `RangeInFuture`. Unknown granularity/metric or malformed date → `Request.Validation.Failed`. `length` outside 1..100 → `Request.Validation.Failed`.
AC14 (users) Deactivated m2 still appears in the team list with `isActive:false` and credit unchanged. A soft-deleted user id → `UserNotFound`.
AC15 (deleted task) T1 soft-deleted after Done → still counted. Drill-down item `isDeleted:true`.
AC16 (no ranking) The team `members` order equals the display-name order whatever the metric values are.
AC17 (read-only) After any report call, row counts and the max `UpdatedAt` of `Tasks`, `TaskHistories` and `TaskNotifications` are unchanged.
AC18 (drill-down) `GET /reports/tasks?userId=m1&from=03-02&to=03-08&metric=Late` → exactly T2. `metric=TakeOver&userId=m2` → T3.
AC19 (export) Manager: CSV with the header row, a UTF-8 BOM, `Content-Disposition: attachment`, `Cache-Control: no-store`. A cell starting with `= + - @` or a tab is prefixed with `'`. Over 10 000 rows → `Report.Export.TooManyRows` (JSON envelope, 400).
AC20 (permissions seed) `ViewReports`/`ExportReports` seeded for Admin (16, 17) and Manager (1015, 1016), not for Member. Present in login and refresh `permissions[]`.
AC21 (Activity removed) `GET api/v1/activity` → 404. `GET api/v1/tasks/{id}/history` unchanged (F04 AC15–AC21 tests still pass).
AC22 (contributors, corrected 2026-11-21 — R12: contributors counts members with `done ≥ 1`, never roster size) Team scope, 2026-03-02..03-08, Week: `members` (the full R11 roster — every active user, so {m1, m2, mgr}) has 3 rows, including mgr with all-zero/null totals (mgr is never a holder in this fixture, so `done = 0`). `team.contributors` and `teamMedian.contributors` are **not** the roster size (3); they count only members with `done ≥ 1` in range → `contributors = 2` (m1 done=3, m2 done=2; mgr excluded). `team.takeOvers = 1`, `team.takenFrom = 1` (T3, m1→m2, R9 — pooled across the full roster regardless of `contributors`, since a take-over needs no completion).

Client ACs: SPEC §11.

## Technical contract (architect, 2026-11-21)

Controller `ReportsController : BaseController`, route `api/v{version:apiVersion}/reports`. Service `IReportService`/`ReportService` (scoped, registered in `CoreDependencyConfiguration.AddServices`). Pure `ReportCalculator` in `BE/TaskFlow.BusinessLogic/Reports/`. Constants `BE/TaskFlow.Common/Constants/ReportRules.cs`. Enums `ReportGranularity { Day=1, Week=2, Month=3, Quarter=4 }`, `ReportTaskMetric { Done=1, OnTime=2, Late=3, NoDueDate=4, SentBack=5, Reopened=6, OverdueMarked=7, TakeOver=8 }`, `ReportExportDataset { Buckets=1, Members=2, Tasks=3 }` in `TaskFlow.Common/Enums`. The controller reads `GetUserId()` and `GetUserRoleClaims()` and passes them inward (TasksController precedent). Every action uses the house `try`/`catch` → `<Op>.Exception` → `InternalServerError`.

### Endpoints

| # | Verb route | Policy | Request (`[FromQuery]`) | Success |
|---|---|---|---|---|
| 1 | GET `reports/scorecard` | `ViewTasks` (+ R1 in service) | `userId` long? `[Range(1,long.MaxValue)]` (absent = team) · `from`,`to` `DateOnly` `[Required]` · `granularity` `[EnumDataType]` default `Week` | `ResultRes<ReportScorecardRes>` |
| 2 | GET `reports/team` | `ViewReports` | `from`,`to` `[Required]` | `ResultRes<ReportTeamRes>` |
| 3 | GET `reports/tasks` | `ViewTasks` (+ R1) | `userId?` · `from`,`to` · `metric` `[Required][EnumDataType]` · `start` `[Range(1,int.MaxValue)]` default 1 · `length` `[Range(1,100)]` default 30 | `PaginationResultRes<List<ReportTaskRes>>`, ordered `occurredAt desc, taskId desc` |
| 4 | GET `reports/export` | `ExportReports` | `userId?` · `from`,`to` · `granularity` · `dataset` `[Required][EnumDataType]` (`Members` requires team scope → `Report.Export.InvalidDataset`) | `text/csv; charset=utf-8` `FileContentResult`; failures as the JSON envelope |

Validation order (service): range (`InvalidRange` → `RangeInFuture` → `RangeTooLarge` → `TooManyBuckets`) → scope (`SelfOnly`) → user exists (`UserNotFound`) → compute.

### Shapes

```jsonc
// ReportScorecardRes
{ "meta": { "scope": "Member", "userId": 12, "from": "2026-03-02", "to": "2026-03-08",
            "granularity": "Week", "timeZone": "Asia/Ho_Chi_Minh", "weekStartsOn": "Monday",
            "dataAvailableSince": "2026-02-20", "generatedAt": "2026-11-21T03:00:00+00:00",
            "smallSampleThreshold": 5, "inProgressLimit": 3 },
  "subject": { "id": 12, "userName": "member1", "displayName": "Lan", "avatarUrl": "/api/v1/users/12/avatar?v=…", "isActive": true }, // null for team scope
  "totals": MetricSet,
  "teamMedian": TeamMedian | null,   // null for team scope, and for callers without ViewReports (Q3)
  "buckets": [ { "start": "2026-03-02", "end": "2026-03-08", "isPartial": false, "hasData": true, "metrics": MetricSet } ] }

// MetricSet
{ "delivery":    { "done": 3, "byPriority": { "urgent": 0, "high": 1, "normal": 2 } },
  "speed":       { "cycleTime": Distribution, "leadTime": Distribution, "reviewWait": Distribution },
  "reliability": { "onTime": 1, "late": 1, "noDueDate": 1, "onTimeRate": Rate, "overdueMarked": 1 },
  "quality":     { "submissionsDecided": 6, "sentBack": 2, "reworkRate": Rate, "reopenedAfterDone": 1 },
  "load":        { "avgInProgress": 1.71, "capacityUtilization": 0.5694 },   // null when hasData=false
  "attribution": { "takeOvers": 0, "takenFrom": 1 } }
// Distribution { "sampleSize": 3, "medianSeconds": 248400, "p85Seconds": null, "lowSample": true }
// Rate         { "value": 0.5, "denominator": 2, "lowSample": true }   // value 0..1, 4 dp; null when denominator 0
// TeamMedian   { "contributors": 6, "done": 4, "cycleTimeMedianSeconds": …, "leadTimeMedianSeconds": …, "reviewWaitMedianSeconds": …,
//                "onTimeRate": …, "overdueMarked": …, "reworkRate": …, "avgInProgress": …, "capacityUtilization": … }

// ReportTeamRes
{ "meta": { …as above, "granularity": "Week" },
  "team": { "totals": MetricSet, "contributors": 6, "unassignedOverdueMarked": 1 },  // contributors = members with done >= 1 in range (R12), not members.length
  "teamMedian": TeamMedian,
  "members": [ { "user": { "id", "userName", "displayName", "avatarUrl", "isActive" }, "totals": MetricSet } ],  // R11 order
  "heatmap": { "weeks": [ { "start", "end", "isPartial" } ],
               "rows": [ { "userId": 12, "avgInProgress": [1.71, null, …] } ] } }  // same order as members; null = no data

// ReportTaskRes
{ "taskId": 7, "title": "…", "isDeleted": false, "metric": "Late",
  "creditedUser": { "id", "userName", "displayName" } | null,
  "occurredAt": "…+00:00", "occurredAtLocal": "…+07:00",
  "priority": "High", "dueDate": "2026-03-04" | null,
  "cycleTimeSeconds": 248400 | null, "leadTimeSeconds": 262800 | null }
```

Rounding: seconds truncated to whole numbers; rates to 4 dp; `avgInProgress` to 2 dp; utilisation computed from the unrounded average, to 4 dp.

CSV datasets — **Buckets**: `bucket_start,bucket_end,is_partial,done,done_urgent,done_high,done_normal,on_time,late,no_due_date,on_time_rate,overdue_marked,cycle_median_h,cycle_p85_h,lead_median_h,lead_p85_h,review_wait_median_h,review_wait_p85_h,submissions_decided,sent_back,rework_rate,reopened_after_done,avg_in_progress,capacity_utilization,take_overs,taken_from,low_sample`. **Members**: `user_name,display_name,is_active` + the totals columns. **Tasks**: one row per counted completion (`task_id,title,credited_user,completed_local,priority,due_date,on_time,cycle_h,lead_h`). Hours to 1 dp. Filename `taskflow-report-{team|userName}-{from}-{to}-{dataset}.csv` (sanitised to `[A-Za-z0-9._-]`). Cap 10 000 data rows.

### Error codes (`BE/TaskFlow.Common/Constants/Messages/ReportControllerMS.cs`, all 400 except Exception = 500)

`Report.GetScorecard.{InvalidRange, RangeInFuture, RangeTooLarge, TooManyBuckets, SelfOnly, UserNotFound, Exception}` · `Report.GetTeam.{InvalidRange, RangeInFuture, RangeTooLarge, Exception}` · `Report.GetTasks.{InvalidRange, RangeInFuture, RangeTooLarge, SelfOnly, UserNotFound, Exception}` · `Report.Export.{InvalidRange, RangeInFuture, RangeTooLarge, TooManyBuckets, InvalidDataset, UserNotFound, TooManyRows, Exception}`. 401/403 from the framework.

### Queries (no-tracking, no N+1, at most 4 round trips plus chunking)

1. `MIN(CreatedAt)` from `TaskHistories` (uses `IX_TaskHistories_CreatedAt_Id`).
2. Candidate tasks, `IgnoreQueryFilters()`: `CreatedAt < toUtcExclusive && !(Status == Done && CompletedAt < fromUtc) && !(IsDeleted && UpdatedAt < fromUtc)`. Project `Id, Title, Status, Priority, AssigneeId, DueDate, IsDeleted, CreatedAt`. Full scan of `Tasks` accepted at board scale (no speculative index — `database.md` §3). Record `EXPLAIN`.
3. Histories `WHERE TaskId IN (chunk ≤ 500) ORDER BY TaskId, CreatedAt, Id`, all events, narrow projection (uses `IX_TaskHistories_TaskId_CreatedAt_Id`). Record `EXPLAIN`.
4. Users referenced (credited ids + roster): `IgnoreQueryFilters()` for names; roster = `!IsDeleted`.

Reads happen without an explicit transaction. A write landing between queries can skew one figure until the next load — accepted (read-only, not persisted). Budget: < 1 s for a 1-year team report at the assumed scale. Log `Warning` above 2 s.

### Data model / migration

`AddReportPermissions` (`MyDbContext`, **Additive**): RoleClaims `InsertData` Admin 16 `ViewReports`, 17 `ExportReports`; Manager 1015 `ViewReports`, 1016 `ExportReports` (appended at the **end** of `RolePolicies.Admin/Manager.AllowedPermissions`; added to `Permissions.All` under `Permissions.Reports`). `Down()` deletes the 4 ids. No schema change. **Applying it to any database — including `mydb_test` through `dotnet test` and `mydb` through `AutoMigration` — needs the user's approval; `mydb_test` is approved, `mydb` is not (ask again).**

**Id-collision check (verified 2026-11-21 against `RolePolicies.cs` as it stands on this branch — re-verify at implementation time in case the branch has moved):** `RoleClaimConfiguration.SeedRole` assigns ids positionally per role, starting at a fixed block (`Admin` 1, `Manager` 1001, `Member` 2001), so the three roles' id ranges never overlap regardless of count. `RolePolicies.Admin.AllowedPermissions` has 15 entries today (`ViewMembers`…`ManageUsers`, the last being the round-2 `Users.ManageUsers` permission) → ids 1–15 taken, **16/17 free**. `RolePolicies.Manager.AllowedPermissions` has 14 entries → ids 1001–1014 taken, **1015/1016 free**. `RolePolicies.Member.AllowedPermissions` has 5 entries → 2001–2005, unaffected either way. No existing or round-2 permission (including `ManageUsers`, `PlanTasks`, `AssignAnyone`) occupies 16, 17, 1015 or 1016. backend-lead re-confirms this count against the live file before generating the migration (stop gate, below) rather than trusting this snapshot.

### Activity removal (same branch)

Delete: `ActivityController.cs`, `ActivityMapper.cs`, `Models/Requests/Activity/`, `Models/Responses/Activity/`, `Models/Responses/Base/CursorResultRes.cs`, `Dtos/Activity/`, `ActivityControllerMS.cs`, `Common/Helpers/ActivityCursor.cs`, `ITaskHistoryService.GetFeed` (+ its private helpers), `Tests/Http/ActivityControllerTests.cs`. Keep: `TaskHistoryService.GetList`, `TaskHistoryMessages`, the per-task endpoint and its tests, both feed indexes (dropping them is not additive; `IX_TaskHistories_CreatedAt_Id` serves query 1). Client: see SPEC §11.9. **Breaking change:** `GET /activity` removed — announced here. Its only consumer is removed in the same branch.

## Operations

No new setting, job or secret. One additive seed migration (approval required). New FE dependency `recharts@3.10.1` (approval required). Deploy note: existing sessions gain `ViewReports`/`ExportReports` at their next refresh (≤ 15 min, `Security:Jwt:ExpirationMinutes`).

## Follow-up doc changes (on approval)

- `f04-activity-feed.md` Status → **Superseded by f09 / removed 2026-11-21**. `adr/0007` Status → Superseded by 0008. `f04-audit.md` R2 → note the feed's removal.
- `SPEC.md` §10.15 → superseded by §11. §3 nav → "Reports".
- `ROADMAP.md` → new unit "F09 Reports": slice R3-1 backend (permissions, migration, endpoints, calculator, Activity BE removal, tests), slice R3-2 frontend (page, charts, drill-down, CSV, Activity FE removal, nav, Vitest).
- `CLAUDE.md` (proposed diff): *In scope* + "per-member Reports (F09) with CSV export"; *Out of scope* "dashboards, exports" → "dashboards and exports other than F09 Reports"; *Frontend* → Vitest present (`npm --prefix FE test`); *Tests* row frontend runner → Vitest 5.1.0; Development commands Frontend test → `npm --prefix FE test`; add `recharts` (exact pin) to the frontend stack.

## Open questions

See the architect's handoff (Q1–Q14) in the task's chat record; defaults are applied in this spec.
