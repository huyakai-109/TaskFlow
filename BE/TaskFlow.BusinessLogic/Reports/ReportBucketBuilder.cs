using TaskFlow.Common.Enums;
using TaskFlow.Common.Helpers;

namespace TaskFlow.BusinessLogic.Reports
{
    // f09-reports.md R13 (bucket boundaries) + R15 (dataAvailableSince ->
    // hasData) + R13's isPartial rules (edge clip, or a measured window
    // shortened by dataAvailableSince/now).
    public static class ReportBucketBuilder
    {
        public static List<ReportBucket> Build(
            DateOnly from,
            DateOnly to,
            ReportGranularity granularity,
            DateOnly? dataAvailableSince,
            DateTimeOffset nowUtc,
            string timeZone)
        {
            var buckets = new List<ReportBucket>();
            var cursor = NaturalStart(from, granularity);

            while (cursor <= to)
            {
                var naturalEnd = NaturalEnd(cursor, granularity);
                var displayStart = cursor < from ? from : cursor;
                var displayEnd = naturalEnd > to ? to : naturalEnd;

                var isPartial = displayStart != cursor || displayEnd != naturalEnd;

                var measuredStartUtc = displayStart.LocalStartOfDayToUtc(timeZone);
                var measuredEndUtcExclusive = displayEnd.AddDays(1).LocalStartOfDayToUtc(timeZone);

                var hasData = dataAvailableSince.HasValue && displayEnd >= dataAvailableSince.Value;

                if (dataAvailableSince.HasValue)
                {
                    var dataAvailableSinceUtc = dataAvailableSince.Value.LocalStartOfDayToUtc(timeZone);
                    if (dataAvailableSinceUtc > measuredStartUtc)
                    {
                        measuredStartUtc = dataAvailableSinceUtc > measuredEndUtcExclusive ? measuredEndUtcExclusive : dataAvailableSinceUtc;
                        isPartial = true;
                    }
                }

                if (measuredEndUtcExclusive > nowUtc)
                {
                    var clipped = nowUtc < measuredStartUtc ? measuredStartUtc : nowUtc;
                    if (clipped != measuredEndUtcExclusive)
                    {
                        isPartial = true;
                    }

                    measuredEndUtcExclusive = clipped;
                }

                buckets.Add(new ReportBucket
                {
                    Start = displayStart,
                    End = displayEnd,
                    NaturalStart = cursor,
                    NaturalEnd = naturalEnd,
                    IsPartial = isPartial,
                    HasData = hasData,
                    MeasuredStartUtc = measuredStartUtc,
                    MeasuredEndUtcExclusive = measuredEndUtcExclusive,
                });

                cursor = NextNaturalStart(cursor, granularity);
            }

            return buckets;
        }

        private static DateOnly NaturalStart(DateOnly date, ReportGranularity granularity) => granularity switch
        {
            ReportGranularity.Day => date,
            ReportGranularity.Week => date.AddDays(-DaysSinceMonday(date)),
            ReportGranularity.Month => new DateOnly(date.Year, date.Month, 1),
            ReportGranularity.Quarter => new DateOnly(date.Year, QuarterStartMonth(date.Month), 1),
            _ => throw new ArgumentOutOfRangeException(nameof(granularity)),
        };

        private static DateOnly NaturalEnd(DateOnly naturalStart, ReportGranularity granularity) => granularity switch
        {
            ReportGranularity.Day => naturalStart,
            ReportGranularity.Week => naturalStart.AddDays(6),
            ReportGranularity.Month => naturalStart.AddMonths(1).AddDays(-1),
            ReportGranularity.Quarter => naturalStart.AddMonths(3).AddDays(-1),
            _ => throw new ArgumentOutOfRangeException(nameof(granularity)),
        };

        private static DateOnly NextNaturalStart(DateOnly naturalStart, ReportGranularity granularity) => granularity switch
        {
            ReportGranularity.Day => naturalStart.AddDays(1),
            ReportGranularity.Week => naturalStart.AddDays(7),
            ReportGranularity.Month => naturalStart.AddMonths(1),
            ReportGranularity.Quarter => naturalStart.AddMonths(3),
            _ => throw new ArgumentOutOfRangeException(nameof(granularity)),
        };

        private static int DaysSinceMonday(DateOnly date) => ((int)date.DayOfWeek + 6) % 7;

        private static int QuarterStartMonth(int month) => ((month - 1) / 3 * 3) + 1;
    }
}
