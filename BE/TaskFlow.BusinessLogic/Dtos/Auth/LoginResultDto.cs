namespace TaskFlow.BusinessLogic.Dtos.Auth
{
    public class LoginResultDto
    {
        public string? Token { get; set; }

        public string? RefreshToken { get; set; }

        public DateTimeOffset RefreshTokenExpiresAt { get; set; }

        public string[] Permissions { get; set; } = [];

        public bool Success { get; set; }

        public bool InvalidCredential { get; set; }

        public bool WillBeLockedOut { get; set; }

        public bool IsLockedOut { get; set; }

        // R2-3 (f08-accounts-profile.md "Auth changes"): correct password,
        // but the account must change it before normal login works - no
        // tokens are issued.
        public bool PasswordChangeRequired { get; set; }
    }
}
