using TaskFlow.Api.Models.Responses.Base;
using TaskFlow.Common.Enums;

namespace TaskFlow.Api.Models.Responses.Tasks
{
    public class BoardRes
    {
        public List<BoardColumnRes> Columns { get; set; } = [];
    }

    public class BoardColumnRes
    {
        public TaskWorkflowStatus Status { get; set; }

        public List<TaskSummaryRes> Items { get; set; } = [];

        public PaginationRes? Pagination { get; set; }
    }
}
