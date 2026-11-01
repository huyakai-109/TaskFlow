namespace TaskFlow.BusinessLogic.Dtos.Tasks
{
    // An explicit JSON null clears the due date (f01-workflow.md T5 endpoint
    // 7, literal contract wording) - a missing field binds the same as null,
    // which is acceptable because this is a dedicated single-field endpoint.
    public class TaskUpdateDueDateReqDto
    {
        public DateOnly? DueDate { get; set; }

        public Guid? Version { get; set; }
    }
}
