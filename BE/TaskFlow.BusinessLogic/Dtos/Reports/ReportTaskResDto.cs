using TaskFlow.Common.Enums;

namespace TaskFlow.BusinessLogic.Dtos.Reports
{
    // f09-reports.md "Shapes" ReportTaskRes - one row per matched drill-down
    // event (reports/tasks) or per counted completion (CSV Tasks dataset).
    public class ReportTaskResDto
    {
        public long TaskId { get; set; }

        public string Title { get; set; } = string.Empty;

        public bool IsDeleted { get; set; }

        public ReportTaskMetric Metric { get; set; }

        public long? CreditedUserId { get; set; }

        public string? CreditedUserName { get; set; }

        public string? CreditedDisplayName { get; set; }

        public DateTimeOffset OccurredAt { get; set; }

        public DateTimeOffset OccurredAtLocal { get; set; }

        public TaskPriority Priority { get; set; }

        public DateOnly? DueDate { get; set; }

        public long? CycleTimeSeconds { get; set; }

        public long? LeadTimeSeconds { get; set; }
    }
}
