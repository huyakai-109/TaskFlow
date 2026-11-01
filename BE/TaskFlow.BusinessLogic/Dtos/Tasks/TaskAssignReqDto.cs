namespace TaskFlow.BusinessLogic.Dtos.Tasks
{
    public class TaskAssignReqDto
    {
        public long? AssigneeId { get; set; }

        public Guid? Version { get; set; }
    }
}
