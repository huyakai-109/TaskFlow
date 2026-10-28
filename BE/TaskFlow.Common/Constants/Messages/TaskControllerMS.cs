namespace TaskFlow.Common.Constants.Messages
{
    // One file per controller (f01-workflow.md T3, approved 2026-10-25). Only
    // the operations built so far get a group; later slices append Move,
    // Reopen, Update, UpdatePriority, UpdateDueDate, Assign, Delete, GetList,
    // GetBoard groups to this same file.
    public static class TaskControllerMS
    {
        public struct Create
        {
            public const string DueDateInPast = "Task.Create.DueDateInPast";
            public const string AssigneeNotFound = "Task.Create.AssigneeNotFound";
            public const string Exception = "Task.Create.Exception";
        }

        public struct Get
        {
            public const string NotFound = "Task.Get.NotFound";
            public const string Exception = "Task.Get.Exception";
        }
    }
}
