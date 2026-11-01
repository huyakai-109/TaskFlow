using TaskFlow.Common.Enums;

namespace TaskFlow.Api.Models.Responses.Tasks
{
    public class TaskSummaryRes
    {
        public long Id { get; set; }

        public string Title { get; set; } = string.Empty;

        public TaskWorkflowStatus Status { get; set; }

        public TaskPriority Priority { get; set; }

        public AssigneeRes? Assignee { get; set; }

        public DateOnly? DueDate { get; set; }

        public bool IsOverdue { get; set; }

        public DateTimeOffset? CompletedAt { get; set; }

        public Guid Version { get; set; }

        public List<AllowedTransitionRes> AllowedTransitions { get; set; } = [];
    }
}
