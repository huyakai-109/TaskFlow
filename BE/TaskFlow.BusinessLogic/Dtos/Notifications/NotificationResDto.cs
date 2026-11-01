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
    }
}
