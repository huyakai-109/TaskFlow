using TaskFlow.BusinessLogic.Schedulers;
using TaskFlow.BusinessLogic.Schedulers.BackgroundJobs;
using TaskFlow.Common.Constants;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Quartz;

namespace TaskFlow.BusinessLogic.HostedServices
{
    // f05-overdue.md Technical contract T5 (D5): schedules OverdueScanJob on
    // the scaffold's existing Quartz scheduler at 00:05 in the configured
    // zone. Registered only inside ScheduleInjection.AddQuartzScheduler, so
    // the "Testing" host (which never calls that) never schedules this.
    public class OverdueScanHostedService(
        ILogger<OverdueScanHostedService> logger,
        IConfiguration configuration,
        IScheduleService scheduleService,
        ISchedulerFactory schedulerFactory) : IHostedService
    {
        // D5 fixes the time of day; only the zone is configuration (D4).
        private const string JobId = "OverdueScan";
        private const string CronExpression = "0 5 0 * * ?";

        public async Task StartAsync(CancellationToken cancellationToken)
        {
            var timeZoneName = configuration.GetSection(ConfigKeys.Workflow.TimeZone).Get<string>()!;
            var timeZone = TimeZoneInfo.FindSystemTimeZoneById(timeZoneName);

            var scheduled = await scheduleService.ScheduleJob<OverdueScanJob>(JobId, CronExpression, timeZone: timeZone);

            if (!scheduled)
            {
                logger.LogError("Failed to schedule {JobId}; the overdue scan will not run", JobId);
            }
        }

        public async Task StopAsync(CancellationToken cancellationToken)
        {
            var scheduler = await schedulerFactory.GetScheduler(cancellationToken);
            await scheduler.Shutdown(waitForJobsToComplete: true, cancellationToken);
        }
    }
}
