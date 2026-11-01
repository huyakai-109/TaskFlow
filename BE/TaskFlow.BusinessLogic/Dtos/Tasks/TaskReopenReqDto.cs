namespace TaskFlow.BusinessLogic.Dtos.Tasks
{
    public class TaskReopenReqDto
    {
        public string Reason { get; set; } = string.Empty;

        public Guid? Version { get; set; }
    }
}
