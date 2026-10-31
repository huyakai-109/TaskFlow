using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using TaskFlow.Common.Enums;

namespace TaskFlow.DataAccess.Entities
{
    // Append-only - no IIsDeletedEntity, no navigation property to TaskItem
    // (f04-audit.md T1). ActorUserId null means System; never inferred from
    // CreatedBy, which UnitOfWork stamps as 0 outside an HttpContext.
    [Table("TaskHistories")]
    public class TaskHistory : BaseEntity
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public long Id { get; set; }

        public long TaskId { get; set; }

        public TaskHistoryEvent Event { get; set; }

        public long? ActorUserId { get; set; }

        public string FromValue { get; set; }

        public string ToValue { get; set; }

        public string Reason { get; set; }
    }
}
