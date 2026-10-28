namespace TaskFlow.Api.Models.Responses.Auth
{
    public class RefreshTokenRes
    {
        public string? Token { get; set; }

        public string[] Permissions { get; set; } = [];
    }
}
