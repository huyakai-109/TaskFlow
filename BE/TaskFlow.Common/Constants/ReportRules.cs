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

        // R17: "Warning if > 2 s" - shared by every report call's elapsed-time
        // check (scorecard/team/export).
        public const int SlowCallWarningMs = 2000;

        // ReportMetaDto.Scope's two values (API field, capitalized).
        public struct Scope
        {
            public const string Team = "Team";
            public const string Member = "Member";
        }

        // The lowercase "team" token used in logs, CSV filenames and the
        // export subject - distinct from Scope.Team (the capitalized
        // response field value).
        public const string TeamSubject = "team";

        public const string ExportFileNamePrefix = "taskflow-report";

        // CSV cell rounding (f09-reports.md "Rounding"): seconds truncated
        // upstream, rates 4dp, hours 1dp.
        public struct NumberFormats
        {
            public const string RateOrUtilization = "0.0000";
            public const string Hours = "0.0";
        }
    }
}
