namespace TaskFlow.BusinessLogic.Dtos.Users
{
    // f10-read-models-and-sort.md Section 5: replaces client-side
    // derivation from the board's page 1 (removes the "may be incomplete"
    // caveats).
    public class WorkloadResDto
    {
        public long UserId { get; set; }

        public int Todo { get; set; }

        public int InProgress { get; set; }

        public int Review { get; set; }
    }
}
