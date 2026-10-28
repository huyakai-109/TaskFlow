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

        public TaskNotificationEvent Event { get; set; }

        public bool IsRead { get; set; }

        public DateTimeOffset? ReadAt { get; set; }
    }
}
