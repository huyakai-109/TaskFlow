using TaskFlow.Common.Enums;

namespace TaskFlow.Api.Models.Responses.Reports
{
    public class ReportScorecardRes
    {
        public ReportMetaRes Meta { get; set; } = new();

        public ReportUserRefRes? Subject { get; set; }

        public MetricSetRes Totals { get; set; } = new();

        public TeamMedianRes? TeamMedian { get; set; }

        public List<ReportBucketRes> Buckets { get; set; } = [];
    }

    public class ReportMetaRes
    {
        public string Scope { get; set; } = string.Empty;

        public long? UserId { get; set; }

        public DateOnly From { get; set; }

        public DateOnly To { get; set; }

        public ReportGranularity Granularity { get; set; }

        public string TimeZone { get; set; } = string.Empty;

        public string WeekStartsOn { get; set; } = "Monday";

        public DateOnly? DataAvailableSince { get; set; }

        public DateTimeOffset GeneratedAt { get; set; }

        public int SmallSampleThreshold { get; set; }

        public int InProgressLimit { get; set; }
    }

    public class ReportUserRefRes
    {
        public long Id { get; set; }

        public string UserName { get; set; } = string.Empty;

        public string? DisplayName { get; set; }

        public string? AvatarUrl { get; set; }

        public bool IsActive { get; set; }
    }

    public class ReportBucketRes
    {
        public DateOnly Start { get; set; }

        public DateOnly End { get; set; }

        public bool IsPartial { get; set; }

        public bool HasData { get; set; }

        public MetricSetRes? Metrics { get; set; }
    }
}
