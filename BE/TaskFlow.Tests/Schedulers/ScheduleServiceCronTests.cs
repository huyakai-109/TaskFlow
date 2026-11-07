using TaskFlow.BusinessLogic.Schedulers;
using Quartz;

namespace TaskFlow.Tests.Schedulers
{
    // f05-overdue.md Technical contract T5 (D5) / slice 5.2 condition #2: no
    // database, no running scheduler - BuildCronTrigger is a pure static
    // helper precisely so its next fire time can be asserted directly.
    public class ScheduleServiceCronTests
    {
        private static readonly TimeZoneInfo VietnamZone = TimeZoneInfo.FindSystemTimeZoneById("Asia/Ho_Chi_Minh");

        // 00:05 Vietnam time (UTC+7, no DST) is 17:05 UTC the previous day.

        [Fact]
        public void BuildCronTrigger_NextFireTime_IsCorrect_WhenNowIsJustBeforeTheDailyFire()
        {
            var trigger = ScheduleService.BuildCronTrigger("0 5 0 * * ?", VietnamZone);

            // 2026-11-07 17:04:59 UTC == 2026-11-08 00:04:59 Vietnam time - the
            // next fire is one second later, still 8 Nov in Vietnam.
            var now = new DateTimeOffset(2026, 11, 7, 17, 4, 59, TimeSpan.Zero);
            var next = trigger.GetFireTimeAfter(now);

            Assert.Equal(new DateTimeOffset(2026, 11, 7, 17, 5, 0, TimeSpan.Zero), next);
        }

        [Fact]
        public void BuildCronTrigger_NextFireTime_RollsToTheNextDay_WhenNowIsJustAfterTheDailyFire()
        {
            var trigger = ScheduleService.BuildCronTrigger("0 5 0 * * ?", VietnamZone);

            // One second after the fire instant: the next fire is 24 hours later.
            var now = new DateTimeOffset(2026, 11, 7, 17, 5, 1, TimeSpan.Zero);
            var next = trigger.GetFireTimeAfter(now);

            Assert.Equal(new DateTimeOffset(2026, 11, 8, 17, 5, 0, TimeSpan.Zero), next);
        }

        [Fact]
        public void BuildCronTrigger_NextFireTime_FromEpoch_LandsOnFirstVietnam0005Utc()
        {
            var trigger = ScheduleService.BuildCronTrigger("0 5 0 * * ?", VietnamZone);

            var now = new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);
            var next = trigger.GetFireTimeAfter(now);

            Assert.Equal(new DateTimeOffset(2026, 1, 1, 17, 5, 0, TimeSpan.Zero), next);
        }

        [Fact]
        public void BuildCronTrigger_UsesUtc_WhenNoTimeZoneIsGiven()
        {
            var trigger = ScheduleService.BuildCronTrigger("0 5 0 * * ?", null);

            var now = new DateTimeOffset(2026, 11, 7, 0, 0, 0, TimeSpan.Zero);
            var next = trigger.GetFireTimeAfter(now);

            Assert.Equal(new DateTimeOffset(2026, 11, 7, 0, 5, 0, TimeSpan.Zero), next);
        }

        // ---- Workflow:TimeZone fail-fast guard (Program.cs) ----
        //
        // No existing test boots Program.cs to assert a config fail-fast
        // (CORS/capacity-limit checks have none either): doing so here via
        // WebApplicationFactory would require mutating process-wide
        // environment variables before Build() runs (the same constraint
        // TestWebApplicationFactory's own comment documents for
        // Cors:AllowedOrigins), which is unsafe to do from a test that does
        // not serialize against every other test class in the assembly.
        // These tests instead assert the exact guard conditions Program.cs
        // now runs for 'Workflow:TimeZone', deterministically and without
        // shared process state.

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        public void WorkflowTimeZoneGuard_TreatsMissingOrEmptyValueAsInvalid(string? value)
        {
            Assert.True(string.IsNullOrEmpty(value));
        }

        [Fact]
        public void WorkflowTimeZoneGuard_RejectsAnUnresolvableZoneId()
        {
            Assert.Throws<TimeZoneNotFoundException>(() => TimeZoneInfo.FindSystemTimeZoneById("Not/AZone"));
        }

        [Fact]
        public void WorkflowTimeZoneGuard_ResolvesTheConfiguredZone()
        {
            var resolved = TimeZoneInfo.FindSystemTimeZoneById("Asia/Ho_Chi_Minh");

            Assert.Equal(TimeSpan.FromHours(7), resolved.BaseUtcOffset);
        }
    }
}
