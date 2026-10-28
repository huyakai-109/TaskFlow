namespace TaskFlow.Common.Enums
{
    public enum TaskHistoryEvent
    {
        Created = 1,
        StatusChanged = 2,
        AssigneeChanged = 3,
        PriorityChanged = 4,
        DueDateChanged = 5,
        Reopened = 6,
        OverdueMarked = 7,
        Deleted = 8,
    }
}
