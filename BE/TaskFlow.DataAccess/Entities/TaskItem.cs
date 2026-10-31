using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using TaskFlow.Common.Enums;
using TaskFlow.DataAccess.IEntities;

namespace TaskFlow.DataAccess.Entities
{
    // "Task" collides with System.Threading.Tasks.Task, reachable through
    // ImplicitUsings - table is "Tasks", entity is TaskItem.
    [Table("Tasks")]
    public class TaskItem : BaseEntity, IIsDeletedEntity
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public long Id { get; set; }

        public string Title { get; set; }

        public string Description { get; set; }

        public TaskWorkflowStatus Status { get; set; }

        public TaskPriority Priority { get; set; }

        public long? AssigneeId { get; set; }

        public DateOnly? DueDate { get; set; }

        public DateTimeOffset? CompletedAt { get; set; }

        public bool IsOverdue { get; set; }

        public Guid Version { get; set; }

        public bool IsDeleted { get; set; }
    }
}
