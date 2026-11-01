using TaskFlow.Common.Enums;

namespace TaskFlow.BusinessLogic.Dtos.Tasks
{
    public class BoardResDto
    {
        public List<BoardColumnResDto> Columns { get; set; } = [];
    }

    // F01 ships CreatedAt asc, Id asc and 50 items per column; F03 changes
    // the order-by and the per-column page sizes later, additive on this
    // same shape (f01-workflow.md T5 endpoint 3).
    public class BoardColumnResDto
    {
        public TaskWorkflowStatus Status { get; set; }

        public List<TaskSummaryResDto> Items { get; set; } = [];

        public int TotalItems { get; set; }

        public int CurrentCount { get; set; }

        public int PageSize { get; set; }
    }
}
