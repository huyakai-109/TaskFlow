namespace TaskFlow.BusinessLogic.Dtos.Tasks
{
    // f10-read-models-and-sort.md Section 3: the latest StatusChanged/
    // Reopened entry whose ToValue equals the task's current status - a
    // historical fact ("Approved by", "Sent back by", "Submitted by"), never
    // a reviewer role (COMING-SOON C1 stands).
    public class LastTransitionResDto
    {
        public string? From { get; set; }

        public string To { get; set; } = string.Empty;

        public LastTransitionActorResDto? Actor { get; set; }

        public DateTimeOffset At { get; set; }
    }

    public class LastTransitionActorResDto
    {
        public long Id { get; set; }

        public string UserName { get; set; } = string.Empty;

        public string? DisplayName { get; set; }
    }
}
