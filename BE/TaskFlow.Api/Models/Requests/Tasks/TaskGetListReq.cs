using System.ComponentModel.DataAnnotations;
using TaskFlow.Common.Constants;
using TaskFlow.Common.Enums;

namespace TaskFlow.Api.Models.Requests.Tasks
{
    // f01-workflow.md T5 endpoint 4: page 2+ of one column, same paging
    // vocabulary (start = 1-based page number, length = page size) as every
    // other list.
    public class TaskGetListReq
    {
        [EnumDataType(typeof(TaskWorkflowStatus))]
        public TaskWorkflowStatus? Status { get; set; }

        // f05-overdue.md T8: optional, absent = no filter.
        public bool? IsOverdue { get; set; }

        // f10-read-models-and-sort.md Section 2: composes with status/
        // isOverdue/sort; My tasks uses this instead of board-cache filtering.
        public long? AssigneeId { get; set; }

        [EnumDataType(typeof(TaskSortOrder))]
        public TaskSortOrder Sort { get; set; } = TaskSortOrder.Priority;

        public int Start { get; set; } = GlobalConstants.PageConfig.Start;

        public int Length { get; set; } = GlobalConstants.PageConfig.Length;
    }
}
