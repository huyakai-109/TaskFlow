using TaskFlow.Common.Enums;

namespace TaskFlow.Api.Models.Responses.Activity
{
    public class ActivityRes
    {
        public long Id { get; set; }

        public TaskHistoryEvent Event { get; set; }

        public ActivityTaskRes Task { get; set; } = null!;

        public long? ActorUserId { get; set; }

        public string? ActorName { get; set; }

        public string? FromValue { get; set; }

        public string? ToValue { get; set; }

        public string? Reason { get; set; }

        public DateTimeOffset OccurredAt { get; set; }

        public DateTimeOffset OccurredAtLocal { get; set; }

        public string Message { get; set; } = string.Empty;
    }

    public class ActivityTaskRes
    {
        public long Id { get; set; }

        public string Title { get; set; } = string.Empty;

        public bool IsDeleted { get; set; }
    }
}
