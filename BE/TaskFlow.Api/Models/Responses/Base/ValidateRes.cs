namespace TaskFlow.Api.Models.Responses.Base
{
    public class ValidateRes
    {
        public required string Key { get; set; }

        public required string[] Errors { get; set; }
    }
}
