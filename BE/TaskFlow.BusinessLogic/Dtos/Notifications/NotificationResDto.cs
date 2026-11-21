using TaskFlow.Common.Enums;

namespace TaskFlow.BusinessLogic.Dtos.Notifications
{
    public class NotificationResDto
    {
        public long Id { get; set; }

        public long TaskId { get; set; }

        public TaskNotificationEvent Event { get; set; }

        public bool IsRead { get; set; }

        public DateTimeOffset? ReadAt { get; set; }

        public DateTimeOffset CreatedAt { get; set; }

        // R2-2 (f06-notification-history.md API): additive. Task is always
        // present (the join already excludes soft-deleted tasks); Actor is
        // null for legacy rows and for any event with no acting user.
        public NotificationTaskResDto Task { get; set; } = null!;

        public NotificationActorResDto? Actor { get; set; }
    }

    public class NotificationTaskResDto
    {
        public long Id { get; set; }

        public string Title { get; set; } = string.Empty;
    }

    public class NotificationActorResDto
    {
        public long Id { get; set; }

        public string UserName { get; set; } = string.Empty;
    }
}
