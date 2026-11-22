using TaskFlow.BusinessLogic.Dtos.Reports;
using TaskFlow.BusinessLogic.Reports;
using TaskFlow.Common.Enums;
using TaskFlow.Common.Helpers;

namespace TaskFlow.Tests.Reports
{
    // f09-reports.md fixture F, AC1-AC10, R7's fix and R5b. Pure - no
    // database, no [Collection(DatabaseCollection.Name)] needed (testing.md
    // §1: through the public seam, no wall clock, no shared state).
    public class ReportCalculatorTests
    {
        private const string Zone = "Asia/Ho_Chi_Minh";
        private const long M1 = 101;
        private const long M2 = 102;
        private const long Mgr = 103;

        // ---- ReportStatistics ----

        [Fact]
        public void Median_OddCount_ReturnsMiddleValue()
        {
            Assert.Equal(200L, ReportStatistics.Median([100, 300, 200]));
        }

        [Fact]
        public void Median_EvenCount_ReturnsTruncatedMeanOfMiddleTwo()
        {
            // (100 + 201) / 2 = 150.5 -> truncated to 150
            Assert.Equal(150L, ReportStatistics.Median([100, 201]));
        }

        [Fact]
        public void P85_NearestRank_SevenValues_UsesRankSix()
        {
            var values = new long[] { 1, 2, 3, 4, 5, 6, 7 };
            Assert.Equal(6L, ReportStatistics.P85(values));
        }

        [Fact]
        public void P85_NearestRank_FiveValues_UsesRankFive()
        {
            var values = new long[] { 1, 2, 3, 4, 5 };
            Assert.Equal(5L, ReportStatistics.P85(values));
        }

        [Fact]
        public void P85_NullWhenSampleBelowThreshold()
        {
            Assert.Null(ReportStatistics.P85([1, 2, 3, 4]));
        }

        [Fact]
        public void BuildRate_NullValue_WhenDenominatorZero()
        {
            var rate = ReportStatistics.BuildRate(0, 0);
            Assert.Null(rate.Value);
            Assert.Equal(0, rate.Denominator);
        }

        // ---- Holder reconstruction (R2) ----

        [Fact]
        public void ExtractFacts_Holder_UsesCurrentAssignee_WhenNeverReassigned()
        {
            var task = BuildTask(900, M1, TaskWorkflowStatus.Done, null,
                H(1, TaskHistoryEvent.Created, Local(2026, 3, 2, 9, 0)),
                H(2, TaskHistoryEvent.StatusChanged, Local(2026, 3, 2, 10, 0), "Todo", "InProgress"),
                H(3, TaskHistoryEvent.StatusChanged, Local(2026, 3, 3, 10, 0), "InProgress", "Review"),
                H(4, TaskHistoryEvent.StatusChanged, Local(2026, 3, 4, 10, 0), "Review", "Done", Mgr));

            var facts = ReportCalculator.ExtractFacts(task, Zone);

            Assert.Equal(M1, facts.Completion!.HolderId);
        }

        [Fact]
        public void ExtractFacts_Holder_ReassignedTwice_CreditsHolderAtTheEntry()
        {
            // m1 -> m2 -> mgr, completion happens after both reassignments,
            // so the current assignee (mgr) is credited.
            var task = BuildTask(901, Mgr, TaskWorkflowStatus.Done, null,
                H(1, TaskHistoryEvent.Created, Local(2026, 3, 1, 9, 0)),
                H(2, TaskHistoryEvent.StatusChanged, Local(2026, 3, 1, 10, 0), "Todo", "InProgress"),
                H(3, TaskHistoryEvent.AssigneeChanged, Local(2026, 3, 2, 9, 0), M1.ToString(), M2.ToString(), M2),
                H(4, TaskHistoryEvent.AssigneeChanged, Local(2026, 3, 3, 9, 0), M2.ToString(), Mgr.ToString(), Mgr),
                H(5, TaskHistoryEvent.StatusChanged, Local(2026, 3, 3, 10, 0), "InProgress", "Review"),
                H(6, TaskHistoryEvent.StatusChanged, Local(2026, 3, 4, 10, 0), "Review", "Done", Mgr));

            var facts = ReportCalculator.ExtractFacts(task, Zone);

            Assert.Equal(Mgr, facts.Completion!.HolderId);
        }

