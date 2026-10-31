using TaskFlow.Common.Enums;

namespace TaskFlow.BusinessLogic.Dtos.Tasks
{
    public class TaskMoveReqDto
    {
        public TaskWorkflowStatus Status { get; set; }

        public Guid? Version { get; set; }
    }
}
