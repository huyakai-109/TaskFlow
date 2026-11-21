namespace TaskFlow.Common.Constants.Messages
{
    // GET /users (f02-capacity.md T4, sign-off #6). ViewTasks is the policy -
    // there is no permission-specific code here, 403 comes from
    // [Authorize(Policy)] like every other endpoint. R2-3
    // (f08-accounts-profile.md "Admin API") adds the account-management
    // operations below, policy ManageUsers.
    public static class UserControllerMS
    {
        public struct GetList
        {
            public const string Exception = "User.GetList.Exception";
        }

        public struct Get
        {
            public const string NotFound = "User.Get.NotFound";
            public const string Exception = "User.Get.Exception";
        }

        public struct Create
        {
            public const string UserNameTaken = "User.Create.UserNameTaken";
            public const string Exception = "User.Create.Exception";
        }

        public struct ChangeRole
        {
            public const string NotFound = "User.ChangeRole.NotFound";
            public const string LastAdmin = "User.ChangeRole.LastAdmin";
            public const string Conflict = "User.ChangeRole.Conflict";
            public const string Exception = "User.ChangeRole.Exception";
        }

        public struct Deactivate
        {
            public const string NotFound = "User.Deactivate.NotFound";
            public const string Self = "User.Deactivate.Self";
            public const string LastAdmin = "User.Deactivate.LastAdmin";
            public const string Conflict = "User.Deactivate.Conflict";
            public const string Exception = "User.Deactivate.Exception";
        }

        public struct Activate
        {
            public const string NotFound = "User.Activate.NotFound";
            public const string Conflict = "User.Activate.Conflict";
            public const string Exception = "User.Activate.Exception";
        }

        public struct ResetPassword
        {
            public const string NotFound = "User.ResetPassword.NotFound";
            public const string Conflict = "User.ResetPassword.Conflict";
            public const string Exception = "User.ResetPassword.Exception";
        }

        public struct RemoveAvatar
        {
            public const string NotFound = "User.RemoveAvatar.NotFound";
            public const string Exception = "User.RemoveAvatar.Exception";
        }

        public struct Avatar
        {
            public const string NotFound = "User.Avatar.NotFound";
            public const string Exception = "User.Avatar.Exception";
        }
    }
}