        [Fact]
        public void ExtractFacts_SameTimestampEntries_OrderedById()
        {
            // Two entries sharing one instant: the reassignment (id 3)
            // before the completion (id 4) in (CreatedAt, Id) order means
            // the completion is credited to the new holder, not the old one.
            var sameInstant = Local(2026, 3, 4, 10, 0);
            var task = BuildTask(902, M2, TaskWorkflowStatus.Done, null,
                H(1, TaskHistoryEvent.Created, Local(2026, 3, 1, 9, 0)),
                H(2, TaskHistoryEvent.StatusChanged, Local(2026, 3, 1, 10, 0), "Todo", "InProgress"),
                H(3, TaskHistoryEvent.AssigneeChanged, sameInstant, M1.ToString(), M2.ToString(), M2),
                H(4, TaskHistoryEvent.StatusChanged, sameInstant, "Review", "Done", Mgr),
                H(5, TaskHistoryEvent.StatusChanged, Local(2026, 3, 3, 10, 0), "InProgress", "Review"));

            var facts = ReportCalculator.ExtractFacts(task, Zone);

            // Entry 3 (AssigneeChanged) is listed before entry 4 (the
            // completion), so the completion's holder-at-entry scan starting
            // at entry 4's position never sees entry 3 - the current
            // assignee (M2) is credited.
            Assert.Equal(M2, facts.Completion!.HolderId);
        }

        // ---- R3 / reopen ----

        [Fact]
        public void ExtractFacts_ReopenThenDoneAgain_CountedOnce()
        {
            var task = BuildFixtureT6();
            var facts = ReportCalculator.ExtractFacts(task, Zone);

            Assert.NotNull(facts.Completion);
            Assert.Equal(Local(2026, 3, 7, 15, 0), facts.Completion!.CompletedAtUtc);
            Assert.Equal(1, facts.Reopens.Count);
        }

        [Fact]
        public void ExtractFacts_ReopenedNotYetRedone_CountedZero()
        {
            var task = BuildTask(903, M1, TaskWorkflowStatus.Review, null,
                H(1, TaskHistoryEvent.Created, Local(2026, 3, 1, 9, 0)),
                H(2, TaskHistoryEvent.StatusChanged, Local(2026, 3, 1, 10, 0), "Todo", "InProgress"),
                H(3, TaskHistoryEvent.StatusChanged, Local(2026, 3, 2, 10, 0), "InProgress", "Review"),
                H(4, TaskHistoryEvent.StatusChanged, Local(2026, 3, 3, 10, 0), "Review", "Done", Mgr),
                H(5, TaskHistoryEvent.Reopened, Local(2026, 3, 4, 9, 0), "Done", "Review", Mgr));

            var facts = ReportCalculator.ExtractFacts(task, Zone);

            Assert.Null(facts.Completion);
            Assert.Equal(1, facts.Reopens.Count);
        }

        // ---- R10 deleted task ----

        [Fact]
        public void ExtractFacts_DeletedTask_InProgressIntervalEndsAtDeleted()
        {
            var deletedAt = Local(2026, 3, 3, 12, 0);
            var task = BuildTask(904, M1, TaskWorkflowStatus.InProgress, null, isDeleted: true,
                history:
                [
                    H(1, TaskHistoryEvent.Created, Local(2026, 3, 1, 9, 0)),
                    H(2, TaskHistoryEvent.StatusChanged, Local(2026, 3, 2, 10, 0), "Todo", "InProgress"),
                    H(3, TaskHistoryEvent.Deleted, deletedAt),
                ]);

            var facts = ReportCalculator.ExtractFacts(task, Zone);

            var segment = Assert.Single(facts.InProgressSegments);
            Assert.Equal(deletedAt, segment.EndUtc);
        }

        // ---- R6 overdue marked by a reopening (non-null) actor ----

        [Fact]
        public void ExtractFacts_OverdueMarkedByReopeningActor_StillCreditedToHolder()
        {
            var task = BuildTask(905, M1, TaskWorkflowStatus.Review, new DateOnly(2026, 3, 3),
                H(1, TaskHistoryEvent.Created, Local(2026, 3, 1, 9, 0)),
                H(2, TaskHistoryEvent.OverdueMarked, Local(2026, 3, 4, 0, 5), actor: Mgr));

            var facts = ReportCalculator.ExtractFacts(task, Zone);

            var mark = Assert.Single(facts.OverdueMarks);
            Assert.Equal(M1, mark.HolderId);
        }

        // ---- R7 regression: a Reopened entry is never a rework submission ----

