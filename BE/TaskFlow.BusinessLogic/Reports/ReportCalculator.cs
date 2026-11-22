using System.Globalization;
using TaskFlow.BusinessLogic.Dtos.Reports;
using TaskFlow.Common.Constants;
using TaskFlow.Common.Enums;
using TaskFlow.Common.Helpers;

namespace TaskFlow.BusinessLogic.Reports
{
    // f09-reports.md R2-R13: pure replay of one task's trail into holder-
    // attributed facts, and pure aggregation of facts into a MetricSet over
    // a holder filter / local-date range / measured UTC window. No I/O -
    // ReportService supplies the trail rows and the "now" instant.
    public static class ReportCalculator
    {
        // ---- Fact extraction (one task) ----

        public static ReportTaskFacts ExtractFacts(ReportTaskInput task, string timeZone)
        {
            var history = task.History;
            var assigneeChanges = history.Where(h => h.Event == TaskHistoryEvent.AssigneeChanged).ToList();

            var facts = new ReportTaskFacts
            {
                TaskId = task.TaskId,
                Title = task.Title,
                IsDeleted = task.IsDeleted,
            };

            int? lastDoneIndex = null;
            (int Index, DateTimeOffset Time)? openSubmission = null;
            DateTimeOffset? inProgressStart = null;

            for (var i = 0; i < history.Count; i++)
            {
                var entry = history[i];

                if (entry.Event == TaskHistoryEvent.StatusChanged)
                {
                    if (entry.ToValue == nameof(TaskWorkflowStatus.InProgress))
                    {
                        inProgressStart = entry.CreatedAt;
                    }
                    else if (entry.FromValue == nameof(TaskWorkflowStatus.InProgress) && inProgressStart.HasValue)
                    {
                        AddInProgressSegments(facts.InProgressSegments, assigneeChanges, task.TaskId, task.AssigneeId, inProgressStart.Value, entry.CreatedAt);
                        inProgressStart = null;
                    }

                    // R7: a submission is InProgress->Review only. Its
                    // outcome is the next entry that leaves Review - a
                    // StatusChanged Review->InProgress with no open
                    // submission (a Reopened-induced round trip) is ignored.
                    if (entry.FromValue == nameof(TaskWorkflowStatus.InProgress) && entry.ToValue == nameof(TaskWorkflowStatus.Review))
                    {
                        openSubmission = (i, entry.CreatedAt);
                    }
                    else if (entry.FromValue == nameof(TaskWorkflowStatus.Review)
                        && (entry.ToValue == nameof(TaskWorkflowStatus.InProgress) || entry.ToValue == nameof(TaskWorkflowStatus.Done))
                        && openSubmission.HasValue)
                    {
                        var outcome = entry.ToValue == nameof(TaskWorkflowStatus.InProgress)
                            ? ReportSubmissionOutcome.SentBack
                            : ReportSubmissionOutcome.Accepted;

                        facts.Submissions.Add(new ReportSubmissionFact
                        {
                            HolderId = HolderAtIndex(history, openSubmission.Value.Index, task.AssigneeId),
                            SubmittedAtUtc = openSubmission.Value.Time,
                            Outcome = outcome,
                        });
                        openSubmission = null;
                    }

                    if (entry.ToValue == nameof(TaskWorkflowStatus.Done))
                    {
                        lastDoneIndex = i;
                    }
                }
                else if (entry.Event == TaskHistoryEvent.Reopened)
                {
                    facts.Reopens.Add(new ReportPointFact { HolderId = HolderAtIndex(history, i, task.AssigneeId), AtUtc = entry.CreatedAt });

                    // R3's known property: a Reopened entry removes the
                    // completion it follows from every bucket that counted it.
                    lastDoneIndex = null;
                }
                else if (entry.Event == TaskHistoryEvent.OverdueMarked)
                {
                    facts.OverdueMarks.Add(new ReportPointFact { HolderId = HolderAtIndex(history, i, task.AssigneeId), AtUtc = entry.CreatedAt });
                }
                else if (entry.Event == TaskHistoryEvent.AssigneeChanged)
                {
                    var toId = ParseLong(entry.ToValue);
                    var fromId = ParseLong(entry.FromValue);
                    if (entry.ActorUserId.HasValue && toId.HasValue && fromId.HasValue
                        && entry.ActorUserId.Value == toId.Value && fromId.Value != toId.Value)
                    {
                        facts.Takeovers.Add(new ReportTakeoverFact { ToUserId = toId.Value, FromUserId = fromId.Value, AtUtc = entry.CreatedAt });
                    }
                }
            }

            if (inProgressStart.HasValue)
            {
                // R10: a soft-deleted task's open InProgress interval ends at
                // its Deleted entry; otherwise it is still open (clipped by
                // the caller's measured window, which never extends past "now").
                var deletedEntry = history.LastOrDefault(h => h.Event == TaskHistoryEvent.Deleted);
                DateTimeOffset? end = deletedEntry != null && deletedEntry.CreatedAt >= inProgressStart.Value ? deletedEntry.CreatedAt : null;
                AddInProgressSegments(facts.InProgressSegments, assigneeChanges, task.TaskId, task.AssigneeId, inProgressStart.Value, end);
            }

            if (lastDoneIndex.HasValue)
            {
                var doneEntry = history[lastDoneIndex.Value];

                var firstInProgress = history.FirstOrDefault(h => h.Event == TaskHistoryEvent.StatusChanged && h.ToValue == nameof(TaskWorkflowStatus.InProgress));

                ReportHistoryEntryInput? reviewEntry = null;
                for (var i = lastDoneIndex.Value - 1; i >= 0; i--)
                {
                    if (history[i].Event == TaskHistoryEvent.StatusChanged
                        && history[i].FromValue == nameof(TaskWorkflowStatus.InProgress)
                        && history[i].ToValue == nameof(TaskWorkflowStatus.Review))
                    {
                        reviewEntry = history[i];
                        break;
                    }
                }

                var dueDate = DueDateAtIndex(history, lastDoneIndex.Value, task.DueDate);

                facts.Completion = new ReportCompletionFact
                {
                    HolderId = HolderAtIndex(history, lastDoneIndex.Value, task.AssigneeId),
                    CompletedAtUtc = doneEntry.CreatedAt,
                    CycleTimeSeconds = firstInProgress != null ? (long)(doneEntry.CreatedAt - firstInProgress.CreatedAt).TotalSeconds : null,
                    LeadTimeSeconds = (long)(doneEntry.CreatedAt - task.CreatedAt).TotalSeconds,
                    ReviewWaitSeconds = reviewEntry != null ? (long)(doneEntry.CreatedAt - reviewEntry.CreatedAt).TotalSeconds : 0,
                    Priority = PriorityAtIndex(history, lastDoneIndex.Value, task.Priority),
                    DueDate = dueDate,
                    OnTimeStatus = ComputeOnTimeStatus(dueDate, doneEntry.CreatedAt, timeZone),
                };
            }

            return facts;
        }

