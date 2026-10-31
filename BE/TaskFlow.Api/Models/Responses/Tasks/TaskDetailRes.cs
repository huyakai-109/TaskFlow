using TaskFlow.Common.Enums;

namespace TaskFlow.Api.Models.Responses.Tasks
{
    // Summary fields (per the contract's TaskSummaryRes shape) plus Description,
    // CreatedAt, CreatedBy, UpdatedAt. TaskSummaryRes itself is not declared yet:
    // nothing returns it until the board/list endpoints (slice 1.4).
    public class TaskDetailRes
    {
        public long Id { get; set; }

        public string Title { get; set; } = string.Empty;

        public string? Description { get; set; }

        public TaskWorkflowStatus Status { get; set; }

        public TaskPriority Priority { get; set; }

        public AssigneeRes? Assignee { get; set; }

        public DateOnly? DueDate { get; set; }

        public bool IsOverdue { get; set; }

        public DateTimeOffset? CompletedAt { get; set; }

        public Guid Version { get; set; }

        public List<AllowedTransitionRes> AllowedTransitions { get; set; } = [];

        public DateTimeOffset CreatedAt { get; set; }

        public long CreatedBy { get; set; }

        public DateTimeOffset UpdatedAt { get; set; }
    }
}
