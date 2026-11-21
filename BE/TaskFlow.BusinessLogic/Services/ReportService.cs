using System.Diagnostics;
using TaskFlow.BusinessLogic.Dtos.Base;
using TaskFlow.BusinessLogic.Dtos.Reports;
using TaskFlow.BusinessLogic.Reports;
using TaskFlow.Common.Constants;
using TaskFlow.Common.Constants.Messages;
using TaskFlow.Common.Enums;
using TaskFlow.Common.Helpers;
using TaskFlow.DataAccess.Entities;
using TaskFlow.Repositories.UoW;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace TaskFlow.BusinessLogic.Services
{
    public interface IReportService
    {
        Task<(string ErrorCode, ReportScorecardResDto? Result)> GetScorecard(long? userId, DateOnly from, DateOnly to, ReportGranularity granularity, long actorUserId, string[] callerPermissions);

        Task<(string ErrorCode, ReportTeamResDto? Result)> GetTeam(DateOnly from, DateOnly to);

        Task<(string ErrorCode, Pagination<ReportTaskResDto>? Page)> GetTasks(long? userId, DateOnly from, DateOnly to, ReportTaskMetric metric, int start, int length, long actorUserId, string[] callerPermissions);

        Task<(string ErrorCode, byte[]? Content, string? FileName)> Export(long? userId, DateOnly from, DateOnly to, ReportGranularity granularity, ReportExportDataset dataset, long actorUserId, string[] callerPermissions);
    }

    // f09-reports.md "Technical contract": everything computed on read from
    // Tasks + TaskHistories (ADR 0008 decision 1). No write path, no
    // tracking (R17). Validation order: range -> scope -> user -> compute.
    public class ReportService(IConfiguration configuration, IUnitOfWork unitOfWork, ILogger<ReportService> logger) : IReportService
    {
        private readonly string _timeZone = configuration.GetSection(ConfigKeys.Workflow.TimeZone).Get<string>()!;
        private readonly int _limit = configuration.GetSection(ConfigKeys.Workflow.MaxInProgressPerAssignee).Get<int>();

        public async Task<(string ErrorCode, ReportScorecardResDto? Result)> GetScorecard(long? userId, DateOnly from, DateOnly to, ReportGranularity granularity, long actorUserId, string[] callerPermissions)
        {
            var stopwatch = Stopwatch.StartNew();

            var rangeError = ValidateRange(from, to, granularity, ReportControllerMS.GetScorecard.InvalidRange, ReportControllerMS.GetScorecard.RangeInFuture, ReportControllerMS.GetScorecard.RangeTooLarge, ReportControllerMS.GetScorecard.TooManyBuckets);
            if (!string.IsNullOrEmpty(rangeError))
            {
                return (rangeError, null);
            }

            var hasViewReports = callerPermissions.Contains(Permissions.Reports.ViewReports);
            if (IsSelfOnlyViolation(userId, actorUserId, hasViewReports))
            {
                return (ReportControllerMS.GetScorecard.SelfOnly, null);
            }

            User? subjectUser = null;
            if (userId.HasValue)
            {
                subjectUser = await unitOfWork.GetRepository<User>().FindById(userId.Value);
                if (subjectUser == null)
                {
                    return (ReportControllerMS.GetScorecard.UserNotFound, null);
                }
            }

            var teamScope = !userId.HasValue;
            var (facts, dataAvailableSince) = await LoadFactsAsync(from, to);
            var now = DateTimeHelper.GetDtOffset();
            var buckets = ReportBucketBuilder.Build(from, to, granularity, dataAvailableSince, now, _timeZone);

            var totalsStartUtc = buckets.Count > 0 ? buckets[0].MeasuredStartUtc : from.LocalStartOfDayToUtc(_timeZone);
            var totalsEndUtc = buckets.Count > 0 ? buckets[^1].MeasuredEndUtcExclusive : to.AddDays(1).LocalStartOfDayToUtc(_timeZone);
            var anyDataInRange = buckets.Any(b => b.HasData);

            var totals = ReportCalculator.BuildMetricSet(facts, teamScope, userId, from, to, totalsStartUtc, totalsEndUtc, _limit, _timeZone, includeLoad: anyDataInRange);

            var bucketDtos = buckets.Select(b => new ReportBucketDto
            {
                Start = b.Start,
                End = b.End,
                IsPartial = b.IsPartial,
                HasData = b.HasData,
                Metrics = b.HasData
                    ? ReportCalculator.BuildMetricSet(facts, teamScope, userId, b.NaturalStart, b.NaturalEnd, b.MeasuredStartUtc, b.MeasuredEndUtcExclusive, _limit, _timeZone, includeLoad: true)
                    : null,
            }).ToList();

            ReportTeamMedianDto? teamMedian = null;
            if (!teamScope && hasViewReports)
            {
                teamMedian = await BuildTeamMedian(facts, from, to, totalsStartUtc, totalsEndUtc);
            }

            var result = new ReportScorecardResDto
            {
                Meta = BuildMeta(teamScope ? null : userId, from, to, granularity, dataAvailableSince, teamScope ? "Team" : "Member"),
                Subject = subjectUser != null ? BuildUserRef(subjectUser) : null,
                Totals = totals,
                TeamMedian = teamMedian,
                Buckets = bucketDtos,
            };

            LogCall("GetScorecard", actorUserId, teamScope ? "team" : userId!.Value.ToString(), from, to, granularity, stopwatch);
            return (string.Empty, result);
        }

        public async Task<(string ErrorCode, ReportTeamResDto? Result)> GetTeam(DateOnly from, DateOnly to)
        {
            var stopwatch = Stopwatch.StartNew();

            var rangeError = ValidateRange(from, to, ReportGranularity.Week, ReportControllerMS.GetTeam.InvalidRange, ReportControllerMS.GetTeam.RangeInFuture, ReportControllerMS.GetTeam.RangeTooLarge, null);
            if (!string.IsNullOrEmpty(rangeError))
            {
                return (rangeError, null);
            }

            var (facts, dataAvailableSince) = await LoadFactsAsync(from, to);
            var now = DateTimeHelper.GetDtOffset();
            var buckets = ReportBucketBuilder.Build(from, to, ReportGranularity.Week, dataAvailableSince, now, _timeZone);

            var totalsStartUtc = buckets.Count > 0 ? buckets[0].MeasuredStartUtc : from.LocalStartOfDayToUtc(_timeZone);
            var totalsEndUtc = buckets.Count > 0 ? buckets[^1].MeasuredEndUtcExclusive : to.AddDays(1).LocalStartOfDayToUtc(_timeZone);
            var anyDataInRange = buckets.Any(b => b.HasData);

            var teamTotals = ReportCalculator.BuildMetricSet(facts, teamScope: true, holderId: null, from, to, totalsStartUtc, totalsEndUtc, _limit, _timeZone, includeLoad: anyDataInRange);

            var roster = await BuildRosterAsync(facts, from, to);

            var members = roster
                .Select(u => new ReportTeamMemberDto
                {
                    User = BuildUserRef(u),
                    Totals = ReportCalculator.BuildMetricSet(facts, teamScope: false, holderId: u.Id, from, to, totalsStartUtc, totalsEndUtc, _limit, _timeZone, includeLoad: anyDataInRange),
                })
                .OrderBy(m => m.User.DisplayName ?? m.User.UserName, StringComparer.OrdinalIgnoreCase)
                .ThenBy(m => m.User.Id)
                .ToList();

            var unassignedOverdueMarked = facts.SelectMany(f => f.OverdueMarks)
                .Count(o => o.HolderId == null && InLocalRange(o.AtUtc, from, to));

            var teamMedian = BuildTeamMedianFromMembers(members);

            var heatmapWeeks = ReportBucketBuilder.Build(from, to, ReportGranularity.Week, dataAvailableSince, now, _timeZone);
            var heatmap = new ReportHeatmapDto
            {
                Weeks = heatmapWeeks.Select(w => new ReportBucketDto { Start = w.Start, End = w.End, IsPartial = w.IsPartial, HasData = w.HasData }).ToList(),
                Rows = members.Select(m => new ReportHeatmapRowDto
                {
                    UserId = m.User.Id,
                    AvgInProgress = heatmapWeeks.Select(w => w.HasData
                        ? (double?)ReportCalculator.BuildLoad(facts, teamScope: false, m.User.Id, w.MeasuredStartUtc, w.MeasuredEndUtcExclusive, _limit).AvgInProgress
                        : null).ToList(),
                }).ToList(),
            };

            var result = new ReportTeamResDto
            {
                Meta = BuildMeta(null, from, to, ReportGranularity.Week, dataAvailableSince, "Team"),
                TeamTotals = teamTotals,
                Contributors = members.Count(m => m.Totals.Delivery.Done >= 1),
                UnassignedOverdueMarked = unassignedOverdueMarked,
                TeamMedian = teamMedian,
                Members = members,
                Heatmap = heatmap,
            };

            LogCall("GetTeam", null, "team", from, to, ReportGranularity.Week, stopwatch);
            return (string.Empty, result);
        }

        public async Task<(string ErrorCode, Pagination<ReportTaskResDto>? Page)> GetTasks(long? userId, DateOnly from, DateOnly to, ReportTaskMetric metric, int start, int length, long actorUserId, string[] callerPermissions)
        {
            var rangeError = ValidateRange(from, to, ReportGranularity.Day, ReportControllerMS.GetTasks.InvalidRange, ReportControllerMS.GetTasks.RangeInFuture, ReportControllerMS.GetTasks.RangeTooLarge, null, enforceDayLimit: false);
            if (!string.IsNullOrEmpty(rangeError))
            {
                return (rangeError, null);
            }

            var hasViewReports = callerPermissions.Contains(Permissions.Reports.ViewReports);
            if (IsSelfOnlyViolation(userId, actorUserId, hasViewReports))
            {
                return (ReportControllerMS.GetTasks.SelfOnly, null);
            }

            if (userId.HasValue)
            {
                var subjectUser = await unitOfWork.GetRepository<User>().FindById(userId.Value);
                if (subjectUser == null)
                {
                    return (ReportControllerMS.GetTasks.UserNotFound, null);
                }
            }

            var (facts, _) = await LoadFactsAsync(from, to);
            var rows = BuildDrillDownRows(facts, userId, from, to, metric);

            var ordered = rows.OrderByDescending(r => r.OccurredAt).ThenByDescending(r => r.TaskId).ToList();

            var userIdsToResolve = ordered.Select(r => r.CreditedUserId).Where(id => id.HasValue).Select(id => id!.Value).Distinct().ToList();
            await ResolveUserNamesAsync(ordered, userIdsToResolve);

            var page = new Pagination<ReportTaskResDto>(ordered.AsEnumerable(), start, length);
            return (string.Empty, page);
        }

        public async Task<(string ErrorCode, byte[]? Content, string? FileName)> Export(long? userId, DateOnly from, DateOnly to, ReportGranularity granularity, ReportExportDataset dataset, long actorUserId, string[] callerPermissions)
        {
            var stopwatch = Stopwatch.StartNew();

            var rangeError = ValidateRange(from, to, granularity, ReportControllerMS.Export.InvalidRange, ReportControllerMS.Export.RangeInFuture, ReportControllerMS.Export.RangeTooLarge, ReportControllerMS.Export.TooManyBuckets);
            if (!string.IsNullOrEmpty(rangeError))
            {
                return (rangeError, null, null);
            }

            var hasViewReports = callerPermissions.Contains(Permissions.Reports.ViewReports);
            if (IsSelfOnlyViolation(userId, actorUserId, hasViewReports))
            {
                return (ReportControllerMS.Export.SelfOnly, null, null);
            }

            User? subjectUser = null;
            if (userId.HasValue)
            {
                subjectUser = await unitOfWork.GetRepository<User>().FindById(userId.Value);
                if (subjectUser == null)
                {
                    return (ReportControllerMS.Export.UserNotFound, null, null);
                }
            }

            if (dataset == ReportExportDataset.Members && userId.HasValue)
            {
                return (ReportControllerMS.Export.InvalidDataset, null, null);
            }

            var teamScope = !userId.HasValue;
            var (facts, dataAvailableSince) = await LoadFactsAsync(from, to);
            var now = DateTimeHelper.GetDtOffset();
            var buckets = ReportBucketBuilder.Build(from, to, granularity, dataAvailableSince, now, _timeZone);

            List<string[]> rows;
            string[] header;

            switch (dataset)
            {
                case ReportExportDataset.Buckets:
                    header = BucketsCsvHeader;
                    rows = buckets.Select(b => BuildBucketRow(b, facts, teamScope, userId)).ToList();
                    break;
                case ReportExportDataset.Members:
                    header = MembersCsvHeader;
                    var roster = await BuildRosterAsync(facts, from, to);
                    var totalsStartUtc = buckets.Count > 0 ? buckets[0].MeasuredStartUtc : from.LocalStartOfDayToUtc(_timeZone);
                    var totalsEndUtc = buckets.Count > 0 ? buckets[^1].MeasuredEndUtcExclusive : to.AddDays(1).LocalStartOfDayToUtc(_timeZone);
                    rows = roster
                        .OrderBy(u => u.DisplayName ?? u.UserName, StringComparer.OrdinalIgnoreCase)
                        .ThenBy(u => u.Id)
                        .Select(u => BuildMemberRow(u, facts, from, to, totalsStartUtc, totalsEndUtc))
                        .ToList();
                    break;
                case ReportExportDataset.Tasks:
                    header = TasksCsvHeader;
                    var completions = facts
                        .Where(f => f.Completion != null && (teamScope || f.Completion!.HolderId == userId) && InLocalRange(f.Completion!.CompletedAtUtc, from, to))
                        .OrderByDescending(f => f.Completion!.CompletedAtUtc)
                        .ThenByDescending(f => f.TaskId)
                        .ToList();
                    var creditedIds = completions.Select(f => f.Completion!.HolderId).Where(id => id.HasValue).Select(id => id!.Value).Distinct().ToList();
                    var names = await ResolveUserNamesAsync(creditedIds);
                    rows = completions.Select(f => BuildTaskRow(f, names)).ToList();
                    break;
                default:
                    return (ReportControllerMS.Export.InvalidDataset, null, null);
            }

            if (rows.Count > ReportRules.ExportMaxRows)
            {
                return (ReportControllerMS.Export.TooManyRows, null, null);
            }

            var csvBytes = CsvBuilder.Build(header, rows);
            var subjectName = teamScope ? "team" : (subjectUser?.UserName ?? userId!.Value.ToString());
            var fileName = $"taskflow-report-{Sanitize(subjectName)}-{from:yyyy-MM-dd}-{to:yyyy-MM-dd}-{dataset.ToString().ToLowerInvariant()}.csv";

            // R17: export logs the subject/range/dataset/row count, never content.
            logger.LogInformation(
                "Report export by {CallerId} subject={Subject} from={From} to={To} dataset={Dataset} rows={RowCount}",
                actorUserId, teamScope ? "team" : userId!.Value.ToString(), from, to, dataset, rows.Count);

            stopwatch.Stop();
            if (stopwatch.ElapsedMilliseconds > 2000)
            {
                logger.LogWarning("Report export took {ElapsedMs} ms", stopwatch.ElapsedMilliseconds);
            }

            return (string.Empty, csvBytes, fileName);
        }

        // ---- Validation ----

        private string ValidateRange(DateOnly from, DateOnly to, ReportGranularity granularity, string invalidRangeCode, string rangeInFutureCode, string rangeTooLargeCode, string? tooManyBucketsCode, bool enforceDayLimit = true)
        {
            if (from > to)
            {
                return invalidRangeCode;
            }

            var today = DateTimeHelper.GetDtOffset().ToDateInZone(_timeZone);
            if (to > today)
            {
                return rangeInFutureCode;
            }

            var days = to.DayNumber - from.DayNumber + 1;
            if (days > ReportRules.MaxRangeDays)
            {
                return rangeTooLargeCode;
            }

            if (enforceDayLimit && granularity == ReportGranularity.Day && days > ReportRules.MaxDayBuckets && tooManyBucketsCode != null)
            {
                return tooManyBucketsCode;
            }

            return string.Empty;
        }

        private static bool IsSelfOnlyViolation(long? userId, long actorUserId, bool hasViewReports)
        {
            if (hasViewReports)
            {
                return false;
            }

            return !userId.HasValue || userId.Value != actorUserId;
        }

        // ---- Queries (Technical contract: at most 4 round trips + chunking) ----

        private async Task<(List<ReportTaskFacts> Facts, DateOnly? DataAvailableSince)> LoadFactsAsync(DateOnly from, DateOnly to)
        {
            // Query 1.
            var earliest = await unitOfWork.DbContext.Set<TaskHistory>().AsNoTracking()
                .OrderBy(h => h.CreatedAt)
                .Select(h => (DateTimeOffset?)h.CreatedAt)
                .FirstOrDefaultAsync();
            var dataAvailableSince = earliest?.ToDateInZone(_timeZone);

            var fromUtc = from.LocalStartOfDayToUtc(_timeZone);
            var toUtcExclusive = to.AddDays(1).LocalStartOfDayToUtc(_timeZone);

            // Query 2.
            var candidateTasks = await unitOfWork.DbContext.Set<TaskItem>()
                .IgnoreQueryFilters()
                .AsNoTracking()
                .Where(t => t.CreatedAt < toUtcExclusive
                    && !(t.Status == TaskWorkflowStatus.Done && t.CompletedAt < fromUtc)
                    && !(t.IsDeleted && t.UpdatedAt < fromUtc))
                .Select(t => new { t.Id, t.Title, t.Status, t.Priority, t.AssigneeId, t.DueDate, t.IsDeleted, t.CreatedAt })
                .ToListAsync();

            var historiesByTask = new Dictionary<long, List<ReportHistoryEntryInput>>();

            // Query 3, chunked (<= 500 ids per round trip).
            foreach (var chunk in candidateTasks.Select(t => t.Id).Chunk(500))
            {
                var chunkIds = chunk;
                var rows = await unitOfWork.DbContext.Set<TaskHistory>()
                    .AsNoTracking()
                    .Where(h => chunkIds.Contains(h.TaskId))
                    .OrderBy(h => h.TaskId).ThenBy(h => h.CreatedAt).ThenBy(h => h.Id)
                    .Select(h => new { h.TaskId, h.Id, h.Event, h.ActorUserId, h.FromValue, h.ToValue, h.CreatedAt })
                    .ToListAsync();

                foreach (var row in rows)
                {
                    if (!historiesByTask.TryGetValue(row.TaskId, out var list))
                    {
                        list = [];
                        historiesByTask[row.TaskId] = list;
                    }

                    list.Add(new ReportHistoryEntryInput
                    {
                        Id = row.Id,
                        Event = row.Event,
                        ActorUserId = row.ActorUserId,
                        FromValue = row.FromValue,
                        ToValue = row.ToValue,
                        CreatedAt = row.CreatedAt,
                    });
                }
            }

            var facts = candidateTasks.Select(t => ReportCalculator.ExtractFacts(new ReportTaskInput
            {
                TaskId = t.Id,
                Title = t.Title,
                IsDeleted = t.IsDeleted,
                Status = t.Status,
                Priority = t.Priority,
                AssigneeId = t.AssigneeId,
                DueDate = t.DueDate,
                CreatedAt = t.CreatedAt,
                History = historiesByTask.TryGetValue(t.Id, out var h) ? h : [],
            }, _timeZone)).ToList();

            return (facts, dataAvailableSince);
        }

        // R11 roster: every active user, plus any inactive or deleted user
        // with activity in range - resolved by name with IgnoreQueryFilters().
        private async Task<List<User>> BuildRosterAsync(List<ReportTaskFacts> facts, DateOnly from, DateOnly to)
        {
            var activeUsers = await unitOfWork.DbContext.Set<User>().AsNoTracking()
                .Where(u => u.DeactivatedAt == null)
                .ToListAsync();

            var activityIds = CollectHolderIdsInRange(facts, from, to, _timeZone);
            var activeIds = activeUsers.Select(u => u.Id).ToHashSet();
            var missingIds = activityIds.Where(id => !activeIds.Contains(id)).ToList();

            var others = missingIds.Count > 0
                ? await unitOfWork.DbContext.Set<User>().IgnoreQueryFilters().AsNoTracking().Where(u => missingIds.Contains(u.Id)).ToListAsync()
                : [];

            return [.. activeUsers, .. others];
        }

        private static HashSet<long> CollectHolderIdsInRange(List<ReportTaskFacts> facts, DateOnly from, DateOnly to, string timeZone)
        {
            bool InRange(DateTimeOffset at) => at.ToDateInZone(timeZone) is var d && d >= from && d <= to;

            var ids = new HashSet<long>();

            foreach (var f in facts)
            {
                if (f.Completion?.HolderId != null && InRange(f.Completion.CompletedAtUtc))
                {
                    ids.Add(f.Completion.HolderId.Value);
                }

                foreach (var s in f.Submissions)
                {
                    if (s.HolderId.HasValue && InRange(s.SubmittedAtUtc))
                    {
                        ids.Add(s.HolderId.Value);
                    }
                }

                foreach (var r in f.Reopens)
                {
                    if (r.HolderId.HasValue && InRange(r.AtUtc))
                    {
                        ids.Add(r.HolderId.Value);
                    }
                }

                foreach (var o in f.OverdueMarks)
                {
                    if (o.HolderId.HasValue && InRange(o.AtUtc))
                    {
                        ids.Add(o.HolderId.Value);
                    }
                }

                foreach (var t in f.Takeovers)
                {
                    if (InRange(t.AtUtc))
                    {
                        ids.Add(t.ToUserId);
                        ids.Add(t.FromUserId);
                    }
                }
            }

            return ids;
        }

        private bool InLocalRange(DateTimeOffset at, DateOnly from, DateOnly to)
        {
            var date = at.ToDateInZone(_timeZone);
            return date >= from && date <= to;
        }

        // ---- Team median (R12) ----

        private async Task<ReportTeamMedianDto> BuildTeamMedian(List<ReportTaskFacts> facts, DateOnly from, DateOnly to, DateTimeOffset startUtc, DateTimeOffset endUtc)
        {
            var roster = await BuildRosterAsync(facts, from, to);
            var members = roster.Select(u => ReportCalculator.BuildMetricSet(facts, teamScope: false, u.Id, from, to, startUtc, endUtc, _limit, _timeZone, includeLoad: true)).ToList();
            return BuildTeamMedianFromMetricSets(members);
        }

        private static ReportTeamMedianDto BuildTeamMedianFromMembers(List<ReportTeamMemberDto> members) =>
            BuildTeamMedianFromMetricSets(members.Select(m => m.Totals).ToList());

        private static ReportTeamMedianDto BuildTeamMedianFromMetricSets(List<MetricSetDto> members)
        {
            double? NonZeroMedian(IEnumerable<double> values)
            {
                var list = values.ToList();
                return list.Count == 0 ? null : ReportStatistics.MedianOfDoubles(list);
            }

            return new ReportTeamMedianDto
            {
                // R12 (corrected 2026-11-21, AC22): contributors counts
                // roster members with at least one counted completion in
                // range (done >= 1) - never the roster size. A member with
                // only a take-over or InProgress time but no completion
                // does not count.
                Contributors = members.Count(m => m.Delivery.Done >= 1),
                Done = NonZeroMedian(members.Select(m => (double)m.Delivery.Done)),
                CycleTimeMedianSeconds = ReportStatistics.Median(members.Where(m => m.Speed.CycleTime.MedianSeconds.HasValue).Select(m => m.Speed.CycleTime.MedianSeconds!.Value).ToList()),
                LeadTimeMedianSeconds = ReportStatistics.Median(members.Where(m => m.Speed.LeadTime.MedianSeconds.HasValue).Select(m => m.Speed.LeadTime.MedianSeconds!.Value).ToList()),
                ReviewWaitMedianSeconds = ReportStatistics.Median(members.Where(m => m.Speed.ReviewWait.MedianSeconds.HasValue).Select(m => m.Speed.ReviewWait.MedianSeconds!.Value).ToList()),
                OnTimeRate = NonZeroMedian(members.Where(m => m.Reliability.OnTimeRate.Value.HasValue).Select(m => m.Reliability.OnTimeRate.Value!.Value)),
                OverdueMarked = NonZeroMedian(members.Select(m => (double)m.Reliability.OverdueMarked)),
                ReworkRate = NonZeroMedian(members.Where(m => m.Quality.ReworkRate.Value.HasValue).Select(m => m.Quality.ReworkRate.Value!.Value)),
                AvgInProgress = NonZeroMedian(members.Where(m => m.Load != null).Select(m => m.Load!.AvgInProgress)),
                CapacityUtilization = NonZeroMedian(members.Where(m => m.Load != null).Select(m => m.Load!.CapacityUtilization)),
            };
        }

        // ---- Drill-down (reports/tasks) ----

        private List<ReportTaskResDto> BuildDrillDownRows(List<ReportTaskFacts> facts, long? userId, DateOnly from, DateOnly to, ReportTaskMetric metric)
        {
            bool Matches(long? holder) => !userId.HasValue || holder == userId.Value;

            var rows = new List<ReportTaskResDto>();

            switch (metric)
            {
                case ReportTaskMetric.Done:
                case ReportTaskMetric.OnTime:
                case ReportTaskMetric.Late:
                case ReportTaskMetric.NoDueDate:
                    var wanted = metric switch
                    {
                        ReportTaskMetric.OnTime => ReportOnTimeStatus.OnTime,
                        ReportTaskMetric.Late => ReportOnTimeStatus.Late,
                        ReportTaskMetric.NoDueDate => ReportOnTimeStatus.NoDueDate,
                        _ => (ReportOnTimeStatus?)null,
                    };

                    rows.AddRange(facts
                        .Where(f => f.Completion != null && Matches(f.Completion!.HolderId) && InLocalRange(f.Completion!.CompletedAtUtc, from, to)
                            && (wanted == null || f.Completion!.OnTimeStatus == wanted))
                        .Select(f => new ReportTaskResDto
                        {
                            TaskId = f.TaskId,
                            Title = f.Title,
                            IsDeleted = f.IsDeleted,
                            Metric = metric,
                            CreditedUserId = f.Completion!.HolderId,
                            OccurredAt = f.Completion.CompletedAtUtc,
                            OccurredAtLocal = f.Completion.CompletedAtUtc.ToTimezone(_timeZone),
                            Priority = f.Completion.Priority,
                            DueDate = f.Completion.DueDate,
                            CycleTimeSeconds = f.Completion.CycleTimeSeconds,
                            LeadTimeSeconds = f.Completion.LeadTimeSeconds,
                        }));
                    break;

                case ReportTaskMetric.SentBack:
                    rows.AddRange(facts.SelectMany(f => f.Submissions
                            .Where(s => s.Outcome == ReportSubmissionOutcome.SentBack && Matches(s.HolderId) && InLocalRange(s.SubmittedAtUtc, from, to))
                            .Select(s => new ReportTaskResDto
                            {
                                TaskId = f.TaskId,
                                Title = f.Title,
                                IsDeleted = f.IsDeleted,
                                Metric = metric,
                                CreditedUserId = s.HolderId,
                                OccurredAt = s.SubmittedAtUtc,
                                OccurredAtLocal = s.SubmittedAtUtc.ToTimezone(_timeZone),
                            })));
                    break;

                case ReportTaskMetric.Reopened:
                    rows.AddRange(facts.SelectMany(f => f.Reopens
                            .Where(r => Matches(r.HolderId) && InLocalRange(r.AtUtc, from, to))
                            .Select(r => new ReportTaskResDto
                            {
                                TaskId = f.TaskId,
                                Title = f.Title,
                                IsDeleted = f.IsDeleted,
                                Metric = metric,
                                CreditedUserId = r.HolderId,
                                OccurredAt = r.AtUtc,
                                OccurredAtLocal = r.AtUtc.ToTimezone(_timeZone),
                            })));
                    break;

                case ReportTaskMetric.OverdueMarked:
                    rows.AddRange(facts.SelectMany(f => f.OverdueMarks
                            .Where(o => Matches(o.HolderId) && InLocalRange(o.AtUtc, from, to))
                            .Select(o => new ReportTaskResDto
                            {
                                TaskId = f.TaskId,
                                Title = f.Title,
                                IsDeleted = f.IsDeleted,
                                Metric = metric,
                                CreditedUserId = o.HolderId,
                                OccurredAt = o.AtUtc,
                                OccurredAtLocal = o.AtUtc.ToTimezone(_timeZone),
                            })));
                    break;

                case ReportTaskMetric.TakeOver:
                    rows.AddRange(facts.SelectMany(f => f.Takeovers
                            .Where(t => (!userId.HasValue || t.ToUserId == userId.Value) && InLocalRange(t.AtUtc, from, to))
                            .Select(t => new ReportTaskResDto
                            {
                                TaskId = f.TaskId,
                                Title = f.Title,
                                IsDeleted = f.IsDeleted,
                                Metric = metric,
                                CreditedUserId = t.ToUserId,
                                OccurredAt = t.AtUtc,
                                OccurredAtLocal = t.AtUtc.ToTimezone(_timeZone),
                            })));
                    break;
            }

            return rows;
        }

        private async Task ResolveUserNamesAsync(List<ReportTaskResDto> rows, List<long> userIds)
        {
            var names = await ResolveUserNamesAsync(userIds);
            foreach (var row in rows)
            {
                if (row.CreditedUserId.HasValue && names.TryGetValue(row.CreditedUserId.Value, out var user))
                {
                    row.CreditedUserName = user.UserName;
                    row.CreditedDisplayName = user.DisplayName;
                }
            }
        }

        private async Task<Dictionary<long, (string? UserName, string? DisplayName)>> ResolveUserNamesAsync(List<long> userIds)
        {
            if (userIds.Count == 0)
            {
                return [];
            }

            var users = await unitOfWork.DbContext.Set<User>().IgnoreQueryFilters().AsNoTracking()
                .Where(u => userIds.Contains(u.Id))
                .Select(u => new { u.Id, u.UserName, u.DisplayName })
                .ToListAsync();

            return users.ToDictionary(u => u.Id, u => ((string?)u.UserName, (string?)u.DisplayName));
        }

        // ---- Shaping helpers ----

        private ReportMetaDto BuildMeta(long? userId, DateOnly from, DateOnly to, ReportGranularity granularity, DateOnly? dataAvailableSince, string scope) => new()
        {
            Scope = scope,
            UserId = userId,
            From = from,
            To = to,
            Granularity = granularity,
            TimeZone = _timeZone,
            DataAvailableSince = dataAvailableSince,
            GeneratedAt = DateTimeHelper.GetDtOffset(),
            SmallSampleThreshold = ReportRules.SmallSampleThreshold,
            InProgressLimit = _limit,
        };

        private static ReportUserRefDto BuildUserRef(User user) => new()
        {
            Id = user.Id,
            UserName = user.UserName ?? string.Empty,
            DisplayName = user.DisplayName,
            AvatarUrl = AvatarKeyHelper.BuildUrl(user.Id, user.AvatarObjectKey),
            IsActive = user.DeactivatedAt == null,
        };

        private void LogCall(string operation, long? callerId, string subject, DateOnly from, DateOnly to, ReportGranularity granularity, Stopwatch stopwatch)
        {
            stopwatch.Stop();
            logger.LogInformation(
                "Report call {Operation} caller={CallerId} scope={Subject} from={From} to={To} granularity={Granularity} elapsedMs={ElapsedMs}",
                operation, callerId, subject, from, to, granularity, stopwatch.ElapsedMilliseconds);

            if (stopwatch.ElapsedMilliseconds > 2000)
            {
                logger.LogWarning("Report call {Operation} took {ElapsedMs} ms", operation, stopwatch.ElapsedMilliseconds);
            }
        }

        // ---- CSV ----

        private static readonly string[] BucketsCsvHeader =
        [
            "bucket_start", "bucket_end", "is_partial", "done", "done_urgent", "done_high", "done_normal",
            "on_time", "late", "no_due_date", "on_time_rate", "overdue_marked",
            "cycle_median_h", "cycle_p85_h", "lead_median_h", "lead_p85_h", "review_wait_median_h", "review_wait_p85_h",
            "submissions_decided", "sent_back", "rework_rate", "reopened_after_done",
            "avg_in_progress", "capacity_utilization", "take_overs", "taken_from", "low_sample",
        ];

        private static readonly string[] MembersCsvHeader =
        [
            "user_name", "display_name", "is_active", "done", "done_urgent", "done_high", "done_normal",
            "on_time", "late", "no_due_date", "on_time_rate", "overdue_marked",
            "cycle_median_h", "cycle_p85_h", "lead_median_h", "lead_p85_h", "review_wait_median_h", "review_wait_p85_h",
            "submissions_decided", "sent_back", "rework_rate", "reopened_after_done",
            "avg_in_progress", "capacity_utilization", "take_overs", "taken_from",
        ];

        private static readonly string[] TasksCsvHeader =
        [
            "task_id", "title", "credited_user", "completed_local", "priority", "due_date", "on_time", "cycle_h", "lead_h",
        ];

        private string[] BuildBucketRow(ReportBucket bucket, List<ReportTaskFacts> facts, bool teamScope, long? userId)
        {
            if (!bucket.HasData)
            {
                return [bucket.Start.ToString("yyyy-MM-dd"), bucket.End.ToString("yyyy-MM-dd"), bucket.IsPartial.ToString(), .. Enumerable.Repeat(string.Empty, BucketsCsvHeader.Length - 3)];
            }

            var m = ReportCalculator.BuildMetricSet(facts, teamScope, userId, bucket.NaturalStart, bucket.NaturalEnd, bucket.MeasuredStartUtc, bucket.MeasuredEndUtcExclusive, _limit, _timeZone, includeLoad: true);
            var lowSample = m.Speed.CycleTime.LowSample || m.Reliability.OnTimeRate.LowSample || m.Quality.ReworkRate.LowSample;

            return
            [
                bucket.Start.ToString("yyyy-MM-dd"), bucket.End.ToString("yyyy-MM-dd"), bucket.IsPartial.ToString(),
                m.Delivery.Done.ToString(), m.Delivery.Urgent.ToString(), m.Delivery.High.ToString(), m.Delivery.Normal.ToString(),
                m.Reliability.OnTime.ToString(), m.Reliability.Late.ToString(), m.Reliability.NoDueDate.ToString(), FormatRate(m.Reliability.OnTimeRate), m.Reliability.OverdueMarked.ToString(),
                FormatHours(m.Speed.CycleTime.MedianSeconds), FormatHours(m.Speed.CycleTime.P85Seconds), FormatHours(m.Speed.LeadTime.MedianSeconds), FormatHours(m.Speed.LeadTime.P85Seconds), FormatHours(m.Speed.ReviewWait.MedianSeconds), FormatHours(m.Speed.ReviewWait.P85Seconds),
                m.Quality.SubmissionsDecided.ToString(), m.Quality.SentBack.ToString(), FormatRate(m.Quality.ReworkRate), m.Quality.ReopenedAfterDone.ToString(),
                m.Load?.AvgInProgress.ToString("0.00") ?? string.Empty, m.Load?.CapacityUtilization.ToString("0.0000") ?? string.Empty,
                m.Attribution.TakeOvers.ToString(), m.Attribution.TakenFrom.ToString(), lowSample.ToString(),
            ];
        }

        private string[] BuildMemberRow(User user, List<ReportTaskFacts> facts, DateOnly from, DateOnly to, DateTimeOffset startUtc, DateTimeOffset endUtc)
        {
            var m = ReportCalculator.BuildMetricSet(facts, teamScope: false, user.Id, from, to, startUtc, endUtc, _limit, _timeZone, includeLoad: true);

            return
            [
                user.UserName ?? string.Empty, user.DisplayName ?? string.Empty, (user.DeactivatedAt == null).ToString(),
                m.Delivery.Done.ToString(), m.Delivery.Urgent.ToString(), m.Delivery.High.ToString(), m.Delivery.Normal.ToString(),
                m.Reliability.OnTime.ToString(), m.Reliability.Late.ToString(), m.Reliability.NoDueDate.ToString(), FormatRate(m.Reliability.OnTimeRate), m.Reliability.OverdueMarked.ToString(),
                FormatHours(m.Speed.CycleTime.MedianSeconds), FormatHours(m.Speed.CycleTime.P85Seconds), FormatHours(m.Speed.LeadTime.MedianSeconds), FormatHours(m.Speed.LeadTime.P85Seconds), FormatHours(m.Speed.ReviewWait.MedianSeconds), FormatHours(m.Speed.ReviewWait.P85Seconds),
                m.Quality.SubmissionsDecided.ToString(), m.Quality.SentBack.ToString(), FormatRate(m.Quality.ReworkRate), m.Quality.ReopenedAfterDone.ToString(),
                m.Load?.AvgInProgress.ToString("0.00") ?? string.Empty, m.Load?.CapacityUtilization.ToString("0.0000") ?? string.Empty,
                m.Attribution.TakeOvers.ToString(), m.Attribution.TakenFrom.ToString(),
            ];
        }

        private string[] BuildTaskRow(ReportTaskFacts fact, Dictionary<long, (string? UserName, string? DisplayName)> names)
        {
            var completion = fact.Completion!;
            var creditedName = completion.HolderId.HasValue && names.TryGetValue(completion.HolderId.Value, out var n) ? (n.DisplayName ?? n.UserName ?? completion.HolderId.Value.ToString()) : string.Empty;

            return
            [
                fact.TaskId.ToString(), fact.Title, creditedName,
                completion.CompletedAtUtc.ToTimezone(_timeZone).ToString("yyyy-MM-dd HH:mm"),
                completion.Priority.ToString(), completion.DueDate?.ToString("yyyy-MM-dd") ?? string.Empty,
                (completion.OnTimeStatus == ReportOnTimeStatus.OnTime).ToString(),
                FormatHours(completion.CycleTimeSeconds), FormatHours(completion.LeadTimeSeconds),
            ];
        }

        private static string FormatHours(long? seconds) => seconds.HasValue ? Math.Round(seconds.Value / 3600.0, 1).ToString("0.0") : string.Empty;

        private static string FormatRate(ReportRateDto rate) => rate.Value?.ToString("0.0000") ?? string.Empty;

        private static string Sanitize(string value)
        {
            var chars = value.Select(c => char.IsLetterOrDigit(c) || c is '.' or '_' or '-' ? c : '-').ToArray();
            return new string(chars);
        }
    }
}
