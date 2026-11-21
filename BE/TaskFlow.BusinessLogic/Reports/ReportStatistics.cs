using TaskFlow.BusinessLogic.Dtos.Reports;
using TaskFlow.Common.Constants;

namespace TaskFlow.BusinessLogic.Reports
{
    // f09-reports.md R5 (median/p85) and R16 (small-sample rules). Pure,
    // reused by ReportCalculator for cycleTime/leadTime/reviewWait and for
    // the team median (R12, same median rule over per-member values).
    public static class ReportStatistics
    {
        // Conventional median: for even n, the mean of the two middle
        // values truncated to a whole number (R5). Values are durations in
        // whole seconds, always >= 0, so truncation is integer division.
        public static long? Median(IReadOnlyList<long> values)
        {
            if (values.Count == 0)
            {
                return null;
            }

            var sorted = values.OrderBy(v => v).ToList();
            var n = sorted.Count;
            var mid = n / 2;

            return n % 2 == 1
                ? sorted[mid]
                : (sorted[mid - 1] + sorted[mid]) / 2;
        }

        // Nearest rank: rank = ceil(0.85 * n), 1-indexed into the ascending
        // list. Null whenever n < SmallSampleThreshold (R16), never a mean.
        public static long? P85(IReadOnlyList<long> values)
        {
            if (values.Count < ReportRules.SmallSampleThreshold)
            {
                return null;
            }

            var sorted = values.OrderBy(v => v).ToList();
            var rank = (int)Math.Ceiling(0.85 * sorted.Count);
            rank = Math.Clamp(rank, 1, sorted.Count);

            return sorted[rank - 1];
        }

        public static ReportDistributionDto BuildDistribution(IReadOnlyList<long> values)
        {
            var n = values.Count;

            return new ReportDistributionDto
            {
                SampleSize = n,
                MedianSeconds = Median(values),
                P85Seconds = P85(values),
                LowSample = n > 0 && n < ReportRules.SmallSampleThreshold,
            };
        }

        // Rate: null value when the denominator is 0; never suppressed
        // otherwise (R16 - counts, and here denominators, are never
        // suppressed). Rounded to 4 decimal places.
        public static ReportRateDto BuildRate(int numerator, int denominator)
        {
            return new ReportRateDto
            {
                Value = denominator == 0 ? null : Math.Round((double)numerator / denominator, 4),
                Denominator = denominator,
                LowSample = denominator > 0 && denominator < ReportRules.SmallSampleThreshold,
            };
        }

        public static double? MedianOfDoubles(IReadOnlyList<double> values)
        {
            if (values.Count == 0)
            {
                return null;
            }

            var sorted = values.OrderBy(v => v).ToList();
            var n = sorted.Count;
            var mid = n / 2;

            return n % 2 == 1
                ? sorted[mid]
                : (sorted[mid - 1] + sorted[mid]) / 2.0;
        }
    }
}
