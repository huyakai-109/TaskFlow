using System.ComponentModel.DataAnnotations;
using TaskFlow.Common.Enums;

namespace TaskFlow.Api.Models.Requests.Tasks
{
    public class TaskCreateReq
    {
        [Required]
        [MaxLength(200)]
        public string Title { get; set; } = string.Empty;

        [MaxLength(4000)]
        public string? Description { get; set; }

        // f01-workflow.md Revision R2, D13: optional, Manager/Admin
        // (PlanTasks) only - a Member supplying this is refused
        // (Task.Create.PlanningNotPermitted), not silently defaulted.
        [EnumDataType(typeof(TaskPriority))]
        public TaskPriority? Priority { get; set; }

        public DateOnly? DueDate { get; set; }

        public long? AssigneeId { get; set; }
    }
}
