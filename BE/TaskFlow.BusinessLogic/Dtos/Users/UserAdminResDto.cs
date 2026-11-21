namespace TaskFlow.BusinessLogic.Dtos.Users
{
    // f08-accounts-profile.md "Admin API" GET /users/{id} and every admin
    // mutation's success shape.
    public class UserAdminResDto
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

    public class UserCreatedResDto
    {
        public UserAdminResDto User { get; set; } = null!;

        public string TemporaryPassword { get; set; } = string.Empty;
    }

    public class UserCreateReqDto
    {
        public string UserName { get; set; } = string.Empty;

        public string? DisplayName { get; set; }

        public string Role { get; set; } = string.Empty;
    }
}