        [Fact]
        public void ExtractFacts_ReopenedRoundTrip_NeverCountsAsSentBack_OnlyAsReopenedAfterDone()
        {
            var task = BuildFixtureT6();
            var facts = ReportCalculator.ExtractFacts(task, Zone);

            Assert.Equal(1, facts.Reopens.Count);
            Assert.DoesNotContain(facts.Submissions, s => s.Outcome == ReportSubmissionOutcome.SentBack);
            Assert.Equal(2, facts.Submissions.Count(s => s.Outcome == ReportSubmissionOutcome.Accepted));

            var metricSet = ReportCalculator.BuildMetricSet(
                [facts], teamScope: false, holderId: M1,
                rangeStart: new DateOnly(2026, 3, 2), rangeEnd: new DateOnly(2026, 3, 8),
                measuredStartUtc: Local(2026, 3, 2, 0, 0), measuredEndUtcExclusive: Local(2026, 3, 9, 0, 0),
                timeZone: Zone, includeLoad: false);

            Assert.Equal(0, metricSet.Quality.SentBack);
            Assert.Equal(1, metricSet.Quality.ReopenedAfterDone);
        }

        // ---- R5b review wait, independent of an earlier submission's own value ----

        [Fact]
        public void ExtractFacts_ReviewWait_PerSubmission_IndependentOfEarlierSubmission()
        {
            var task = BuildFixtureT2();
            var facts = ReportCalculator.ExtractFacts(task, Zone);

            // T2's completion is credited with the review wait of the
            // *second* (decided) submission (21h = 75600s), not the first,
            // already-sent-back submission's own wait (03-03 12:00 ->
            // 03-03 15:00 = 3h), which never reaches a completion at all.
            Assert.Equal(75600, facts.Completion!.ReviewWaitSeconds);
        }

        // ---- Fixture F, AC1-AC10 ----

        [Fact]
        public void FixtureF_AC1_Delivery_CreditedToHolder()
        {
            var facts = BuildFixtureFacts();

            var m1 = BuildWeekMetricSet(facts, M1);
            var m2 = BuildWeekMetricSet(facts, M2);
            var mgr = BuildWeekMetricSet(facts, Mgr);

            Assert.Equal(3, m1.Delivery.Done);
            Assert.Equal(2, m2.Delivery.Done);
            Assert.Equal(0, mgr.Delivery.Done);
        }

        [Fact]
        public void FixtureF_AC2_Takeover_CreditedToNewHolder()
        {
            var facts = BuildFixtureFacts();

            var m1 = BuildWeekMetricSet(facts, M1);
            var m2 = BuildWeekMetricSet(facts, M2);

            Assert.Equal(1, m2.Attribution.TakeOvers);
            Assert.Equal(1, m1.Attribution.TakenFrom);
        }

        [Fact]
        public void FixtureF_AC3_ReopenedTask_CountedOnceWithExpectedCycleTime()
        {
            var facts = BuildFixtureFacts();
            var m1 = BuildWeekMetricSet(facts, M1);

            Assert.Equal(1, m1.Quality.ReopenedAfterDone);
            Assert.Contains(360000L, CycleTimes(facts, M1));
        }

        [Fact]
        public void FixtureF_AC4_Speed_MedianAndLowSample()
        {
            var facts = BuildFixtureFacts();
            var m1 = BuildWeekMetricSet(facts, M1);

            Assert.Equal(3, m1.Speed.CycleTime.SampleSize);
            Assert.Equal(248400L, m1.Speed.CycleTime.MedianSeconds);
            Assert.Null(m1.Speed.CycleTime.P85Seconds);
            Assert.True(m1.Speed.CycleTime.LowSample);

            Assert.Equal(3, m1.Speed.ReviewWait.SampleSize);
            Assert.Equal(75600L, m1.Speed.ReviewWait.MedianSeconds);
            Assert.Null(m1.Speed.ReviewWait.P85Seconds);
            Assert.True(m1.Speed.ReviewWait.LowSample);
        }

        [Fact]
        public void FixtureF_AC5_Reliability_OnTimeRate()
        {
            var facts = BuildFixtureFacts();
            var m1 = BuildWeekMetricSet(facts, M1);

            Assert.Equal(1, m1.Reliability.OnTime);
            Assert.Equal(1, m1.Reliability.Late);
            Assert.Equal(1, m1.Reliability.NoDueDate);
            Assert.Equal(0.5, m1.Reliability.OnTimeRate.Value);
            Assert.Equal(2, m1.Reliability.OnTimeRate.Denominator);
            Assert.True(m1.Reliability.OnTimeRate.LowSample);
        }

        [Fact]
        public void FixtureF_AC6_OverdueMarked_UnassignedIsTeamOnly()
        {
            var facts = BuildFixtureFacts();
            var m1 = BuildWeekMetricSet(facts, M1);

            Assert.Equal(1, m1.Reliability.OverdueMarked);

            var unassigned = facts.SelectMany(f => f.OverdueMarks)
                .Count(o => o.HolderId == null && o.AtUtc.ToDateInZone(Zone) >= new DateOnly(2026, 3, 2) && o.AtUtc.ToDateInZone(Zone) <= new DateOnly(2026, 3, 8));
            Assert.Equal(1, unassigned);
        }

