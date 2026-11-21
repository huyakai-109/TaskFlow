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

        public bool CanTakeOver { get; set; }

        public bool CanAssignOthers { get; set; }

        public LastTransitionRes? LastTransition { get; set; }
    }

    public class LastTransitionRes
    {
        public string? From { get; set; }

        public string To { get; set; } = string.Empty;

        public LastTransitionActorRes? Actor { get; set; }

        public DateTimeOffset At { get; set; }
    }

    public class LastTransitionActorRes
    {
        public long Id { get; set; }

        public string UserName { get; set; } = string.Empty;

        public string? DisplayName { get; set; }
    }
}
