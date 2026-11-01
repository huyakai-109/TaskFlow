namespace TaskFlow.Common.Constants.Messages
{
    // One file per controller (f01-workflow.md T3, approved 2026-10-25).
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

        // Done -> Review only, validated against TaskTransitions.Reopens, not
        // Moves (f01-workflow.md T3/T4, D6, AC6, AC13).
        public struct Reopen
        {
            public const string NotFound = "Task.Reopen.NotFound";
            public const string Conflict = "Task.Reopen.Conflict";
            public const string InvalidTransition = "Task.Reopen.InvalidTransition";
            public const string Exception = "Task.Reopen.Exception";
        }

        // Title/Description only - no history entry (f04-audit.md scope,
        // f01-workflow.md T5 endpoint 5).
        public struct Update
        {
            public const string NotFound = "Task.Update.NotFound";
            public const string Conflict = "Task.Update.Conflict";
            public const string Exception = "Task.Update.Exception";
        }

        public struct UpdatePriority
        {
            public const string NotFound = "Task.UpdatePriority.NotFound";
            public const string Conflict = "Task.UpdatePriority.Conflict";
            public const string Exception = "Task.UpdatePriority.Exception";
        }

        public struct UpdateDueDate
        {
            public const string NotFound = "Task.UpdateDueDate.NotFound";
            public const string Conflict = "Task.UpdateDueDate.Conflict";
            public const string DueDateInPast = "Task.UpdateDueDate.DueDateInPast";
            public const string Exception = "Task.UpdateDueDate.Exception";
        }

        // AssigneeRequired / CapacityReached are reserved for F02 - not
        // implemented here (f01-workflow.md T3).
        public struct Assign
        {
            public const string NotFound = "Task.Assign.NotFound";
            public const string Conflict = "Task.Assign.Conflict";
            public const string AssigneeNotFound = "Task.Assign.AssigneeNotFound";
            public const string Exception = "Task.Assign.Exception";
        }

        public struct Delete
        {
            public const string NotFound = "Task.Delete.NotFound";
            public const string Conflict = "Task.Delete.Conflict";
            public const string Exception = "Task.Delete.Exception";
        }

        public struct GetList
        {
            public const string Exception = "Task.GetList.Exception";
        }

        public struct GetBoard
        {
            public const string Exception = "Task.GetBoard.Exception";
        }
    }
}
