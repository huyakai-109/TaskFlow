namespace TaskFlow.Common.Constants
{
    // f09-reports.md "Technical contract" CSV datasets - the exact column
    // order and names for each exported dataset.
    public static class ReportCsvHeaders
    {
        public static readonly string[] Buckets =
        [
            "bucket_start", "bucket_end", "is_partial", "done", "done_urgent", "done_high", "done_normal",
            "on_time", "late", "no_due_date", "on_time_rate", "overdue_marked",
            "cycle_median_h", "cycle_p85_h", "lead_median_h", "lead_p85_h", "review_wait_median_h", "review_wait_p85_h",
            "submissions_decided", "sent_back", "rework_rate", "reopened_after_done",
            "tickets_worked", "take_overs", "taken_from", "low_sample",
        ];

        public static readonly string[] Members =
        [
            "user_name", "display_name", "is_active", "done", "done_urgent", "done_high", "done_normal",
            "on_time", "late", "no_due_date", "on_time_rate", "overdue_marked",
            "cycle_median_h", "cycle_p85_h", "lead_median_h", "lead_p85_h", "review_wait_median_h", "review_wait_p85_h",
            "submissions_decided", "sent_back", "rework_rate", "reopened_after_done",
            "tickets_worked", "take_overs", "taken_from",
        ];

        public static readonly string[] Tasks =
        [
            "task_id", "title", "credited_user", "completed_local", "priority", "due_date", "on_time", "cycle_h", "lead_h",
        ];
    }
}
