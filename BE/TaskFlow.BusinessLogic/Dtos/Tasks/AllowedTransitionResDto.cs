namespace TaskFlow.BusinessLogic.Dtos.Tasks
{
    public class AllowedTransitionResDto
    {
        public string Status { get; set; } = string.Empty;

        public string Operation { get; set; } = string.Empty;

        public bool RequiresReason { get; set; }

        // f01-workflow.md Revision R2: true only for Todo->InProgress on an
        // unassigned task when the caller holds AssignTasks - the client
        // runs "take it and start" (assign self, then move).
        public bool RequiresTakeover { get; set; }
    }
}