        // ---- Aggregation (many tasks, one holder filter, one range) ----

        public static MetricSetDto BuildMetricSet(
            IReadOnlyList<ReportTaskFacts> allFacts,
            bool teamScope,
            long? holderId,
            DateOnly rangeStart,
            DateOnly rangeEnd,
            DateTimeOffset measuredStartUtc,
            DateTimeOffset measuredEndUtcExclusive,
            string timeZone,
            bool includeLoad)
        {
            bool InRange(DateTimeOffset at) => IsInRange(at, rangeStart, rangeEnd, timeZone);
            bool Matches(long? holder) => teamScope || holder == holderId;

            var completions = allFacts
                .Where(f => f.Completion != null && Matches(f.Completion!.HolderId) && InRange(f.Completion!.CompletedAtUtc))
                .Select(f => f.Completion!)
                .ToList();

            var submissions = allFacts
                .SelectMany(f => f.Submissions)
                .Where(s => Matches(s.HolderId) && InRange(s.SubmittedAtUtc))
                .ToList();

            var reopens = allFacts.SelectMany(f => f.Reopens).Where(r => Matches(r.HolderId) && InRange(r.AtUtc)).ToList();
            var overdueMarks = allFacts.SelectMany(f => f.OverdueMarks).Where(o => Matches(o.HolderId) && InRange(o.AtUtc)).ToList();
            var takeOvers = allFacts.SelectMany(f => f.Takeovers).Count(t => (teamScope || t.ToUserId == holderId) && InRange(t.AtUtc));
            var takenFrom = allFacts.SelectMany(f => f.Takeovers).Count(t => (teamScope || t.FromUserId == holderId) && InRange(t.AtUtc));

            var metricSet = ComposeMetricSet(completions, submissions, reopens, overdueMarks, takeOvers, takenFrom);

            if (includeLoad)
            {
                metricSet.Load = BuildLoad(allFacts, teamScope, holderId, measuredStartUtc, measuredEndUtcExclusive);
            }

            return metricSet;
        }

