namespace TaskFlow.Common.Enums
{
    // f10-read-models-and-sort.md Section 1: a server-defined enum, never an
    // arbitrary client-chosen column (F03 D1 stands otherwise). Done ignores
    // every value (F03 D4) - its own order ("most recently completed first")
    // ranks higher for a finished task than any of these.
    public enum TaskSortOrder
    {
        Priority = 1,
        DueDate = 2,
        Newest = 3,
    }
}
