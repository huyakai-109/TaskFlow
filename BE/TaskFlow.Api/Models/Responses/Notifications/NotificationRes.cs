using TaskFlow.Common.Enums;

namespace TaskFlow.Api.Models.Responses.Notifications
{
    public class NotificationRes
    {
        public long Id { get; set; }

        public long TaskId { get; set; }

        public TaskNotificationEvent Event { get; set; }

        public bool IsRead { get; set; }

        public DateTimeOffset? ReadAt { get; set; }

        public DateTimeOffset CreatedAt { get; set; }
    }
}
