using System.ComponentModel.DataAnnotations;

namespace TaskFlow.Api.Models.Requests.Tasks
{
    public class TaskReopenReq
    {
        // RequiredAttribute trims before checking length, so a whitespace-only
        // reason fails model validation the same as an empty one (AC22,
        // f01-workflow.md T5 endpoint 10).
        [Required]
        [MaxLength(500)]
        public string Reason { get; set; } = string.Empty;

        // Nullable so a missing field is a field-level model error instead of
        // silently binding Guid.Empty (f01-workflow.md T2).
        [Required]
        public Guid? Version { get; set; }
    }
}
