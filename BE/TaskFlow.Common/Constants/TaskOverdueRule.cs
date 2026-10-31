using TaskFlow.Common.Enums;

namespace TaskFlow.Common.Constants
{
    // Single implementation of "is this task overdue", called by every F01
    // write path that can change DueDate or Status (create, due-date change,
    // move into Done, reopen) and, later, by F05's nightly scan. Never
    // duplicate this predicate (f01-workflow.md T1 "Shared rules").
    public static class TaskOverdueRule
    {
        public static bool IsOverdue(DateOnly? dueDate, TaskWorkflowStatus status, DateOnly todayInZone)
        {
            if (status == TaskWorkflowStatus.Done)
            {
                return false;
            }

            return dueDate.HasValue && todayInZone > dueDate.Value;
        }
    }
}
