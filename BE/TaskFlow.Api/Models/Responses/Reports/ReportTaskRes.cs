using TaskFlow.Common.Enums;

namespace TaskFlow.Api.Models.Responses.Reports
{
    public class ReportTaskRes
    {
        public long TaskId { get; set; }

        public string Title { get; set; } = string.Empty;

        public bool IsDeleted { get; set; }

        public ReportTaskMetric Metric { get; set; }

        public ReportTaskCreditedUserRes? CreditedUser { get; set; }

        public DateTimeOffset OccurredAt { get; set; }

        public DateTimeOffset OccurredAtLocal { get; set; }

        public TaskPriority Priority { get; set; }

        public DateOnly? DueDate { get; set; }

        public long? CycleTimeSeconds { get; set; }

        public long? LeadTimeSeconds { get; set; }
    }

    public class ReportTaskCreditedUserRes
    {
        public long Id { get; set; }

        public string? UserName { get; set; }

        public string? DisplayName { get; set; }
    }
}
