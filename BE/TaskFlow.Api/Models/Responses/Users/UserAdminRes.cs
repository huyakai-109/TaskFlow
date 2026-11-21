namespace TaskFlow.Api.Models.Responses.Users
{
    public class UserAdminRes
    {
        public long Id { get; set; }

        public string UserName { get; set; } = string.Empty;

        public string? DisplayName { get; set; }

        public string Role { get; set; } = string.Empty;

        public bool IsActive { get; set; }

        public DateTimeOffset? DeactivatedAt { get; set; }

        public bool MustChangePassword { get; set; }

        public bool IsLockedOut { get; set; }

        public DateTimeOffset CreatedAt { get; set; }

        public string? AvatarUrl { get; set; }
    }

    public class UserCreatedRes
    {
        public UserAdminRes User { get; set; } = null!;

        public string TemporaryPassword { get; set; } = string.Empty;
    }

    public class PasswordResetRes
    {
        public string TemporaryPassword { get; set; } = string.Empty;
    }
}
