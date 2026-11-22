# Slice R3-1 — F09 Reports: EXPLAIN and timing

Recorded 2026-11-21 against `mydb_test`.

Seeded dataset: 3000 tasks (`Tasks`), 12000 history rows (`TaskHistories`, 4 events/task), 40 users, spread across calendar year 2025.

## Query 2 — candidate tasks scan

```sql
SET @__toUtcExclusive_0 = TIMESTAMP '2025-12-31 17:00:00';
SET @__fromUtc_1 = TIMESTAMP '2024-12-31 17:00:00';
SET @__fromUtc_2 = TIMESTAMP '2024-12-31 17:00:00';

SELECT `t`.`Id`, `t`.`Title`, `t`.`Status`, `t`.`Priority`, `t`.`AssigneeId`, `t`.`DueDate`, `t`.`IsDeleted`, `t`.`CreatedAt`
FROM `Tasks` AS `t`
WHERE ((`t`.`CreatedAt` < @__toUtcExclusive_0) AND ((`t`.`Status` <> 5) OR CASE
    WHEN `t`.`CompletedAt` < @__fromUtc_1 THEN FALSE
    ELSE TRUE
END)) AND (NOT (`t`.`IsDeleted`) OR (`t`.`UpdatedAt` >= @__fromUtc_2))
```

EXPLAIN:

```
id | select_type | table | partitions | type | possible_keys | key | key_len | ref | rows | filtered | Extra
1 | SIMPLE | t |  | ALL | IX_Tasks_IsDeleted_Status_CompletedAt,IX_Tasks_IsDeleted_Status_Priority_DueDate,IX_Tasks_IsDeleted_AssigneeId_Status,IX_Tasks_IsDeleted_IsOverdue_Status_DueDate |  |  |  | 3031 | 33.28 | Using where
```


## Query 3 — histories IN-chunk (500 task ids)

