using System.ComponentModel.DataAnnotations;
using TaskFlow.Common.Constants;
using TaskFlow.Common.Enums;

namespace TaskFlow.Api.Models.Requests.Tasks
{
    // f01-workflow.md T5 endpoint 4: page 2+ of one column, same paging
    // vocabulary (start = 1-based page number, length = page size) as every
    // other list. F05 later adds an isOverdue filter here, additive.
    public class TaskGetListReq
    {
        [EnumDataType(typeof(TaskWorkflowStatus))]
        public TaskWorkflowStatus? Status { get; set; }

        public int Start { get; set; } = GlobalConstants.PageConfig.Start;

        public int Length { get; set; } = GlobalConstants.PageConfig.Length;
    }
}
