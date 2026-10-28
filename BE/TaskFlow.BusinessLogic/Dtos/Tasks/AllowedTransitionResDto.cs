namespace TaskFlow.BusinessLogic.Dtos.Tasks
{
    public class AllowedTransitionResDto
    {
        public string Status { get; set; } = string.Empty;

        public string Operation { get; set; } = string.Empty;

        public bool RequiresReason { get; set; }
    }
}