        // Bulk form of BuildMetricSet for a per-member scan over one shared
        // range/window (GetTeam's roster loop): groups every fact by holder
        // once (O(tasks)), then each member reads only its own slice
        // (O(members) on top) instead of re-scanning allFacts per member
        // (O(members x tasks)). Output is identical, member for member, to
        // calling BuildMetricSet(teamScope: false, holderId: id, ...) once
        // per id in holderIds, in the same order.
        public static List<MetricSetDto> BuildMetricSetsForRoster(
            IReadOnlyList<ReportTaskFacts> allFacts,
            IReadOnlyList<long> holderIds,
            DateOnly rangeStart,
            DateOnly rangeEnd,
            DateTimeOffset measuredStartUtc,
            DateTimeOffset measuredEndUtcExclusive,
            string timeZone,
            bool includeLoad)
        {
            bool InRange(DateTimeOffset at) => IsInRange(at, rangeStart, rangeEnd, timeZone);

            var completionsByHolder = new Dictionary<long, List<ReportCompletionFact>>();
            var submissionsByHolder = new Dictionary<long, List<ReportSubmissionFact>>();
            var reopensByHolder = new Dictionary<long, List<ReportPointFact>>();
            var overdueByHolder = new Dictionary<long, List<ReportPointFact>>();
            var takeOversByHolder = new Dictionary<long, int>();
            var takenFromByHolder = new Dictionary<long, int>();
            var segmentsByHolder = new Dictionary<long, List<ReportInProgressSegment>>();

            foreach (var f in allFacts)
            {
                if (f.Completion != null && f.Completion.HolderId.HasValue && InRange(f.Completion.CompletedAtUtc))
                {
                    Add(completionsByHolder, f.Completion.HolderId.Value, f.Completion);
                }

                foreach (var s in f.Submissions)
                {
                    if (s.HolderId.HasValue && InRange(s.SubmittedAtUtc))
                    {
                        Add(submissionsByHolder, s.HolderId.Value, s);
                    }
                }

                foreach (var r in f.Reopens)
                {
                    if (r.HolderId.HasValue && InRange(r.AtUtc))
                    {
                        Add(reopensByHolder, r.HolderId.Value, r);
                    }
                }

                foreach (var o in f.OverdueMarks)
                {
                    if (o.HolderId.HasValue && InRange(o.AtUtc))
                    {
                        Add(overdueByHolder, o.HolderId.Value, o);
                    }
                }

                foreach (var t in f.Takeovers)
                {
                    if (!InRange(t.AtUtc))
                    {
                        continue;
                    }

                    takeOversByHolder[t.ToUserId] = takeOversByHolder.GetValueOrDefault(t.ToUserId) + 1;
                    takenFromByHolder[t.FromUserId] = takenFromByHolder.GetValueOrDefault(t.FromUserId) + 1;
                }

                if (includeLoad)
                {
                    foreach (var segment in f.InProgressSegments)
                    {
                        if (segment.HolderId.HasValue)
                        {
                            Add(segmentsByHolder, segment.HolderId.Value, segment);
                        }
                    }
                }
            }

            var results = new List<MetricSetDto>(holderIds.Count);

            foreach (var holderId in holderIds)
            {
                var metricSet = ComposeMetricSet(
                    completionsByHolder.GetValueOrDefault(holderId) ?? [],
                    submissionsByHolder.GetValueOrDefault(holderId) ?? [],
                    reopensByHolder.GetValueOrDefault(holderId) ?? [],
                    overdueByHolder.GetValueOrDefault(holderId) ?? [],
                    takeOversByHolder.GetValueOrDefault(holderId),
                    takenFromByHolder.GetValueOrDefault(holderId));

                if (includeLoad)
                {
                    metricSet.Load = BuildLoadFromSegments(segmentsByHolder.GetValueOrDefault(holderId) ?? [], measuredStartUtc, measuredEndUtcExclusive);
                }

                results.Add(metricSet);
            }

            return results;
        }

