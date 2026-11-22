namespace TaskFlow.Common.Constants.Messages
{
    // R2-3 (f08-accounts-profile.md "Auth changes"). The first
    // AuthControllerMS constants class for this feature - AuthController's
    // existing Login/RefreshToken actions keep their literal Error strings
    // (CLAUDE.md "scaffold code that is not a pattern": not touched here,
    // not a precedent to follow for new code).
    public static class AuthControllerMS
    {
        public struct Login
        {
            // Correct password, but MustChangePassword is set: no tokens are
            // issued until POST /auth/password/change-required succeeds.
            public const string PasswordChangeRequired = "Auth.Login.PasswordChangeRequired";
        }

        public struct ChangeRequired
        {
            public const string InvalidCredential = "Auth.ChangeRequired.InvalidCredential";
            public const string PolicyViolation = "Auth.ChangeRequired.PolicyViolation";
            public const string Exception = "Auth.ChangeRequired.Exception";
        }
    }
}
