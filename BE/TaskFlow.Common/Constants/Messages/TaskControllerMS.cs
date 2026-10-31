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

        // AssigneeRequired / CapacityReached are reserved for F02 (slice 1.2's
        // move does not enforce RequiresAssignee/ConsumesCapacity yet, f01-workflow.md T3).
        public struct Move
        {
            public const string NotFound = "Task.Move.NotFound";
            public const string Conflict = "Task.Move.Conflict";
            public const string InvalidTransition = "Task.Move.InvalidTransition";
            public const string SameStatus = "Task.Move.SameStatus";
            public const string ActorIsAssignee = "Task.Move.ActorIsAssignee";
            public const string Exception = "Task.Move.Exception";
        }
    }
}
