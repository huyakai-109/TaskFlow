namespace TaskFlow.Common.Constants.Messages
{
    // GET /users (f02-capacity.md T4, sign-off #6). ViewTasks is the policy -
    // there is no permission-specific code here, 403 comes from
    // [Authorize(Policy)] like every other endpoint.
    public static class UserControllerMS
    {
        public struct GetList
        {
            public const string Exception = "User.GetList.Exception";
        }
    }
}