        [Fact]
        public void FixtureF_AC7_Rework_NeverDoubleCountsReopen()
        {
            var facts = BuildFixtureFacts();
            var m1 = BuildWeekMetricSet(facts, M1);

            Assert.Equal(5, m1.Quality.SubmissionsDecided);
            Assert.Equal(1, m1.Quality.SentBack);
            Assert.Equal(0.2, m1.Quality.ReworkRate.Value);
            Assert.False(m1.Quality.ReworkRate.LowSample);
            Assert.Equal(1, m1.Quality.ReopenedAfterDone);
        }

        // AC8 (replaced 2026-11-22: ticketsWorked is a distinct-task count,
        // not a time-weighted average). Day bucket 2026-03-02 local
        // (00:00..24:00). Hand derivation from fixture F's InProgress
        // segments overlapping that window:
        // - T1: InProgress 03-02 10:00 -> 03-03 10:00 (holder m1) - overlaps.
        // - T2: two m1 segments, 03-02 12:00 -> 03-03 12:00 and
        //   03-03 15:00 -> 03-04 12:00 - the first overlaps, the second
        //   starts after the day ends, so T2 is still counted once (from
        //   the first segment).
        // - T3: m1 03-02 09:00 -> 03-03 09:00 (then handed to m2) - overlaps.
        // - T6: m1 03-03 11:00 -> 03-04 09:00 and 03-06 10:00 -> 03-07 09:00
        //   - both start after the day ends (03-03 00:00), no overlap.
        // Distinct m1 tasks overlapping 03-02: {T1, T2, T3} -> 3.
        [Fact]
        public void FixtureF_AC8_Load_DayBucket()
        {
            var facts = BuildFixtureFacts();

            var dayStart = Local(2026, 3, 2, 0, 0);
            var dayEnd = Local(2026, 3, 3, 0, 0);

            var m1 = ReportCalculator.BuildMetricSet(
                facts, teamScope: false, holderId: M1,
                rangeStart: new DateOnly(2026, 3, 2), rangeEnd: new DateOnly(2026, 3, 2),
                measuredStartUtc: dayStart, measuredEndUtcExclusive: dayEnd,
                timeZone: Zone, includeLoad: true);

            Assert.Equal(3, m1.Load!.TicketsWorked);
        }

        // Handover case (R8: "a task handed over mid-bucket counts for BOTH
        // holders"). Day bucket 2026-03-03 local - T3's handover from m1 to
        // m2 happens at 03-03 09:00, squarely inside this day:
        // - T3 m1 segment 03-02 09:00 -> 03-03 09:00 overlaps [03-03 00:00,
        //   03-03 09:00) -> counted for m1.
        // - T3 m2 segment 03-03 09:00 -> 03-04 09:00 overlaps [03-03 09:00,
        //   03-04 00:00) -> counted for m2.
        // Also in this window: T1 (03-02 10:00 -> 03-03 10:00, overlaps
        // 03-03 00:00-10:00), T2's first m1 segment (03-02 12:00 -> 03-03
        // 12:00, overlaps 03-03 00:00-12:00), T6's first m1 segment (03-03
        // 11:00 -> 03-04 09:00, overlaps). m1 distinct: {T1, T2, T3, T6} -> 4.
        // m2 distinct: {T3} -> 1. Team (distinct across both holders): {T1,
        // T2, T3, T6} -> 4 (T3 is one task, not two, at team scope), so the
        // member sum (4 + 1 = 5) exceeds the team total (4) by exactly the
        // one handed-over task - the documented "sum can exceed team total"
        // property.
        [Fact]
        public void FixtureF_LoadHandover_CountsForBothHolders_DayBucket()
        {
            var facts = BuildFixtureFacts();

            var dayStart = Local(2026, 3, 3, 0, 0);
            var dayEnd = Local(2026, 3, 4, 0, 0);

            var m1 = ReportCalculator.BuildMetricSet(
                facts, teamScope: false, holderId: M1,
                rangeStart: new DateOnly(2026, 3, 3), rangeEnd: new DateOnly(2026, 3, 3),
                measuredStartUtc: dayStart, measuredEndUtcExclusive: dayEnd,
                timeZone: Zone, includeLoad: true);
            var m2 = ReportCalculator.BuildMetricSet(
                facts, teamScope: false, holderId: M2,
                rangeStart: new DateOnly(2026, 3, 3), rangeEnd: new DateOnly(2026, 3, 3),
                measuredStartUtc: dayStart, measuredEndUtcExclusive: dayEnd,
                timeZone: Zone, includeLoad: true);
            var team = ReportCalculator.BuildMetricSet(
                facts, teamScope: true, holderId: null,
                rangeStart: new DateOnly(2026, 3, 3), rangeEnd: new DateOnly(2026, 3, 3),
                measuredStartUtc: dayStart, measuredEndUtcExclusive: dayEnd,
                timeZone: Zone, includeLoad: true);

            Assert.Equal(4, m1.Load!.TicketsWorked);
            Assert.Equal(1, m2.Load!.TicketsWorked);
            Assert.Equal(4, team.Load!.TicketsWorked);
            Assert.True(m1.Load.TicketsWorked + m2.Load.TicketsWorked > team.Load.TicketsWorked);
        }

