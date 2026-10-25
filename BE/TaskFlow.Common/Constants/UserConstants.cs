namespace TaskFlow.Common.Constants
{
    public class UserConstants
    {
        public const long AdminId = 1;

        public static class Password
        {
            public const int MinLength = 8;
            public const string RegexPattern = @"^(?=.*[a-z])(?=.*[A-Z])(?=.*\d)(?=.*[^\da-zA-Z]).{8,}$";
            public const string ValidChars = "abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ1234567890!@#$%^&*()_+-=[]{}|;:,.<>?";
        }

        public static class Claim
        {
            public const string Name = "Name";
            public const string JobTitle = "JobTitle";
            public const string AvatarUrl = "AvatarUrl";
        }
    }
}
