using System.ComponentModel.DataAnnotations;
using TaskFlow.Common.Enums;

namespace TaskFlow.Api.Models.Requests.Tasks
{
    public class TaskMoveReq
    {
        [Required]
        [EnumDataType(typeof(TaskWorkflowStatus))]
        public TaskWorkflowStatus Status { get; set; }

        // Nullable so a missing field is a field-level model error instead of
        // silently binding Guid.Empty (f01-workflow.md T2).
        [Required]
        public Guid? Version { get; set; }
    }
}
