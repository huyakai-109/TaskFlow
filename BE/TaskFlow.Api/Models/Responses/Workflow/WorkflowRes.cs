namespace TaskFlow.Api.Models.Responses.Workflow
{
    public class WorkflowRes
    {
        public int MaxInProgressPerAssignee { get; set; }

        public string TimeZone { get; set; } = string.Empty;

        public DateOnly Today { get; set; }

        public OverdueScanRes OverdueScan { get; set; } = null!;
    }

    public class OverdueScanRes
    {
        public DateTimeOffset? LastFiredAt { get; set; }

        public DateTimeOffset? NextFireAt { get; set; }
    }
}
