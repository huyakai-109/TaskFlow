using System.ComponentModel.DataAnnotations;

namespace TaskFlow.Api.Models.Requests.Tasks
{
    // An explicit JSON null clears the due date (f01-workflow.md T5 endpoint 7).
    public class TaskUpdateDueDateReq
    {
        public DateOnly? DueDate { get; set; }

        [Required]
        public Guid? Version { get; set; }
    }
}