```sql
SELECT `t`.`TaskId`, `t`.`Id`, `t`.`Event`, `t`.`ActorUserId`, `t`.`FromValue`, `t`.`ToValue`, `t`.`CreatedAt`
FROM `TaskHistories` AS `t`
WHERE `t`.`TaskId` IN (61641, 61711, 61729, 61770, 61873, 61924, 61952, 62133, 62181, 62183, 62218, 62240, 62246, 62252, 62269, 62318, 62402, 62641, 62650, 62662, 62782, 62847, 62864, 62876, 62878, 62995, 63068, 63094, 63105, 63109, 63129, 63203, 63229, 63282, 63342, 63387, 63515, 63579, 63582, 63623, 63641, 63663, 63669, 63672, 63835, 63879, 63886, 63887, 63903, 63938, 64019, 64173, 64235, 64261, 64356, 64427, 64440, 64503, 64515, 64532, 64539, 64558, 64566, 64626, 61683, 61725, 61773, 61842, 61892, 61910, 61974, 62006, 62008, 62042, 62103, 62266, 62382, 62442, 62465, 62493, 62535, 62546, 62573, 62574, 62652, 62743, 62816, 62858, 62887, 62955, 63003, 63022, 63066, 63162, 63205, 63228, 63248, 63299, 63340, 63405, 63421, 63481, 63592, 63608, 63621, 63677, 63700, 63814, 63833, 63862, 63918, 64033, 64035, 64100, 64119, 64120, 64243, 64251, 64396, 64443, 64447, 64449, 64522, 61628, 61645, 61646, 61865, 61885, 61907, 61947, 62021, 62062, 62085, 62091, 62147, 62222, 62265, 62349, 62369, 62372, 62375, 62407, 62520, 62551, 62583, 62589, 62634, 62685, 62748, 62888, 62929, 62975, 63054, 63059, 63093, 63140, 63141, 63306, 63333, 63368, 63381, 63383, 63416, 63443, 63466, 63530, 63561, 63601, 63785, 63888, 63907, 63923, 63930, 63936, 63985, 64004, 64022, 64086, 64089, 64122, 64125, 64150, 64160, 64168, 64177, 64184, 64200, 64361, 64375, 64420, 64451, 64512, 61640, 61677, 61844, 61875, 61921, 61979, 62092, 62095, 62100, 62105, 62129, 62130, 62182, 62186, 62273, 62343, 62399, 62456, 62504, 62566, 62596, 62633, 62669, 62677, 62680, 62683, 62799, 62834, 62994, 63004, 63041, 63092, 63113, 63123, 63180, 63254, 63262, 63375, 63397, 63447, 63662, 63690, 63777, 63846, 63878, 63932, 63935, 63953, 63965, 64002, 64013, 64062, 64105, 64136, 64154, 64186, 64258, 64264, 64354, 64370, 64386, 64395, 64446, 64450, 64462, 64480, 64536, 64562, 64568, 61632, 61672, 61703, 61737, 61758, 61811, 61826, 61827, 61848, 61854, 61909, 61932, 61964, 61980, 62010, 62014, 62046, 62098, 62111, 62123, 62126, 62163, 62180, 62194, 62217, 62488, 62549, 62619, 62642, 62654, 62673, 62705, 62711, 62718, 62762, 62821, 62829, 62896, 62930, 62982, 63018, 63061, 63125, 63147, 63172, 63220, 63221, 63274, 63345, 63347, 63376, 63403, 63417, 63459, 63478, 63508, 63545, 63552, 63595, 63598, 63625, 63688, 63699, 63708, 63759, 63765, 63768, 63944, 63968, 63982, 63986, 64048, 64075, 64126, 64129, 64159, 64164, 64190, 64230, 64394, 64435, 64494, 64509, 64523, 64531, 64577, 64586, 64619, 61705, 61706, 61710, 61808, 61850, 61867, 61925, 61946, 61978, 62094, 62099, 62154, 62176, 62239, 62306, 62314, 62337, 62394, 62414, 62423, 62447, 62460, 62464, 62470, 62541, 62578, 62579, 62591, 62741, 62760, 62844, 62946, 62978, 63032, 63108, 63121, 63179, 63195, 63211, 63233, 63242, 63365, 63486, 63505, 63549, 63556, 63590, 63605, 63624, 63652, 63691, 63712, 63778, 63864, 63884, 63925, 63927, 63970, 64106, 64274, 64278, 64279, 64296, 64401, 64433, 64483, 64546, 64623, 64624, 61795, 61798, 61802, 61841, 61853, 61878, 61894, 61953, 61957, 61982, 61986, 62073, 62153, 62187, 62191, 62400, 62469, 62472, 62557, 62692, 62747, 62754, 62761, 62770, 62797, 62806, 62807, 62810, 62872, 62893, 62999, 63030, 63051, 63114, 63139, 63146, 63161, 63163, 63251, 63284, 63374, 63409, 63544, 63573, 63578, 63661, 63698, 63709, 63715, 63722, 63776, 63817, 63832, 63914, 63976, 64095, 64153, 64162, 64170, 64175, 64221, 64234, 64253, 64273, 64342, 64348, 64464, 64482, 64556, 64585, 64587, 64600, 64605, 61690, 61747, 61769, 61817, 61891, 61941, 61948, 61954, 61956)
ORDER BY `t`.`TaskId`, `t`.`CreatedAt`, `t`.`Id`
```

EXPLAIN:

```
id | select_type | table | partitions | type | possible_keys | key | key_len | ref | rows | filtered | Extra
1 | SIMPLE | t |  | range | IX_TaskHistories_TaskId_CreatedAt_Id | IX_TaskHistories_TaskId_CreatedAt_Id | 8 |  | 2000 | 100 | Using index condition
```


## Timing — 1-year team report

`IReportService.GetTeam(2025-01-01, 2025-12-31)` (Week granularity, fixed), called in-process (not over HTTP - see note below): **208264 ms**.

