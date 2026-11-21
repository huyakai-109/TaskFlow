namespace TaskFlow.BusinessLogic.Dtos.Workflow
{
    // f10-read-models-and-sort.md Section 4: replaces the client literal
    // IN_PROGRESS_LIMIT; `Today` replaces browser-date approximations.
    public class WorkflowResDto
    {
        public int MaxInProgressPerAssignee { get; set; }

        public string TimeZone { get; set; } = string.Empty;

        public DateOnly Today { get; set; }

        public DateTimeOffset? LastFiredAt { get; set; }

        public DateTimeOffset? NextFireAt { get; set; }
    }
}
