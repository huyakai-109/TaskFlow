using TaskFlow.Common.Enums;

namespace TaskFlow.BusinessLogic.Reports
{
    // Input/output shapes of the pure ReportCalculator (f09-reports.md
    // "Technical contract"). No I/O, no entity types - ReportService maps
    // Tasks/TaskHistories rows into these before calling the calculator.

    public class ReportHistoryEntryInput
    {
        public long Id { get; set; }

        public TaskHistoryEvent Event { get; set; }

        public long? ActorUserId { get; set; }

        public string? FromValue { get; set; }

        public string? ToValue { get; set; }

        public DateTimeOffset CreatedAt { get; set; }
    }

    public class ReportTaskInput
    {
        public long TaskId { get; set; }

        public string Title { get; set; } = string.Empty;

        public bool IsDeleted { get; set; }

        public TaskWorkflowStatus Status { get; set; }

        public TaskPriority Priority { get; set; }

        public long? AssigneeId { get; set; }

        public DateOnly? DueDate { get; set; }

        public DateTimeOffset CreatedAt { get; set; }

        // Must be ordered (CreatedAt, Id) ascending - R2's reconstruction order.
        public IReadOnlyList<ReportHistoryEntryInput> History { get; set; } = [];
    }

    public enum ReportOnTimeStatus
    {
        OnTime = 1,
        Late = 2,
        NoDueDate = 3,
    }

    public enum ReportSubmissionOutcome
    {
        SentBack = 1,
        Accepted = 2,
    }

    public class ReportCompletionFact
    {
        public long? HolderId { get; set; }

        public DateTimeOffset CompletedAtUtc { get; set; }

        public long? CycleTimeSeconds { get; set; }

        public long LeadTimeSeconds { get; set; }

        public long ReviewWaitSeconds { get; set; }

        public TaskPriority Priority { get; set; }

        public DateOnly? DueDate { get; set; }

        public ReportOnTimeStatus OnTimeStatus { get; set; }
    }

    public class ReportSubmissionFact
    {
        public long? HolderId { get; set; }

        public DateTimeOffset SubmittedAtUtc { get; set; }

        public ReportSubmissionOutcome Outcome { get; set; }
    }

    public class ReportPointFact
    {
        public long? HolderId { get; set; }

        public DateTimeOffset AtUtc { get; set; }
    }

    public class ReportTakeoverFact
    {
        public long ToUserId { get; set; }

        public long FromUserId { get; set; }

        public DateTimeOffset AtUtc { get; set; }
    }

    public class ReportInProgressSegment
    {
        public long TaskId { get; set; }

        public long? HolderId { get; set; }

        public DateTimeOffset StartUtc { get; set; }

        // null = still open (task currently InProgress) - the caller clips
        // this to "now" when intersecting with a measured window.
        public DateTimeOffset? EndUtc { get; set; }
    }

    public class ReportTaskFacts
    {
        public long TaskId { get; set; }

        public string Title { get; set; } = string.Empty;

        public bool IsDeleted { get; set; }

        public ReportCompletionFact? Completion { get; set; }

        public List<ReportSubmissionFact> Submissions { get; set; } = [];

        public List<ReportPointFact> Reopens { get; set; } = [];

        public List<ReportPointFact> OverdueMarks { get; set; } = [];

        public List<ReportTakeoverFact> Takeovers { get; set; } = [];

        public List<ReportInProgressSegment> InProgressSegments { get; set; } = [];
    }
}
