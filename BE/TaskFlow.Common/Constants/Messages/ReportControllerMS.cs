namespace TaskFlow.Common.Constants.Messages
{
    // f09-reports.md "Technical contract": one file per controller, codes
    // grouped by operation - the established style (TaskControllerMS).
    public static class ReportControllerMS
    {
        public struct GetScorecard
        {
            public const string InvalidRange = "Report.GetScorecard.InvalidRange";
            public const string RangeInFuture = "Report.GetScorecard.RangeInFuture";
            public const string RangeTooLarge = "Report.GetScorecard.RangeTooLarge";
            public const string TooManyBuckets = "Report.GetScorecard.TooManyBuckets";
            public const string SelfOnly = "Report.GetScorecard.SelfOnly";
            public const string UserNotFound = "Report.GetScorecard.UserNotFound";
            public const string Exception = "Report.GetScorecard.Exception";
        }

        public struct GetTeam
        {
            public const string InvalidRange = "Report.GetTeam.InvalidRange";
            public const string RangeInFuture = "Report.GetTeam.RangeInFuture";
            public const string RangeTooLarge = "Report.GetTeam.RangeTooLarge";
            public const string Exception = "Report.GetTeam.Exception";
        }

        public struct GetTasks
        {
            public const string InvalidRange = "Report.GetTasks.InvalidRange";
            public const string RangeInFuture = "Report.GetTasks.RangeInFuture";
            public const string RangeTooLarge = "Report.GetTasks.RangeTooLarge";
            public const string SelfOnly = "Report.GetTasks.SelfOnly";
            public const string UserNotFound = "Report.GetTasks.UserNotFound";
            public const string Exception = "Report.GetTasks.Exception";
        }

        public struct Export
        {
            public const string InvalidRange = "Report.Export.InvalidRange";
            public const string RangeInFuture = "Report.Export.RangeInFuture";
            public const string RangeTooLarge = "Report.Export.RangeTooLarge";
            public const string TooManyBuckets = "Report.Export.TooManyBuckets";
            public const string SelfOnly = "Report.Export.SelfOnly";
            public const string InvalidDataset = "Report.Export.InvalidDataset";
            public const string UserNotFound = "Report.Export.UserNotFound";
            public const string TooManyRows = "Report.Export.TooManyRows";
            public const string Exception = "Report.Export.Exception";
        }
    }
}