        // ---- Calculator-level ticketsWorked tests (2026-11-22) ----

        [Fact]
        public void TicketsWorked_SpanningTwoBuckets_CountsOnceInEachBucketAndOnceInTotals()
        {
            // One continuous InProgress interval crossing a day boundary:
            // 03-02 20:00 -> 03-03 04:00.
            var task = BuildTask(950, M1, TaskWorkflowStatus.InProgress, null,
                H(1, TaskHistoryEvent.Created, Local(2026, 3, 2, 8, 0)),
                H(2, TaskHistoryEvent.StatusChanged, Local(2026, 3, 2, 20, 0), "Todo", "InProgress"));
            var facts = ReportCalculator.ExtractFacts(task, Zone);

            var day1 = ReportCalculator.BuildMetricSet(
                [facts], teamScope: false, holderId: M1,
                rangeStart: new DateOnly(2026, 3, 2), rangeEnd: new DateOnly(2026, 3, 2),
                measuredStartUtc: Local(2026, 3, 2, 0, 0), measuredEndUtcExclusive: Local(2026, 3, 3, 0, 0),
                timeZone: Zone, includeLoad: true);
            var day2 = ReportCalculator.BuildMetricSet(
                [facts], teamScope: false, holderId: M1,
                rangeStart: new DateOnly(2026, 3, 3), rangeEnd: new DateOnly(2026, 3, 3),
                measuredStartUtc: Local(2026, 3, 3, 0, 0), measuredEndUtcExclusive: Local(2026, 3, 4, 0, 0),
                timeZone: Zone, includeLoad: true);
            var totals = ReportCalculator.BuildMetricSet(
                [facts], teamScope: false, holderId: M1,
                rangeStart: new DateOnly(2026, 3, 2), rangeEnd: new DateOnly(2026, 3, 3),
                measuredStartUtc: Local(2026, 3, 2, 0, 0), measuredEndUtcExclusive: Local(2026, 3, 4, 0, 0),
                timeZone: Zone, includeLoad: true);

            Assert.Equal(1, day1.Load!.TicketsWorked);
            Assert.Equal(1, day2.Load!.TicketsWorked);
            Assert.Equal(1, totals.Load!.TicketsWorked);
        }

        [Fact]
        public void TicketsWorked_TaskNeverEnteredInProgress_CountsZero()
        {
            var facts = ReportCalculator.ExtractFacts(BuildFixtureT7(), Zone);

            var m1 = ReportCalculator.BuildMetricSet(
                [facts], teamScope: false, holderId: M1,
                rangeStart: new DateOnly(2026, 3, 2), rangeEnd: new DateOnly(2026, 3, 8),
                measuredStartUtc: Local(2026, 3, 2, 0, 0), measuredEndUtcExclusive: Local(2026, 3, 9, 0, 0),
                timeZone: Zone, includeLoad: true);

            Assert.Equal(0, m1.Load!.TicketsWorked);
        }

        [Fact]
        public void TicketsWorked_DeletedTask_IntervalEndsAtDeleted_NotCountedAfter()
        {
            var deletedAt = Local(2026, 3, 3, 12, 0);
            var task = BuildTask(951, M1, TaskWorkflowStatus.InProgress, null, isDeleted: true,
                history:
                [
                    H(1, TaskHistoryEvent.Created, Local(2026, 3, 1, 9, 0)),
                    H(2, TaskHistoryEvent.StatusChanged, Local(2026, 3, 2, 10, 0), "Todo", "InProgress"),
                    H(3, TaskHistoryEvent.Deleted, deletedAt),
                ]);
            var facts = ReportCalculator.ExtractFacts(task, Zone);

            var beforeDeleted = ReportCalculator.BuildMetricSet(
                [facts], teamScope: false, holderId: M1,
                rangeStart: new DateOnly(2026, 3, 2), rangeEnd: new DateOnly(2026, 3, 3),
                measuredStartUtc: Local(2026, 3, 2, 0, 0), measuredEndUtcExclusive: Local(2026, 3, 3, 12, 0),
                timeZone: Zone, includeLoad: true);
            var afterDeleted = ReportCalculator.BuildMetricSet(
                [facts], teamScope: false, holderId: M1,
                rangeStart: new DateOnly(2026, 3, 3), rangeEnd: new DateOnly(2026, 3, 4),
                measuredStartUtc: Local(2026, 3, 3, 12, 0), measuredEndUtcExclusive: Local(2026, 3, 4, 0, 0),
                timeZone: Zone, includeLoad: true);

            Assert.Equal(1, beforeDeleted.Load!.TicketsWorked);
            Assert.Equal(0, afterDeleted.Load!.TicketsWorked);
        }

