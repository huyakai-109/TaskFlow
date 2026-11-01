using TaskFlow.Common.Enums;

namespace TaskFlow.BusinessLogic.Dtos.Tasks
{
    // Board card / list row shape (f01-workflow.md T5 TaskSummaryRes).
    public class TaskSummaryResDto
    {
        public long Id { get; set; }

        public string Title { get; set; } = string.Empty;

        public TaskWorkflowStatus Status { get; set; }

        public TaskPriority Priority { get; set; }

        public long? AssigneeId { get; set; }

        public string? AssigneeUserName { get; set; }

        public DateOnly? DueDate { get; set; }

        public bool IsOverdue { get; set; }

        public DateTimeOffset? CompletedAt { get; set; }

        public Guid Version { get; set; }

        public List<AllowedTransitionResDto> AllowedTransitions { get; set; } = [];
    }
}
