namespace TaskFlow.Api.Models.Responses.Users
{
    public class UserRes
    {
        public long Id { get; set; }

        public string UserName { get; set; } = string.Empty;

        public string Role { get; set; } = string.Empty;
    }
}
