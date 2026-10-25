namespace TaskFlow.Api.Models.Responses.Auth
{
    public class LoginRes
    {
        public string? Token { get; set; }

        public string? RefreshToken { get; set; }
    }
}
