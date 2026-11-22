namespace TaskFlow.Api.Models.Responses.Reports
{
    public class ReportTeamRes
    {
        public ReportMetaRes Meta { get; set; } = new();

        public ReportTeamTotalsRes Team { get; set; } = new();

        public TeamMedianRes TeamMedian { get; set; } = new();

        public List<ReportTeamMemberRes> Members { get; set; } = [];

        public ReportHeatmapRes Heatmap { get; set; } = new();
    }

    public class ReportTeamTotalsRes
    {
        public MetricSetRes Totals { get; set; } = new();

        public int Contributors { get; set; }

        public int UnassignedOverdueMarked { get; set; }
    }

    public class ReportTeamMemberRes
    {
        public ReportUserRefRes User { get; set; } = new();

        public MetricSetRes Totals { get; set; } = new();
    }

    public class ReportHeatmapRes
    {
        public List<ReportBucketRes> Weeks { get; set; } = [];

        public List<ReportHeatmapRowRes> Rows { get; set; } = [];
    }

    public class ReportHeatmapRowRes
    {
        public long UserId { get; set; }

        // null = no data for that week (f09-reports.md R8/R15).
        public List<int?> TicketsWorked { get; set; } = [];
    }
}
