namespace TaskFlow.Api.Models.Responses.Profile
{
    public class MeRes
    {
        public long Id { get; set; }

        public string UserName { get; set; } = string.Empty;

        public string? DisplayName { get; set; }

        public string Role { get; set; } = string.Empty;

        public string? AvatarUrl { get; set; }
    }

    public class AvatarRes
    {
        public string AvatarUrl { get; set; } = string.Empty;
    }
}
