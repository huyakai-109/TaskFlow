using TaskFlow.Common.Enums;

namespace TaskFlow.BusinessLogic.Dtos.TaskHistories
{
    // f04-audit.md T3 TaskHistoryRes shape.
    public class TaskHistoryResDto
    {
        public long Id { get; set; }

        public long TaskId { get; set; }

        public TaskHistoryEvent Event { get; set; }

        public long? ActorUserId { get; set; }

        public string? ActorName { get; set; }

        public string? FromValue { get; set; }

        public string? ToValue { get; set; }

        public string? Reason { get; set; }

        public DateTimeOffset OccurredAt { get; set; }

        public DateTimeOffset OccurredAtLocal { get; set; }

        public string Message { get; set; } = string.Empty;
    }
}
