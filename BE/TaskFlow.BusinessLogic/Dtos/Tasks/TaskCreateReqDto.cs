using TaskFlow.Common.Enums;

namespace TaskFlow.BusinessLogic.Dtos.Tasks
{
    public class TaskCreateReqDto
    {
        public string Title { get; set; } = string.Empty;

        public string? Description { get; set; }

        // f01-workflow.md Revision R2, D13: optional - a Member creating
        // without PlanTasks never supplies this; defaults to Normal.
        public TaskPriority? Priority { get; set; }

        public DateOnly? DueDate { get; set; }

        public long? AssigneeId { get; set; }
    }
}
