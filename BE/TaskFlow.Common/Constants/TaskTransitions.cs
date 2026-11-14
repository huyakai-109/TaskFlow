using TaskFlow.Common.Enums;

namespace TaskFlow.Common.Constants
{
    // The workflow state machine, declared once as data (f01-workflow.md T4).
    // Do not scatter "if (status == ...)" checks across services - the move
    // and reopen endpoints validate against this table, and AllowedFor is the
    // same computation the client's drag-and-drop legality (D10) relies on.
    // RequiresAssignee and ConsumesCapacity are declared per edge but are not
    // read anywhere (confirmed by grep, f02-capacity.md slice 3.1 review,
    // 2026-11-05): TaskService.Move/Assign gate on
    // TaskCapacityRule.AssigneeRequiredWhile(toStatus)/OccupiesSlot(toStatus)
    // instead - a target-status-only rule, not a per-edge one - so these two
    // fields have drifted from actual behaviour (e.g. InProgress->Review
    // declares RequiresAssignee: false, but F02 refuses that move today
    // without a resolvable assignee). Left in place, not removed, to avoid
    // reshaping this data table outside F02's assigned scope; the real
    // source of truth for "needs an assignee" / "occupies a capacity slot"
    // is TaskCapacityRule, not these fields.
    public sealed record TaskTransition(
        TaskWorkflowStatus From,
        TaskWorkflowStatus To,
        string RequiredPermission,
        string Operation,
        bool ActorMustNotBeAssignee,
        bool RequiresAssignee,
        bool ConsumesCapacity,
        bool RequiresReason,
        // f01-workflow.md Revision R2, D11: holder-only progression. Only the
        // current assignee may advance along this edge; no role bypasses it.
        bool ActorMustBeAssignee = false);

    public static class TaskTransitions
    {
        public const string MoveOperation = "Move";
        public const string ReopenOperation = "Reopen";

        public static readonly TaskTransition[] Moves =
        [
            new(TaskWorkflowStatus.Backlog, TaskWorkflowStatus.Todo, Permissions.Tasks.MoveTasks, MoveOperation,
                ActorMustNotBeAssignee: false, RequiresAssignee: false, ConsumesCapacity: false, RequiresReason: false),
            new(TaskWorkflowStatus.Todo, TaskWorkflowStatus.InProgress, Permissions.Tasks.MoveTasks, MoveOperation,
                ActorMustNotBeAssignee: false, RequiresAssignee: true, ConsumesCapacity: true, RequiresReason: false, ActorMustBeAssignee: true),
            new(TaskWorkflowStatus.InProgress, TaskWorkflowStatus.Review, Permissions.Tasks.MoveTasks, MoveOperation,
                ActorMustNotBeAssignee: false, RequiresAssignee: false, ConsumesCapacity: false, RequiresReason: false, ActorMustBeAssignee: true),
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
        // to right now, with permissions, the table and the actor rules (D5,
        // D11) already applied. Deliberately not applied: RequiresAssignee
        // and ConsumesCapacity - an unassigned Todo task still offers
        // InProgress (as a take-over), because the UI's assignee-picker flow
        // is triggered by that drop.
        //
        // f01-workflow.md Revision R2: "AllowedFor applies D11: an
        // ActorMustBeAssignee edge is offered iff assignee == caller, or
        // (assignee == null and From == Todo and caller holds AssignTasks ->
        // requiresTakeover)." RequiresTakeover is carried alongside the edge
        // rather than on TaskTransition itself, because it depends on the
        // caller and the task's current assignee, not on the edge alone.
        public static IEnumerable<(TaskTransition Edge, bool RequiresTakeover)> AllowedFor(
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

                var requiresTakeover = false;
                if (edge.ActorMustBeAssignee)
                {
                    if (assigneeId.HasValue)
                    {
                        if (assigneeId.Value != actorUserId)
                        {
                            continue;
                        }
                    }
                    else if (edge.From == TaskWorkflowStatus.Todo && permissions.Contains(Permissions.Tasks.AssignTasks))
                    {
                        requiresTakeover = true;
                    }
                    else
                    {
                        continue;
                    }
                }

                yield return (edge, requiresTakeover);
            }
        }
    }
}
