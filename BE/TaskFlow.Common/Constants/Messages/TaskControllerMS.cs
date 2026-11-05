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

        // AssigneeRequired / CapacityReached: F02 slice 3.1 (f02-capacity.md
        // Technical contract T3/T4).
        public struct Move
        {
            public const string NotFound = "Task.Move.NotFound";
            public const string Conflict = "Task.Move.Conflict";
            public const string InvalidTransition = "Task.Move.InvalidTransition";
            public const string SameStatus = "Task.Move.SameStatus";
            public const string ActorIsAssignee = "Task.Move.ActorIsAssignee";
            public const string AssigneeRequired = "Task.Move.AssigneeRequired";
            public const string CapacityReached = "Task.Move.CapacityReached";
            public const string Exception = "Task.Move.Exception";
        }

        // f02-capacity.md Technical contract T4 [SIGN-OFF]: the envelope's
        // error field for *.CapacityReached is formatted from this template,
        // naming the assignee and the configured limit - never a literal at
        // the call site.
        public struct Templates
        {
            public const string CapacityReached = "{0} already has {1} tasks in progress, which is the limit.";
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

        // AssigneeRequired / CapacityReached: F02 slice 3.2 (f02-capacity.md
        // Technical contract T3/T4).
        public struct Assign
        {
            public const string NotFound = "Task.Assign.NotFound";
            public const string Conflict = "Task.Assign.Conflict";
            public const string AssigneeNotFound = "Task.Assign.AssigneeNotFound";
            public const string AssigneeRequired = "Task.Assign.AssigneeRequired";
            public const string CapacityReached = "Task.Assign.CapacityReached";
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
