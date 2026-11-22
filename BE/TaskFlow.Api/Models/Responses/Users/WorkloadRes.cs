namespace TaskFlow.Api.Models.Responses.Users
{
    public class WorkloadRes
    {
        public long UserId { get; set; }

        public int Todo { get; set; }

        public int InProgress { get; set; }

        public int Review { get; set; }
    }
}
