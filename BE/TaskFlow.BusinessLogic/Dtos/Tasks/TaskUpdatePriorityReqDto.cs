using TaskFlow.Common.Enums;

namespace TaskFlow.BusinessLogic.Dtos.Tasks
{
    public class TaskUpdatePriorityReqDto
    {
        public TaskPriority Priority { get; set; }

        public Guid? Version { get; set; }
    }
}
