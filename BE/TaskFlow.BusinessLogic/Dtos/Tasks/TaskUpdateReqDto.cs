namespace TaskFlow.BusinessLogic.Dtos.Tasks
{
    public class TaskUpdateReqDto
    {
        public string Title { get; set; } = string.Empty;

        public string? Description { get; set; }

        public Guid? Version { get; set; }
    }
}
