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
            Reports.ManageReports
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
    }
}
