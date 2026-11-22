namespace TaskFlow.BusinessLogic.Dtos.Reports
{
    // f09-reports.md "Shapes": MetricSet, shared by scorecard totals/buckets,
    // team totals and each member row. Grouped in one file - a cohesive
    // value shape, not a feature-sized type (BoardResDto precedent).
    public class MetricSetDto
    {
        public ReportDeliveryDto Delivery { get; set; } = new();

        public ReportSpeedDto Speed { get; set; } = new();

        public ReportReliabilityDto Reliability { get; set; } = new();

        public ReportQualityDto Quality { get; set; } = new();

        public ReportLoadDto? Load { get; set; }

        public ReportAttributionDto Attribution { get; set; } = new();
    }

    public class ReportDeliveryDto
    {
        public int Done { get; set; }

        public int Urgent { get; set; }

        public int High { get; set; }

        public int Normal { get; set; }
    }

    public class ReportSpeedDto
    {
        public ReportDistributionDto CycleTime { get; set; } = new();

        public ReportDistributionDto LeadTime { get; set; } = new();

        public ReportDistributionDto ReviewWait { get; set; } = new();
    }

    public class ReportReliabilityDto
    {
        public int OnTime { get; set; }

        public int Late { get; set; }

        public int NoDueDate { get; set; }

        public ReportRateDto OnTimeRate { get; set; } = new();

        public int OverdueMarked { get; set; }
    }

    public class ReportQualityDto
    {
        public int SubmissionsDecided { get; set; }

        public int SentBack { get; set; }

        public ReportRateDto ReworkRate { get; set; } = new();

        public int ReopenedAfterDone { get; set; }
    }

    // 2026-11-22: replaces the time-weighted avgInProgress/capacityUtilization
    // averages with a ticket count (f09-reports.md R8).
    public class ReportLoadDto
    {
        public int TicketsWorked { get; set; }
    }

    public class ReportAttributionDto
    {
        public int TakeOvers { get; set; }

        public int TakenFrom { get; set; }
    }

    public class ReportDistributionDto
    {
        public int SampleSize { get; set; }

        public long? MedianSeconds { get; set; }

        public long? P85Seconds { get; set; }

        public bool LowSample { get; set; }
    }

    public class ReportRateDto
    {
        public double? Value { get; set; }

        public int Denominator { get; set; }

        public bool LowSample { get; set; }
    }

    public class ReportTeamMedianDto
    {
        public int Contributors { get; set; }

        public double? Done { get; set; }

        public long? CycleTimeMedianSeconds { get; set; }

        public long? LeadTimeMedianSeconds { get; set; }

        public long? ReviewWaitMedianSeconds { get; set; }

        public double? OnTimeRate { get; set; }

        public double? OverdueMarked { get; set; }

        public double? ReworkRate { get; set; }
    }
}
