using TaskFlow.Common.Enums;

namespace TaskFlow.Common.Constants.Messages
{
    // f04-audit.md T3: "message" is built on read from these templates, never
    // stored (D1). English only (MVP scope). Carries no raw exception text,
    // internal identifier or secret (AC24) - only the actor's display name,
    // the already-human structured values, and the reopen reason.
    //
    // StatusChanged and PriorityChanged values are already enum member names
    // (T2) - this class only adds the spacing InProgress needs for display.
    // AssigneeChanged stores user ids as text (T2); resolving those ids to a
    // display name needs a database lookup, so the caller (TaskHistoryService)
    // resolves them and passes the result in as fromAssigneeName/toAssigneeName.
    public static class TaskHistoryMessages
    {
        public static string Build(
            TaskHistoryEvent historyEvent,
            string? actorName,
            string? fromValue,
            string? toValue,
            string? reason,
            string? fromAssigneeName = null,
            string? toAssigneeName = null)
        {
            var actor = actorName ?? "System";

            return historyEvent switch
            {
                TaskHistoryEvent.Created => $"{actor} created the task",
                TaskHistoryEvent.StatusChanged => $"{actor} moved the task from {DisplayStatus(fromValue)} to {DisplayStatus(toValue)}",
                TaskHistoryEvent.AssigneeChanged => $"{actor} reassigned the task from {DisplayAssignee(fromAssigneeName)} to {DisplayAssignee(toAssigneeName)}",
                TaskHistoryEvent.PriorityChanged => $"{actor} changed the priority from {fromValue} to {toValue}",
                TaskHistoryEvent.DueDateChanged => $"{actor} changed the due date from {DisplayDate(fromValue)} to {DisplayDate(toValue)}",
                TaskHistoryEvent.Reopened => string.IsNullOrEmpty(reason)
                    ? $"{actor} reopened the task"
                    : $"{actor} reopened the task: {reason}",
                TaskHistoryEvent.OverdueMarked => $"{actor} flagged the task as overdue",
                TaskHistoryEvent.Deleted => $"{actor} deleted the task",
                _ => $"{actor} updated the task",
            };
        }

        private static string DisplayStatus(string? status)
        {
            return status switch
            {
                nameof(TaskWorkflowStatus.InProgress) => "In Progress",
                null => "none",
                _ => status,
            };
        }

        private static string DisplayDate(string? date)
        {
            return string.IsNullOrEmpty(date) ? "no due date" : date;
        }

        private static string DisplayAssignee(string? assigneeName)
        {
            return assigneeName ?? "Unassigned";
        }
    }
}
