using TaskFlow.Common.Helpers;

namespace TaskFlow.Tests.Common
{
    // R-TZ regression: with InvariantGlobalization left on, FindSystemTimeZoneById
    // throws TimeZoneNotFoundException on this machine (confirmed by experiment,
    // f01-workflow.md T7). TaskFlow.Api.csproj now sets it to false.
    public class DateTimeHelperTimeZoneTests
    {
        [Fact]
        public void ToTimezone_ResolvesAsiaHoChiMinh_WithoutThrowing()
        {
            // Arrange
            var utcNow = DateTimeOffset.UtcNow;

            // Act
            var converted = utcNow.ToTimezone("Asia/Ho_Chi_Minh");

            // Assert
            Assert.Equal(TimeSpan.FromHours(7), converted.Offset);
        }
    }
}
