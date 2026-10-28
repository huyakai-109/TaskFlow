using TaskFlow.Common.Enums;

namespace TaskFlow.BusinessLogic.Dtos.Tasks
{
    public class TaskCreateReqDto
    {
        public string Title { get; set; } = string.Empty;

        public string? Description { get; set; }

        public TaskPriority Priority { get; set; }

        public DateOnly? DueDate { get; set; }

        public long? AssigneeId { get; set; }
    }
}