        [Fact]
        public void FixtureF_AC9_WeekBucket_SplitsAtLocalMidnight()
        {
            var facts = BuildFixtureFacts();

            var buckets = ReportBucketBuilder.Build(new DateOnly(2026, 3, 2), new DateOnly(2026, 3, 15), ReportGranularity.Week, new DateOnly(2026, 2, 1), Local(2026, 3, 16, 0, 0), Zone);

            var week1 = buckets.Single(b => b.NaturalStart == new DateOnly(2026, 3, 2));
            var week2 = buckets.Single(b => b.NaturalStart == new DateOnly(2026, 3, 9));

            var t4 = facts.Single(f => f.TaskId == 4);
            var t5 = facts.Single(f => f.TaskId == 5);
            var t4LocalDate = t4.Completion!.CompletedAtUtc.ToDateInZone(Zone);
            var t5LocalDate = t5.Completion!.CompletedAtUtc.ToDateInZone(Zone);

            // T4 (03-08 23:30 local) falls in week 03-02; T5 (03-09 00:10
            // local) falls in week 03-09 - the natural (unclipped) week
            // ranges, per R13's "event belongs to the bucket containing its
            // local date" (not the task's UTC instant).
            Assert.InRange(t4LocalDate, week1.NaturalStart, week1.NaturalEnd);
            Assert.InRange(t5LocalDate, week2.NaturalStart, week2.NaturalEnd);
            Assert.False(t4LocalDate >= week2.NaturalStart);
            Assert.False(t5LocalDate <= week1.NaturalEnd);
        }

        [Fact]
        public void BuildBuckets_PartialEdgeBuckets_ClippedToRequestRange()
        {
            var buckets = ReportBucketBuilder.Build(new DateOnly(2026, 3, 4), new DateOnly(2026, 3, 10), ReportGranularity.Week, new DateOnly(2026, 1, 1), Local(2026, 3, 20, 0, 0), Zone);

            Assert.Equal(2, buckets.Count);
            Assert.Equal(new DateOnly(2026, 3, 4), buckets[0].Start);
            Assert.Equal(new DateOnly(2026, 3, 8), buckets[0].End);
            Assert.True(buckets[0].IsPartial);
            Assert.Equal(new DateOnly(2026, 3, 9), buckets[1].Start);
            Assert.Equal(new DateOnly(2026, 3, 10), buckets[1].End);
            Assert.True(buckets[1].IsPartial);
        }

        [Fact]
        public void BuildBuckets_EmptyHistory_DataAvailableSinceNull_EveryBucketHasNoData()
        {
            var buckets = ReportBucketBuilder.Build(new DateOnly(2026, 3, 1), new DateOnly(2026, 3, 7), ReportGranularity.Day, null, Local(2026, 3, 8, 0, 0), Zone);

            Assert.All(buckets, b => Assert.False(b.HasData));
        }

        [Fact]
        public void BuildBuckets_MonthBoundary_IsCalendarAligned()
        {
            var buckets = ReportBucketBuilder.Build(new DateOnly(2026, 2, 15), new DateOnly(2026, 3, 15), ReportGranularity.Month, new DateOnly(2026, 1, 1), Local(2026, 4, 1, 0, 0), Zone);

            Assert.Equal(2, buckets.Count);
            Assert.Equal(new DateOnly(2026, 2, 1), buckets[0].NaturalStart);
            Assert.Equal(new DateOnly(2026, 2, 28), buckets[0].NaturalEnd);
            Assert.Equal(new DateOnly(2026, 3, 1), buckets[1].NaturalStart);
            Assert.Equal(new DateOnly(2026, 3, 31), buckets[1].NaturalEnd);
        }

