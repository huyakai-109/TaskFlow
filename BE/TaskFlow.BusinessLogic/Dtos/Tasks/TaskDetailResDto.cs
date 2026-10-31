using TaskFlow.Common.Enums;

namespace TaskFlow.BusinessLogic.Dtos.Tasks
{
    public class TaskDetailResDto
    {
        public long Id { get; set; }

        public string Title { get; set; } = string.Empty;

        public string? Description { get; set; }

        public TaskWorkflowStatus Status { get; set; }

        public TaskPriority Priority { get; set; }

        public long? AssigneeId { get; set; }

        public string? AssigneeUserName { get; set; }

        public DateOnly? DueDate { get; set; }

        public bool IsOverdue { get; set; }

        public DateTimeOffset? CompletedAt { get; set; }

        public Guid Version { get; set; }

        public List<AllowedTransitionResDto> AllowedTransitions { get; set; } = [];

        public DateTimeOffset CreatedAt { get; set; }

        public long CreatedBy { get; set; }

        public DateTimeOffset UpdatedAt { get; set; }
    }
}