**Confound, reported honestly rather than hidden:** this run's actual active-user roster was **30548**, not the 40 users this diagnostic seeded. `mydb_test` is a long-lived database shared by the whole backend test suite; `Users` is never reset between test runs (unlike `Tasks`/`TaskHistories`/`TaskNotifications`, which every DB-touching test class resets in its own `InitializeAsync`), so it accumulates every user ever created by every test run executed against it. `GetTeam` computes one `BuildMetricSet` pass per roster member (R11: every active user), so its cost scales with the *total* active-user count, not just this dataset's 40 - at the assumed production scale (<= 50 users) that is negligible, but at this test database's accumulated scale it dominates the measurement above. An HTTP-level timing attempt against the same data timed out (the TestServer client aborted after ~4 minutes) before being replaced with the in-process call above.

Budget (ADR 0008/f09-reports.md): < 1 s for a 1-year team report at the assumed scale (<= 50 users, <= 5000 candidate tasks, <= 100000 history rows). This run's own seeded data was 3000 tasks / 12000 history rows (within budget); the active-user count above was not controllable from within this diagnostic without a destructive cleanup of `mydb_test`'s `Users` table, which was not attempted (out of scope, needs separate approval).

## Follow-up — 2026-11-22, pure calculator benchmark (no DB)

The 208264 ms figure above was not a measurement of the budget: it was dominated by `mydb_test`'s 30548 accumulated `Users` rows (the roster `GetTeam` iterates), not by this slice's own 40-user / 3000-task / 12000-history-row dataset. To verify the actual budget without that confound, `BE/TaskFlow.Tests/Reports/ReportCalculatorBenchmarkTests.cs` synthesizes 50 users, 5000 tasks and ~20000 history rows across one year in memory (no database) and calls the same calculator path `GetTeam` uses: `ReportCalculator.ExtractFacts` once per task, then one aggregation pass over the full roster for the shared team range/window.

- **Before** (looping `ReportCalculator.BuildMetricSet(teamScope: false, holderId: id, ...)` once per roster member — the code `GetTeam` ran before this change): **164 ms**.
- **After** (`ReportCalculator.BuildMetricSetsForRoster`, added in this change — groups every fact by holder once, then each member reads its own slice, O(tasks + users) instead of O(users x tasks)): **72 ms**.

Both numbers are well inside the < 1000 ms budget at the assumed scale (<= 50 users): at 50 users x 5000 tasks the per-member rescans were cheap enough in absolute terms that the budget was never actually at risk at the assumed production scale. The optimization was still made (`BE/TaskFlow.BusinessLogic/Reports/ReportCalculator.cs`, `BE/TaskFlow.BusinessLogic/Services/ReportService.cs`'s `GetTeam`) because the O(users x tasks) shape is the one that degrades as either dimension grows, and the grouped form costs nothing in readability or output: every existing `ReportCalculatorTests` and `ReportsControllerTests` assertion (AC1-AC22) passes unchanged, since `BuildMetricSet`'s own signature and behaviour are untouched — only `GetTeam`'s internal loop now calls the new bulk method instead.

**Test-hygiene follow-up (proposed, not actioned):** `mydb_test`'s `Users` table is never reset between test runs or between sessions (unlike `Tasks`/`TaskHistories`/`TaskNotifications`, which every DB-touching test class resets in its own `InitializeAsync`), so it only ever grows — 30548 rows by the time of the original 208 s measurement, from accumulated runs of every test class that creates a user. This makes any future *database-backed* timing diagnostic against `mydb_test` unreliable for anything that scans or iterates the full `Users` table (as `GetTeam`'s roster does), and will keep growing. Worth a ticket: either reset `Users` per test class the way the other high-churn tables already are, or exclude users seeded by other test classes from roster-shaped diagnostics some other way. Out of scope here — `mydb_test`'s `Users` table was not touched.