        [Fact]
        public void BuildBuckets_QuarterEdge_03_31_2359_Vs_04_01_0000_AreDifferentBuckets()
        {
            var buckets = ReportBucketBuilder.Build(new DateOnly(2026, 3, 25), new DateOnly(2026, 4, 5), ReportGranularity.Quarter, new DateOnly(2026, 1, 1), Local(2026, 4, 6, 0, 0), Zone);

            var q1 = buckets.Single(b => b.NaturalStart == new DateOnly(2026, 1, 1));
            var q2 = buckets.Single(b => b.NaturalStart == new DateOnly(2026, 4, 1));

            Assert.Equal(new DateOnly(2026, 3, 31), q1.NaturalEnd);
            Assert.Equal(new DateOnly(2026, 6, 30), q2.NaturalEnd);
        }

        // ---- Helpers ----

        private static MetricSetDto BuildWeekMetricSet(List<ReportTaskFacts> facts, long holderId)
        {
            var result = ReportCalculator.BuildMetricSet(
                facts, teamScope: false, holderId: holderId,
                rangeStart: new DateOnly(2026, 3, 2), rangeEnd: new DateOnly(2026, 3, 8),
                measuredStartUtc: Local(2026, 3, 2, 0, 0), measuredEndUtcExclusive: Local(2026, 3, 9, 0, 0),
                timeZone: Zone, includeLoad: false);
            return result;
        }

        private static IEnumerable<long> CycleTimes(List<ReportTaskFacts> facts, long holderId) =>
            facts.Where(f => f.Completion?.HolderId == holderId && f.Completion.CycleTimeSeconds.HasValue)
                .Select(f => f.Completion!.CycleTimeSeconds!.Value);

        private static List<ReportTaskFacts> BuildFixtureFacts()
        {
            return
            [
                ReportCalculator.ExtractFacts(BuildFixtureT1(), Zone),
                ReportCalculator.ExtractFacts(BuildFixtureT2(), Zone),
                ReportCalculator.ExtractFacts(BuildFixtureT3(), Zone),
                ReportCalculator.ExtractFacts(BuildFixtureT6(), Zone),
                ReportCalculator.ExtractFacts(BuildFixtureT7(), Zone),
                ReportCalculator.ExtractFacts(BuildFixtureT8(), Zone),
                ReportCalculator.ExtractFacts(BuildFixtureT4(), Zone),
                ReportCalculator.ExtractFacts(BuildFixtureT5(), Zone),
            ];
        }

        private static ReportTaskInput BuildFixtureT1() => BuildTask(1, M1, TaskWorkflowStatus.Done, new DateOnly(2026, 3, 4),
            H(1, TaskHistoryEvent.Created, Local(2026, 3, 2, 9, 0)),
            H(2, TaskHistoryEvent.StatusChanged, Local(2026, 3, 2, 10, 0), "Todo", "InProgress"),
            H(3, TaskHistoryEvent.StatusChanged, Local(2026, 3, 3, 10, 0), "InProgress", "Review"),
            H(4, TaskHistoryEvent.StatusChanged, Local(2026, 3, 4, 10, 0), "Review", "Done", Mgr));

        private static ReportTaskInput BuildFixtureT2() => BuildTask(2, M1, TaskWorkflowStatus.Done, new DateOnly(2026, 3, 4),
            H(5, TaskHistoryEvent.Created, Local(2026, 3, 2, 8, 0)),
            H(6, TaskHistoryEvent.StatusChanged, Local(2026, 3, 2, 12, 0), "Todo", "InProgress"),
            H(7, TaskHistoryEvent.StatusChanged, Local(2026, 3, 3, 12, 0), "InProgress", "Review"),
            H(8, TaskHistoryEvent.StatusChanged, Local(2026, 3, 3, 15, 0), "Review", "InProgress", M2),
            H(9, TaskHistoryEvent.StatusChanged, Local(2026, 3, 4, 12, 0), "InProgress", "Review"),
            H(10, TaskHistoryEvent.StatusChanged, Local(2026, 3, 5, 9, 0), "Review", "Done", Mgr));

        private static ReportTaskInput BuildFixtureT3() => BuildTask(3, M2, TaskWorkflowStatus.Done, null,
            H(11, TaskHistoryEvent.Created, Local(2026, 3, 1, 9, 0)),
            H(12, TaskHistoryEvent.StatusChanged, Local(2026, 3, 2, 9, 0), "Todo", "InProgress"),
            H(13, TaskHistoryEvent.AssigneeChanged, Local(2026, 3, 3, 9, 0), M1.ToString(), M2.ToString(), M2),
            H(14, TaskHistoryEvent.StatusChanged, Local(2026, 3, 4, 9, 0), "InProgress", "Review"),
            H(15, TaskHistoryEvent.StatusChanged, Local(2026, 3, 4, 18, 0), "Review", "Done", Mgr));

