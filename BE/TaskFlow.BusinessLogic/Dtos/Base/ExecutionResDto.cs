namespace TaskFlow.BusinessLogic.Dtos.Base
{
    public class ExecutionResDto
    {
        public bool Success { get; set; }

        public string? ErrorCode { get; set; }

        public string? Error { get; set; }

        public ValidateResDto[]? Validates { get; set; }
    }
}
