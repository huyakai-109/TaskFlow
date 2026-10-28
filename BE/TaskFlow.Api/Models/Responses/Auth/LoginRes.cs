namespace TaskFlow.Api.Models.Responses.Auth
{
    public class LoginRes
    {
        public string? Token { get; set; }

        public string[] Permissions { get; set; } = [];
    }
}
