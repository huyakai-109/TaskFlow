namespace TaskFlow.Common.Enums
{
    // f09-reports.md "Technical contract", drill-down (reports/tasks) and
    // CSV Tasks dataset filter.
    public enum ReportTaskMetric
    {
        Done = 1,
        OnTime = 2,
        Late = 3,
        NoDueDate = 4,
        SentBack = 5,
        Reopened = 6,
        OverdueMarked = 7,
        TakeOver = 8,
    }
}