        private static ReportTaskInput BuildFixtureT6() => BuildTask(6, M1, TaskWorkflowStatus.Done, null,
            H(16, TaskHistoryEvent.Created, Local(2026, 3, 3, 8, 0)),
            H(17, TaskHistoryEvent.StatusChanged, Local(2026, 3, 3, 11, 0), "Todo", "InProgress"),
            H(18, TaskHistoryEvent.StatusChanged, Local(2026, 3, 4, 9, 0), "InProgress", "Review"),
            H(19, TaskHistoryEvent.StatusChanged, Local(2026, 3, 5, 9, 0), "Review", "Done", Mgr),
            H(20, TaskHistoryEvent.Reopened, Local(2026, 3, 6, 9, 0), "Done", "Review", Mgr),
            H(21, TaskHistoryEvent.StatusChanged, Local(2026, 3, 6, 10, 0), "Review", "InProgress", Mgr),
            H(22, TaskHistoryEvent.StatusChanged, Local(2026, 3, 7, 9, 0), "InProgress", "Review"),
            H(23, TaskHistoryEvent.StatusChanged, Local(2026, 3, 7, 15, 0), "Review", "Done", Mgr));

        private static ReportTaskInput BuildFixtureT7() => BuildTask(7, M1, TaskWorkflowStatus.Todo, new DateOnly(2026, 3, 3),
            H(24, TaskHistoryEvent.Created, Local(2026, 2, 28, 9, 0)),
            H(25, TaskHistoryEvent.OverdueMarked, Local(2026, 3, 4, 0, 5)));

        private static ReportTaskInput BuildFixtureT8() => BuildTask(8, null, TaskWorkflowStatus.Todo, new DateOnly(2026, 3, 3),
            H(26, TaskHistoryEvent.Created, Local(2026, 2, 28, 9, 0)),
            H(27, TaskHistoryEvent.OverdueMarked, Local(2026, 3, 4, 0, 5)));

        private static ReportTaskInput BuildFixtureT4() => BuildTask(4, M2, TaskWorkflowStatus.Done, null,
            H(28, TaskHistoryEvent.Created, Local(2026, 3, 5, 8, 0)),
            H(29, TaskHistoryEvent.StatusChanged, Local(2026, 3, 6, 8, 0), "Todo", "InProgress"),
            H(30, TaskHistoryEvent.StatusChanged, Local(2026, 3, 8, 8, 0), "InProgress", "Review"),
            H(31, TaskHistoryEvent.StatusChanged, Local(2026, 3, 8, 23, 30), "Review", "Done", Mgr));

        private static ReportTaskInput BuildFixtureT5() => BuildTask(5, M2, TaskWorkflowStatus.Done, null,
            H(32, TaskHistoryEvent.Created, Local(2026, 3, 5, 9, 0)),
            H(33, TaskHistoryEvent.StatusChanged, Local(2026, 3, 6, 9, 0), "Todo", "InProgress"),
            H(34, TaskHistoryEvent.StatusChanged, Local(2026, 3, 8, 9, 0), "InProgress", "Review"),
            H(35, TaskHistoryEvent.StatusChanged, Local(2026, 3, 9, 0, 10), "Review", "Done", Mgr));

        private static ReportTaskInput BuildTask(
            long taskId,
            long? assigneeId,
            TaskWorkflowStatus status,
            DateOnly? dueDate,
            params ReportHistoryEntryInput[] history) => BuildTask(taskId, assigneeId, status, dueDate, isDeleted: false, history);

        private static ReportTaskInput BuildTask(
            long taskId,
            long? assigneeId,
            TaskWorkflowStatus status,
            DateOnly? dueDate,
            bool isDeleted,
            params ReportHistoryEntryInput[] history) => new()
            {
                TaskId = taskId,
                Title = $"Task {taskId}",
                IsDeleted = isDeleted,
                Status = status,
                Priority = TaskPriority.Normal,
                AssigneeId = assigneeId,
                DueDate = dueDate,
                CreatedAt = history[0].CreatedAt,
                History = history,
            };

        private static ReportHistoryEntryInput H(long id, TaskHistoryEvent evt, DateTimeOffset at, string? from = null, string? to = null, long? actor = null) => new()
        {
            Id = id,
            Event = evt,
            CreatedAt = at,
            FromValue = from,
            ToValue = to,
            ActorUserId = actor,
        };

        private static DateTimeOffset Local(int year, int month, int day, int hour, int minute) =>
            new(year, month, day, hour, minute, 0, TimeSpan.FromHours(7));
    }
}
