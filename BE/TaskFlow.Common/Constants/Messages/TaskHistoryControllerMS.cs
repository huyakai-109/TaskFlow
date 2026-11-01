namespace TaskFlow.Common.Constants.Messages
{
    // GET /tasks/{taskId}/history (f04-audit.md T3). ViewTasks is the policy
    // (D5) - there is no permission-specific code here, 403 comes from
    // [Authorize(Policy)] like every other endpoint.
    public static class TaskHistoryControllerMS
    {
        public struct GetList
        {
            public const string TaskNotFound = "TaskHistory.GetList.TaskNotFound";
            public const string Exception = "TaskHistory.GetList.Exception";
        }
    }
}
