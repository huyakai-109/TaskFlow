namespace TaskFlow.BusinessLogic.Dtos.Profile
{
    public class MeResDto
    {
        public long Id { get; set; }

        public string UserName { get; set; } = string.Empty;

        public string? DisplayName { get; set; }

        public string Role { get; set; } = string.Empty;

        public string? AvatarUrl { get; set; }
    }
}
