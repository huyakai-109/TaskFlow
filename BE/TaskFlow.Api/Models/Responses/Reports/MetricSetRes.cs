namespace TaskFlow.Api.Models.Responses.Reports
{
    // f09-reports.md "Shapes": MetricSet, shared across scorecard/team/
    // member/bucket responses.
    public class MetricSetRes
    {
        public ReportDeliveryRes Delivery { get; set; } = new();

        public ReportSpeedRes Speed { get; set; } = new();

        public ReportReliabilityRes Reliability { get; set; } = new();

        public ReportQualityRes Quality { get; set; } = new();

        public ReportLoadRes? Load { get; set; }

        public ReportAttributionRes Attribution { get; set; } = new();
    }

    public class ReportDeliveryRes
    {
        public int Done { get; set; }

        public ReportByPriorityRes ByPriority { get; set; } = new();
    }

    public class ReportByPriorityRes
    {
        public int Urgent { get; set; }

        public int High { get; set; }

        public int Normal { get; set; }
    }

    public class ReportSpeedRes
    {
        public DistributionRes CycleTime { get; set; } = new();

        public DistributionRes LeadTime { get; set; } = new();

        public DistributionRes ReviewWait { get; set; } = new();
    }

    public class ReportReliabilityRes
    {
        public int OnTime { get; set; }

        public int Late { get; set; }

        public int NoDueDate { get; set; }

        public RateRes OnTimeRate { get; set; } = new();

        public int OverdueMarked { get; set; }
    }

    public class ReportQualityRes
    {
        public int SubmissionsDecided { get; set; }

        public int SentBack { get; set; }

        public RateRes ReworkRate { get; set; } = new();

        public int ReopenedAfterDone { get; set; }
    }

    public class ReportLoadRes
    {
        public int TicketsWorked { get; set; }
    }

    public class ReportAttributionRes
    {
        public int TakeOvers { get; set; }

        public int TakenFrom { get; set; }
    }

    public class DistributionRes
    {
        public int SampleSize { get; set; }

        public long? MedianSeconds { get; set; }

        public long? P85Seconds { get; set; }

        public bool LowSample { get; set; }
    }

    public class RateRes
    {
        public double? Value { get; set; }

        public int Denominator { get; set; }

        public bool LowSample { get; set; }
    }

    public class TeamMedianRes
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