        public static ReportLoadDto BuildLoad(
            IReadOnlyList<ReportTaskFacts> allFacts,
            bool teamScope,
            long? holderId,
            DateTimeOffset measuredStartUtc,
            DateTimeOffset measuredEndUtcExclusive)
        {
            var segments = allFacts.SelectMany(f => f.InProgressSegments).Where(s => teamScope || s.HolderId == holderId);
            return BuildLoadFromSegments(segments, measuredStartUtc, measuredEndUtcExclusive);
        }

        private static bool IsInRange(DateTimeOffset at, DateOnly rangeStart, DateOnly rangeEnd, string timeZone)
        {
            var localDate = at.ToDateInZone(timeZone);
            return localDate >= rangeStart && localDate <= rangeEnd;
        }

        private static void Add<T>(Dictionary<long, List<T>> dict, long holderId, T item)
        {
            if (!dict.TryGetValue(holderId, out var list))
            {
                list = [];
                dict[holderId] = list;
            }

            list.Add(item);
        }

        private static MetricSetDto ComposeMetricSet(
            List<ReportCompletionFact> completions,
            List<ReportSubmissionFact> submissions,
            List<ReportPointFact> reopens,
            List<ReportPointFact> overdueMarks,
            int takeOvers,
            int takenFrom)
        {
            var onTime = completions.Count(c => c.OnTimeStatus == ReportOnTimeStatus.OnTime);
            var late = completions.Count(c => c.OnTimeStatus == ReportOnTimeStatus.Late);
            var noDueDate = completions.Count(c => c.OnTimeStatus == ReportOnTimeStatus.NoDueDate);
            var sentBack = submissions.Count(s => s.Outcome == ReportSubmissionOutcome.SentBack);

            return new MetricSetDto
            {
                Delivery = new ReportDeliveryDto
                {
                    Done = completions.Count,
                    Urgent = completions.Count(c => c.Priority == TaskPriority.Urgent),
                    High = completions.Count(c => c.Priority == TaskPriority.High),
                    Normal = completions.Count(c => c.Priority == TaskPriority.Normal),
                },
                Speed = new ReportSpeedDto
                {
                    CycleTime = ReportStatistics.BuildDistribution(completions.Where(c => c.CycleTimeSeconds.HasValue).Select(c => c.CycleTimeSeconds!.Value).ToList()),
                    LeadTime = ReportStatistics.BuildDistribution(completions.Select(c => c.LeadTimeSeconds).ToList()),
                    ReviewWait = ReportStatistics.BuildDistribution(completions.Select(c => c.ReviewWaitSeconds).ToList()),
                },
                Reliability = new ReportReliabilityDto
                {
                    OnTime = onTime,
                    Late = late,
                    NoDueDate = noDueDate,
                    OnTimeRate = ReportStatistics.BuildRate(onTime, onTime + late),
                    OverdueMarked = overdueMarks.Count,
                },
                Quality = new ReportQualityDto
                {
                    SubmissionsDecided = submissions.Count,
                    SentBack = sentBack,
                    ReworkRate = ReportStatistics.BuildRate(sentBack, submissions.Count),
                    ReopenedAfterDone = reopens.Count,
                },
                Attribution = new ReportAttributionDto
                {
                    TakeOvers = takeOvers,
                    TakenFrom = takenFrom,
                },
            };
        }

        // f09-reports.md R8 (2026-11-22 contract change): ticketsWorked =
        // count of DISTINCT tasks whose InProgress segment overlaps the
        // bucket's measured window at all - no longer a time-weighted
        // average. A task held twice by the same holder inside the window
        // (e.g. reopened and re-worked) still counts once; a task handed
        // over mid-window counts once for each holder because this is
        // called once per holder's own segment list (team scope pools every
        // holder's segments into one list first, so a handover there still
        // yields one distinct task id).
        private static ReportLoadDto BuildLoadFromSegments(
            IEnumerable<ReportInProgressSegment> segments,
            DateTimeOffset measuredStartUtc,
            DateTimeOffset measuredEndUtcExclusive)
        {
            if (measuredEndUtcExclusive <= measuredStartUtc)
            {
                return new ReportLoadDto { TicketsWorked = 0 };
            }

            var ticketIds = new HashSet<long>();
            foreach (var segment in segments)
            {
                var overlapEnd = segment.EndUtc.HasValue && segment.EndUtc.Value < measuredEndUtcExclusive ? segment.EndUtc.Value : measuredEndUtcExclusive;
                var overlapStart = segment.StartUtc > measuredStartUtc ? segment.StartUtc : measuredStartUtc;
                if (overlapEnd > overlapStart)
                {
                    ticketIds.Add(segment.TaskId);
                }
            }

            return new ReportLoadDto { TicketsWorked = ticketIds.Count };
        }

