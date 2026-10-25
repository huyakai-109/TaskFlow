using System.Globalization;
using TaskFlow.Common.Constants;

namespace TaskFlow.Common.Helpers
{
    public static class DateTimeHelper
    {
        /// <summary>
        /// Get current date time
        /// </summary>
        /// <returns>DateTime</returns>
        public static DateTime GetDt()
        {
            return DateTime.UtcNow;
        }

        /// <summary>
        /// Get current date time utc
        /// </summary>
        /// <returns>DateTimeOffset</returns>
        public static DateTimeOffset GetDtOffset()
        {
            return DateTimeOffset.UtcNow;
        }

        /// <summary>
        /// Convert date time to format
        /// </summary>
        /// <param name="source">Source</param>
        /// <param name="format">Format</param>
        /// <returns>Text of converted date time</returns>
        public static string ToFormat(this DateTime source, string format = DateTimeFormats.Date)
        {
            return source.ToString(format, CultureInfo.InvariantCulture);
        }

        /// <summary>
        /// Convert date time offset to format
        /// </summary>
        /// <param name="source">Source</param>
        /// <param name="format">Format</param>
        /// <returns>Text of converted date time offset</returns>
        public static string ToFormat(this DateTimeOffset source, string format = DateTimeFormats.Date)
        {
            return source.ToString(format, CultureInfo.InvariantCulture);
        }

        /// <summary>
        /// Parse a text to utc date with format
        /// </summary>
        /// <param name="source">Source</param>
        /// <param name="result">Parse result</param>
        /// <param name="format">Format</param>
        /// <returns>Parse success/fail</returns>
        public static bool TryParseUtc(this string source, out DateTimeOffset result, string format = DateTimeFormats.Date)
        {
            return DateTimeOffset.TryParseExact(
                source,
                format,
                null,
                DateTimeStyles.AssumeUniversal,
                out result);
        }

        /// <summary>
        /// Parse a text to date with format
        /// </summary>
        /// <param name="source">Source</param>
        /// <param name="result">Parse result</param>
        /// <param name="format">Format</param>
        /// <returns>Parse success/fail</returns>
        public static bool TryParse(this string source, out DateTimeOffset result, string format = DateTimeFormats.Date)
        {
            return DateTimeOffset.TryParseExact(
                source,
                format,
                CultureInfo.InvariantCulture,
                DateTimeStyles.None,
                out result);
        }

        /// <summary>
        /// Get minutes between 2 date time offset
        /// </summary>
        /// <param name="from">From date time</param>
        /// <param name="to">To date time</param>
        /// <returns>Minutes</returns>
        public static double GetTotalMinutes(this DateTimeOffset from, DateTimeOffset to)
        {
            return (to - from).TotalMinutes;
        }

        /// <summary>
        /// Convert to timezone
        /// </summary>
        /// <param name="source">Source</param>
        /// <param name="timezoneName">Timezone</param>
        /// <returns>Converted datetime</returns>
        public static DateTimeOffset ToTimezone(this DateTimeOffset source, string timezoneName)
        {
            var timezone = TimeZoneInfo.FindSystemTimeZoneById(timezoneName);
            return TimeZoneInfo.ConvertTime(source, timezone);
        }

        /// <summary>
        /// Convert the Unix timestamp to a DateTime
        /// </summary>
        /// <param name="timestampObj">TimeObj</param>
        /// <returns>Converted datetime</returns>
        public static DateTimeOffset ConvertUnixTimestampToDateTime(object timestampObj)
        {
            var timestamp = Convert.ToInt64(timestampObj);
            return DateTimeOffset.FromUnixTimeSeconds(timestamp);
        }
    }
}
