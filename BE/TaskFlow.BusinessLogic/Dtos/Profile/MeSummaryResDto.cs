namespace TaskFlow.BusinessLogic.Dtos.Profile
{
    // f10-read-models-and-sort.md Section 6.
    public class MeSummaryResDto
    {
        public int InProgress { get; set; }

        public int CompletedThisWeek { get; set; }

        public DateOnly WeekStart { get; set; }

        public DateOnly WeekEnd { get; set; }
    }
}