        // ---- Holder / priority / due-date reconstruction (R2, R4, R6) ----

        private static long? HolderAtIndex(IReadOnlyList<ReportHistoryEntryInput> history, int atIndex, long? currentAssigneeId)
        {
            for (var i = atIndex; i < history.Count; i++)
            {
                if (history[i].Event == TaskHistoryEvent.AssigneeChanged)
                {
                    return ParseLong(history[i].FromValue);
                }
            }

            return currentAssigneeId;
        }

        private static TaskPriority PriorityAtIndex(IReadOnlyList<ReportHistoryEntryInput> history, int atIndex, TaskPriority currentPriority)
        {
            for (var i = atIndex; i < history.Count; i++)
            {
                if (history[i].Event == TaskHistoryEvent.PriorityChanged && history[i].FromValue != null)
                {
                    return Enum.Parse<TaskPriority>(history[i].FromValue!);
                }
            }

            return currentPriority;
        }

        private static DateOnly? DueDateAtIndex(IReadOnlyList<ReportHistoryEntryInput> history, int atIndex, DateOnly? currentDueDate)
        {
            for (var i = atIndex; i < history.Count; i++)
            {
                if (history[i].Event == TaskHistoryEvent.DueDateChanged)
                {
                    return string.IsNullOrEmpty(history[i].FromValue)
                        ? null
                        : DateOnly.ParseExact(history[i].FromValue!, DateTimeFormats.Date, CultureInfo.InvariantCulture);
                }
            }

            return currentDueDate;
        }

        // R6: on-time is judged against the completion's own local date in
        // the configured zone (never a literal offset - DateTimeHelper).
        private static ReportOnTimeStatus ComputeOnTimeStatus(DateOnly? dueDate, DateTimeOffset completedAtUtc, string timeZone)
        {
            if (!dueDate.HasValue)
            {
                return ReportOnTimeStatus.NoDueDate;
            }

            var completedLocalDate = completedAtUtc.ToDateInZone(timeZone);
            return completedLocalDate <= dueDate.Value ? ReportOnTimeStatus.OnTime : ReportOnTimeStatus.Late;
        }

        private static long? ParseLong(string? value) => long.TryParse(value, out var parsed) ? parsed : null;

        private static void AddInProgressSegments(
            List<ReportInProgressSegment> target,
            List<ReportHistoryEntryInput> assigneeChanges,
            long taskId,
            long? currentAssigneeId,
            DateTimeOffset start,
            DateTimeOffset? end)
        {
            var boundaries = assigneeChanges
                .Where(c => c.CreatedAt > start && (!end.HasValue || c.CreatedAt < end.Value))
                .OrderBy(c => c.CreatedAt)
                .ToList();

            var segmentStart = start;
            var currentHolder = HolderJustBefore(start, assigneeChanges, currentAssigneeId);

            foreach (var change in boundaries)
            {
                target.Add(new ReportInProgressSegment { TaskId = taskId, HolderId = currentHolder, StartUtc = segmentStart, EndUtc = change.CreatedAt });
                currentHolder = ParseLong(change.ToValue);
                segmentStart = change.CreatedAt;
            }

            target.Add(new ReportInProgressSegment { TaskId = taskId, HolderId = currentHolder, StartUtc = segmentStart, EndUtc = end });
        }

        private static long? HolderJustBefore(DateTimeOffset at, List<ReportHistoryEntryInput> assigneeChanges, long? currentAssigneeId)
        {
            var next = assigneeChanges.Where(c => c.CreatedAt >= at).OrderBy(c => c.CreatedAt).FirstOrDefault();
            return next != null ? ParseLong(next.FromValue) : currentAssigneeId;
        }
    }
}
