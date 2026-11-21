using TaskFlow.Common.Enums;

namespace TaskFlow.BusinessLogic.Dtos.Activity
{
    public class ActivityResDto
    {
        public long Id { get; set; }

        public TaskHistoryEvent Event { get; set; }

        public long TaskId { get; set; }

        public string TaskTitle { get; set; } = string.Empty;

        public bool TaskIsDeleted { get; set; }

        public long? ActorUserId { get; set; }

        public string? ActorName { get; set; }

        public string? FromValue { get; set; }

        public string? ToValue { get; set; }

        public string? Reason { get; set; }

        public DateTimeOffset OccurredAt { get; set; }

        public DateTimeOffset OccurredAtLocal { get; set; }

        public string Message { get; set; } = string.Empty;
    }

    public class ActivityFeedResultDto
    {
        public List<ActivityResDto> Items { get; set; } = [];

        public string? NextCursor { get; set; }

        public bool HasMore { get; set; }

        public int PageSize { get; set; }
    }
}
