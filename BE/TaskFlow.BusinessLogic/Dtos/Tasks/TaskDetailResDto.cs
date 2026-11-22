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

        public bool CanTakeOver { get; set; }

        public bool CanAssignOthers { get; set; }

        public DateTimeOffset CreatedAt { get; set; }

        public long CreatedBy { get; set; }

        public DateTimeOffset UpdatedAt { get; set; }

        // f10-read-models-and-sort.md Section 3, additive. Populated on the
        // single-task read (GET /tasks/{id}); left null on every mutation
        // response (Create/Move/Reopen/Update*/Assign) - a deliberate scope
        // trim, not an omission: the client's next board/list/detail read
        // already carries it via BuildSummariesAsync/Get, and no mutation
        // response is the spec's named consumer (board card, Done card,
        // task detail page).
        public LastTransitionResDto? LastTransition { get; set; }
    }
}
