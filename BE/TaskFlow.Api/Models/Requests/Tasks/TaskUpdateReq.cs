using System.ComponentModel.DataAnnotations;

namespace TaskFlow.Api.Models.Requests.Tasks
{
    public class TaskUpdateReq
    {
        [Required]
        [MaxLength(200)]
        public string Title { get; set; } = string.Empty;

        [MaxLength(4000)]
        public string? Description { get; set; }

        // Nullable so a missing field is a field-level model error instead of
        // silently binding Guid.Empty (f01-workflow.md T2).
        [Required]
        public Guid? Version { get; set; }
    }
}
