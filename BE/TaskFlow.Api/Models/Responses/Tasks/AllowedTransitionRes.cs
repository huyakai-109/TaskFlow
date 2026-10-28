namespace TaskFlow.Api.Models.Responses.Tasks
{
    public class AllowedTransitionRes
    {
        public string Status { get; set; } = string.Empty;

        public string Operation { get; set; } = string.Empty;

        public bool RequiresReason { get; set; }
    }
}
