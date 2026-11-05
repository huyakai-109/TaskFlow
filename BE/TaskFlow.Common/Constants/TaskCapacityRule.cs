using TaskFlow.Common.Enums;

namespace TaskFlow.Common.Constants
{
    // Single implementation of F02's capacity rule - shared by Move (slice
    // 3.1) and Assign (slice 3.2). Never duplicate these predicates
    // (f02-capacity.md Technical contract T3).
    public static class TaskCapacityRule
    {
        // D2: only InProgress occupies a capacity slot.
        public static bool OccupiesSlot(TaskWorkflowStatus status)
        {
            return status == TaskWorkflowStatus.InProgress;
        }

        // D5, clarified 2026-11-05: an assignee is mandatory from InProgress
        // onward, including Done, so reopening Done -> Review never produces
        // an unassigned Review task.
        public static bool AssigneeRequiredWhile(TaskWorkflowStatus status)
        {
            return status == TaskWorkflowStatus.InProgress
                || status == TaskWorkflowStatus.Review
                || status == TaskWorkflowStatus.Done;
        }
    }
}
