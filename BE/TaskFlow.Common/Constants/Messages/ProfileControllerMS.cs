namespace TaskFlow.Common.Constants.Messages
{
    // R2-3 (f08-accounts-profile.md "Self API"), route api/v1/me, policy
    // ViewTasks - every caller acts on their own row only, read from claims.
    public static class ProfileControllerMS
    {
        public struct Get
        {
            public const string Exception = "Profile.Get.Exception";
        }

        public struct Update
        {
            public const string Conflict = "Profile.Update.Conflict";
            public const string Exception = "Profile.Update.Exception";
        }

        public struct ChangePassword
        {
            public const string WrongPassword = "Profile.ChangePassword.WrongPassword";
            public const string PolicyViolation = "Profile.ChangePassword.PolicyViolation";
            public const string Exception = "Profile.ChangePassword.Exception";
        }

        public struct Avatar
        {
            public const string Empty = "Profile.Avatar.Empty";
            public const string TooLarge = "Profile.Avatar.TooLarge";
            public const string UnsupportedType = "Profile.Avatar.UnsupportedType";
            public const string StorageUnavailable = "Profile.Avatar.StorageUnavailable";
            public const string Conflict = "Profile.Avatar.Conflict";
            public const string Exception = "Profile.Avatar.Exception";
        }

        // f10-read-models-and-sort.md Section 6.
        public struct Summary
        {
            public const string Exception = "Profile.Summary.Exception";
        }
    }
}
