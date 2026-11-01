using TaskFlow.Common.Enums;

namespace TaskFlow.Api.Models.Responses.TaskHistories
{
    public class TaskHistoryRes
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
