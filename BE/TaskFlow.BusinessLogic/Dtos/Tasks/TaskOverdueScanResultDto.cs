namespace TaskFlow.BusinessLogic.Dtos.Tasks
{
    // f05-overdue.md Technical contract T1: one run's outcome, logged at the
    // end and returned to the caller (the future Quartz job). No wire shape -
    // the scan has no HTTP surface.
    public class TaskOverdueScanResultDto
    {
        public int Examined { get; set; }

        public int Flagged { get; set; }

        public int Skipped { get; set; }

        public int Failed { get; set; }
    }
}
