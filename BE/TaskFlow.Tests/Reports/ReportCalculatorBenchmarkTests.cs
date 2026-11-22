using System.Diagnostics;
using TaskFlow.BusinessLogic.Reports;
using TaskFlow.Common.Enums;

namespace TaskFlow.Tests.Reports
{
    // Pure, no DB (testing.md §1: through the public seam, no shared state -
    // same reasoning as ReportCalculatorTests, so no
    // [Collection(DatabaseCollection.Name)]). Exercises the exact calculator
    // path GetTeam uses on a roster: ExtractFacts once per task, then one
    // bulk per-roster-member aggregation over the shared team range/window.
    // Budget: ADR 0008/f09-reports.md, <1 s for <=50 users / <=5000 tasks /
    // <=100000 history rows over a 1-year team report - see
    // docs/reports/slice-r3-1.md for why the 208 s field measurement did not
    // actually exercise this budget.
    public class ReportCalculatorBenchmarkTests
    {
        private const string Zone = "Asia/Ho_Chi_Minh";
        private const int Limit = 3;
        private const int UserCount = 50;
        private const int TaskCount = 5000;

        [Fact]
        public void BuildMetricSetsForRoster_FiftyUsersFiveThousandTasks_OneYear_RunsUnderOneSecond()
        {
            var tasks = BuildSyntheticTasks(TaskCount, UserCount);
            var historyEntryCount = tasks.Sum(t => t.History.Count);
            Assert.InRange(historyEntryCount, 15000, 25000);

            var facts = tasks.Select(t => ReportCalculator.ExtractFacts(t, Zone)).ToList();

            var rangeStart = new DateOnly(2025, 1, 1);
            var rangeEnd = new DateOnly(2025, 12, 31);
            var measuredStartUtc = new DateTimeOffset(2024, 12, 31, 17, 0, 0, TimeSpan.Zero);
            var measuredEndUtcExclusive = new DateTimeOffset(2025, 12, 31, 17, 0, 0, TimeSpan.Zero);
            var holderIds = Enumerable.Range(1, UserCount).Select(i => (long)i).ToList();

            var stopwatch = Stopwatch.StartNew();

            // Team totals (teamScope: true), mirroring GetTeam.
            var teamTotals = ReportCalculator.BuildMetricSet(facts, teamScope: true, holderId: null, rangeStart, rangeEnd, measuredStartUtc, measuredEndUtcExclusive, Limit, Zone, includeLoad: true);

            // One bulk pass for every roster member, mirroring GetTeam's
            // members loop - this is the call BuildMetricSetsForRoster
            // replaces a per-member BuildMetricSet loop with.
            var memberMetricSets = ReportCalculator.BuildMetricSetsForRoster(facts, holderIds, rangeStart, rangeEnd, measuredStartUtc, measuredEndUtcExclusive, Limit, Zone, includeLoad: true);

            stopwatch.Stop();

            Assert.Equal(UserCount, memberMetricSets.Count);
            Assert.True(teamTotals.Delivery.Done > 0, "Expected some completions in the synthesized dataset.");

            var elapsedMs = stopwatch.ElapsedMilliseconds;
            // Printed for the record (docs/reports/slice-r3-1.md); xunit
            // shows this on failure/verbose output.
            Assert.True(elapsedMs < 1000, $"Expected the roster pass over {TaskCount} tasks / {UserCount} users / {historyEntryCount} history rows to take < 1000 ms, took {elapsedMs} ms.");
        }

        // One task per index, round-robined across users, 4 history entries
        // each (Created, Todo->InProgress, InProgress->Review,
        // Review->Done), spread across calendar year 2025 so every task
        // falls inside the measured range.
        private static List<ReportTaskInput> BuildSyntheticTasks(int taskCount, int userCount)
        {
            var tasks = new List<ReportTaskInput>(taskCount);
            var yearStart = new DateTimeOffset(2025, 1, 1, 0, 0, 0, TimeSpan.FromHours(7));

            for (var i = 0; i < taskCount; i++)
            {
                var assigneeId = (long)(i % userCount) + 1;
                var offsetDays = i % 360;
                var createdAt = yearStart.AddDays(offsetDays).AddHours(i % 24);

                var history = new List<ReportHistoryEntryInput>
                {
                    new() { Id = i * 4L + 1, Event = TaskHistoryEvent.Created, CreatedAt = createdAt },
                    new() { Id = i * 4L + 2, Event = TaskHistoryEvent.StatusChanged, CreatedAt = createdAt.AddHours(1), FromValue = nameof(TaskWorkflowStatus.Todo), ToValue = nameof(TaskWorkflowStatus.InProgress) },
                    new() { Id = i * 4L + 3, Event = TaskHistoryEvent.StatusChanged, CreatedAt = createdAt.AddDays(1), FromValue = nameof(TaskWorkflowStatus.InProgress), ToValue = nameof(TaskWorkflowStatus.Review) },
                    new() { Id = i * 4L + 4, Event = TaskHistoryEvent.StatusChanged, CreatedAt = createdAt.AddDays(2), FromValue = nameof(TaskWorkflowStatus.Review), ToValue = nameof(TaskWorkflowStatus.Done), ActorUserId = assigneeId },
                };

                tasks.Add(new ReportTaskInput
                {
                    TaskId = i + 1,
                    Title = $"Synthetic task {i + 1}",
                    IsDeleted = false,
                    Status = TaskWorkflowStatus.Done,
                    Priority = TaskPriority.Normal,
                    AssigneeId = assigneeId,
                    DueDate = createdAt.AddDays(3).Date is var d ? DateOnly.FromDateTime(d) : null,
                    CreatedAt = createdAt,
                    History = history,
                });
            }

            return tasks;
        }
    }
}
