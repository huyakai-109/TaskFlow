namespace TaskFlow.Api.Models.Responses.Base
{
    public class ExecutionRes
    {
        public bool Success { get; set; }

        public string? ErrorCode { get; set; }

        public string? Error { get; set; }

        public ValidateRes[]? Validates { get; set; }
    }
}
