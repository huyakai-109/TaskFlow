namespace TaskFlow.Common.Constants
{
    public static class RolePolicies
    {
        public const string ClaimType = "RolePolicy";

        public static class Admin
        {
            public const long Id = 1;
            public const string Name = "Admin";
            public const string DisplayName = "Admin";

            public static readonly string[] AllowedPermissions =
            [
                Permissions.Members.ViewMembers,
                Permissions.Members.CreateMembers,
                Permissions.Members.DeleteMembers,
                Permissions.Members.UpdateMembers,
                Permissions.Reports.ManageReports,
            ];
        }

        public static class Manager
        {
            public const long Id = 3;
            public const string Name = "Manager";
            public const string DisplayName = "Manager";

            public static readonly string[] AllowedPermissions =
            [
                  Permissions.Members.ViewMembers,
                Permissions.Members.CreateMembers,
                Permissions.Members.DeleteMembers,
                Permissions.Members.UpdateMembers,
                Permissions.Reports.ManageReports,
            ];
        }

        public static class Member
        {
            public const long Id = 4;
            public const string Name = "Member";
            public const string DisplayName = "Member";

            public static readonly string[] AllowedPermissions =
            [

            ];
        }
    }
}
