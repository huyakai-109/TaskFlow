namespace TaskFlow.BusinessLogic.Reports
{
    // Internal calculation shape - distinct from the DTO returned to the
    // controller (ReportBucketDto has no "natural" range or UTC window).
    public class ReportBucket
    {
        // Clipped to the request range [from, to] - what the client sees.
        public DateOnly Start { get; set; }

        public DateOnly End { get; set; }

        // The full calendar period this bucket belongs to (unclipped) -
        // R13's membership test for "an event belongs to the bucket
        // containing its local date" uses this, not the clipped range.
        public DateOnly NaturalStart { get; set; }

        public DateOnly NaturalEnd { get; set; }

        public bool IsPartial { get; set; }

        public bool HasData { get; set; }

        public DateTimeOffset MeasuredStartUtc { get; set; }

        public DateTimeOffset MeasuredEndUtcExclusive { get; set; }
    }
}
