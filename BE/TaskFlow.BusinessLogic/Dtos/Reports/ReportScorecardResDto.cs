namespace TaskFlow.BusinessLogic.Dtos.Reports
{
    public class ReportScorecardResDto
    {
        public ReportMetaDto Meta { get; set; } = new();

        public ReportUserRefDto? Subject { get; set; }

        public MetricSetDto Totals { get; set; } = new();

        public ReportTeamMedianDto? TeamMedian { get; set; }

        public List<ReportBucketDto> Buckets { get; set; } = [];
    }

    public class ReportBucketDto
    {
        public DateOnly Start { get; set; }

        public DateOnly End { get; set; }

        public bool IsPartial { get; set; }

        public bool HasData { get; set; }

        public MetricSetDto? Metrics { get; set; }
    }
}
