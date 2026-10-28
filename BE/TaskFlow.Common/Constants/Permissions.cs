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
        }
    }
}
