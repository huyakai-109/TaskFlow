namespace TaskFlow.BusinessLogic.Dtos.Reports
{
    public class ReportTeamResDto
    {
        public ReportMetaDto Meta { get; set; } = new();

        public MetricSetDto TeamTotals { get; set; } = new();

        public int Contributors { get; set; }

        public int UnassignedOverdueMarked { get; set; }

        public ReportTeamMedianDto TeamMedian { get; set; } = new();

        public List<ReportTeamMemberDto> Members { get; set; } = [];

        public ReportHeatmapDto Heatmap { get; set; } = new();
    }

    public class ReportTeamMemberDto
    {
        public ReportUserRefDto User { get; set; } = new();

        public MetricSetDto Totals { get; set; } = new();
    }

    public class ReportHeatmapDto
    {
        public List<ReportBucketDto> Weeks { get; set; } = [];

        public List<ReportHeatmapRowDto> Rows { get; set; } = [];
    }

    public class ReportHeatmapRowDto
    {
        public long UserId { get; set; }

        public List<double?> AvgInProgress { get; set; } = [];
    }
}
