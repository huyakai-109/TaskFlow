namespace TaskFlow.BusinessLogic.Dtos.Users
{
    public class UserResDto
    {
        public long Id { get; set; }

        public string UserName { get; set; } = string.Empty;

        public string Role { get; set; } = string.Empty;

        // R2-3 (f08-accounts-profile.md "GET /users"), additive.
        public string? DisplayName { get; set; }

        public string? AvatarUrl { get; set; }

        public bool IsActive { get; set; }
    }
}
