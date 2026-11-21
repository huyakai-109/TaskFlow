using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using TaskFlow.Common.Enums;

namespace TaskFlow.DataAccess.Entities
{
    // No query filter: the inbox read inner-joins Tasks explicitly, so a
    // notification for a soft-deleted task disappears through that join, not
    // through a filter here (f01-workflow.md T1).
    [Table("TaskNotifications")]
    public class TaskNotification : BaseEntity
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public long Id { get; set; }

        public long RecipientUserId { get; set; }

        public long TaskId { get; set; }

        // R2-2 (f06-notification-history.md "Data"): the user who sent the
        // task back; null for legacy rows (no backfill) and, in principle,
        // for any future event with no acting user.
        public long? ActorUserId { get; set; }

        public TaskNotificationEvent Event { get; set; }

        public bool IsRead { get; set; }

        public DateTimeOffset? ReadAt { get; set; }
    }
}
