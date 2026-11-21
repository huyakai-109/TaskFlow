namespace TaskFlow.Common.Constants
{
    // f09-reports.md R16/R14/R19 - echoed in every response's meta
    // (smallSampleThreshold) so the client never hardcodes it.
    public static class ReportRules
    {
        public const int SmallSampleThreshold = 5;

        public const int MaxRangeDays = 366;

        public const int MaxDayBuckets = 92;

        public const int ExportMaxRows = 10000;

        public const int DrillDownMaxLength = 100;
    }
}
