namespace TaskFlow.BusinessLogic.Dtos.Base
{
    public class ValidateResDto
    {
        public required string Key { get; set; }

        public required string[] Errors { get; set; }
    }
}
