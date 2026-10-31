using TaskFlow.Common.Enums;

namespace TaskFlow.Common.Constants
{
    // The workflow state machine, declared once as data (f01-workflow.md T4).
    // Do not scatter "if (status == ...)" checks across services - the move
    // and reopen endpoints validate against this table, and AllowedFor is the
    // same computation the client's drag-and-drop legality (D10) relies on.
    public sealed record TaskTransition(
        TaskWorkflowStatus From,
        TaskWorkflowStatus To,
        string RequiredPermission,
        string Operation,
        bool ActorMustNotBeAssignee,
        bool RequiresAssignee,
        bool ConsumesCapacity,
        bool RequiresReason);

    public static class TaskTransitions
    {
        public const string MoveOperation = "Move";
        public const string ReopenOperation = "Reopen";

        public static readonly TaskTransition[] Moves =
        [
            new(TaskWorkflowStatus.Backlog, TaskWorkflowStatus.Todo, Permissions.Tasks.MoveTasks, MoveOperation,
                ActorMustNotBeAssignee: false, RequiresAssignee: false, ConsumesCapacity: false, RequiresReason: false),
            new(TaskWorkflowStatus.Todo, TaskWorkflowStatus.InProgress, Permissions.Tasks.MoveTasks, MoveOperation,
                ActorMustNotBeAssignee: false, RequiresAssignee: true, ConsumesCapacity: true, RequiresReason: false),
            new(TaskWorkflowStatus.InProgress, TaskWorkflowStatus.Review, Permissions.Tasks.MoveTasks, MoveOperation,
                ActorMustNotBeAssignee: false, RequiresAssignee: false, ConsumesCapacity: false, RequiresReason: false),
            new(TaskWorkflowStatus.Review, TaskWorkflowStatus.Done, Permissions.Tasks.MoveTasks, MoveOperation,
                ActorMustNotBeAssignee: true, RequiresAssignee: false, ConsumesCapacity: false, RequiresReason: false),
            new(TaskWorkflowStatus.Review, TaskWorkflowStatus.InProgress, Permissions.Tasks.MoveTasks, MoveOperation,
                ActorMustNotBeAssignee: true, RequiresAssignee: true, ConsumesCapacity: true, RequiresReason: false),
        ];

        public static readonly TaskTransition[] Reopens =
        [
            new(TaskWorkflowStatus.Done, TaskWorkflowStatus.Review, Permissions.Tasks.ReopenTasks, ReopenOperation,
                ActorMustNotBeAssignee: false, RequiresAssignee: false, ConsumesCapacity: false, RequiresReason: true),
        ];

        public static TaskTransition? FindMove(TaskWorkflowStatus from, TaskWorkflowStatus to) =>
            Array.Find(Moves, edge => edge.From == from && edge.To == to);

        public static TaskTransition? FindReopen(TaskWorkflowStatus from, TaskWorkflowStatus to) =>
            Array.Find(Reopens, edge => edge.From == from && edge.To == to);

        // D10 / AC17 / AC18: the statuses the calling user may move this task
        // to right now, with permissions, the table and the actor rule (D5)
        // already applied. Deliberately not applied: RequiresAssignee and
        // ConsumesCapacity - an unassigned Todo task still offers InProgress,
        // because the UI's assignee-picker flow is triggered by that drop.
        public static IEnumerable<TaskTransition> AllowedFor(
            TaskWorkflowStatus status,
            long? assigneeId,
            long actorUserId,
            string[] permissions)
        {
            foreach (var edge in Moves.Concat(Reopens))
            {
                if (edge.From != status)
                {
                    continue;
                }

                if (!permissions.Contains(edge.RequiredPermission))
                {
                    continue;
                }

                if (edge.ActorMustNotBeAssignee && assigneeId.HasValue && assigneeId.Value == actorUserId)
                {
                    continue;
                }

                yield return edge;
            }
        }
    }
}
