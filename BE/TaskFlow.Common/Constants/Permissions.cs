namespace TaskFlow.Common.Constants
{
    public class Permissions
    {
        public static string[] All =
        [
            Members.ViewMembers,
            Members.CreateMembers,
            Members.UpdateMembers,
            Members.DeleteMembers,
            Reports.ManageReports,
            Tasks.ViewTasks,
            Tasks.CreateTasks,
            Tasks.UpdateTasks,
            Tasks.MoveTasks,
            Tasks.AssignTasks,
            Tasks.DeleteTasks,
            Tasks.ReopenTasks,
            Tasks.PlanTasks,
            Tasks.AssignAnyone,
            Users.ManageUsers,
            Reports.ViewReports,
            Reports.ExportReports,
        ];

        /// <summary>
        /// Manage = Create, Edit, Update
        /// </summary>

        public struct Members
        {
            public const string ViewMembers = "ViewMembers";
            public const string CreateMembers = "CreateMembers";
            public const string UpdateMembers = "UpdateMembers";
            public const string DeleteMembers = "DeleteMembers";
        }

        public struct Reports
        {
            public const string ManageReports = "ManageReports";

            // f09-reports.md R1/ADR 0008 decision 4: the scorecard/drill-down
            // endpoints use ViewTasks (+ the service's own SelfOnly check);
            // these two gate the team-scope and export operations. Seeded
            // for Admin/Manager only (AddReportPermissions) - ManageReports
            // stays dead, like Manager's unused Members.* claims (B8).
            public const string ViewReports = "ViewReports";
            public const string ExportReports = "ExportReports";
        }

        public struct Tasks
        {
            public const string ViewTasks = "ViewTasks";
            public const string CreateTasks = "CreateTasks";
            public const string UpdateTasks = "UpdateTasks";
            public const string MoveTasks = "MoveTasks";
            public const string AssignTasks = "AssignTasks";
            public const string DeleteTasks = "DeleteTasks";
            public const string ReopenTasks = "ReopenTasks";

            // Round 2 (f01-workflow.md Revision R2, D13/D12): planning
            // (priority, due date) and reassigning someone other than
            // yourself, both Manager/Admin only.
            public const string PlanTasks = "PlanTasks";
            public const string AssignAnyone = "AssignAnyone";
        }

        // Round 2 (f08-accounts-profile.md, ADR 0006): account administration
        // (create user, change role, deactivate/activate, reset password,
        // remove anyone's avatar). Admin only (A4 sign-off) - never granted
        // to Manager, despite Manager's unused legacy Members.* claims.
        public struct Users
        {
            public const string ManageUsers = "ManageUsers";
        }
    }
}
