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
                Permissions.Tasks.ViewTasks,
                Permissions.Tasks.CreateTasks,
                Permissions.Tasks.UpdateTasks,
                Permissions.Tasks.MoveTasks,
                Permissions.Tasks.AssignTasks,
                Permissions.Tasks.DeleteTasks,
                Permissions.Tasks.ReopenTasks,
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
                Permissions.Tasks.ViewTasks,
                Permissions.Tasks.CreateTasks,
                Permissions.Tasks.UpdateTasks,
                Permissions.Tasks.MoveTasks,
                Permissions.Tasks.AssignTasks,
                Permissions.Tasks.DeleteTasks,
                Permissions.Tasks.ReopenTasks,
            ];
        }

        public static class Member
        {
            public const long Id = 4;
            public const string Name = "Member";
            public const string DisplayName = "Member";

            public static readonly string[] AllowedPermissions =
            [
                Permissions.Tasks.ViewTasks,
                Permissions.Tasks.CreateTasks,
                Permissions.Tasks.UpdateTasks,
                Permissions.Tasks.MoveTasks,
                Permissions.Tasks.AssignTasks,
            ];
        }
    }
}
