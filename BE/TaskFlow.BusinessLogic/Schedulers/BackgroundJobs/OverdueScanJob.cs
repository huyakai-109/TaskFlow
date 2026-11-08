using TaskFlow.BusinessLogic.Services;
using TaskFlow.Common.Constants;
using TaskFlow.Common.Helpers;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Quartz;

namespace TaskFlow.BusinessLogic.Schedulers.BackgroundJobs
{
    // f05-overdue.md Technical contract T5 (D5, AC24): the daily overdue scan,
    // scheduled by OverdueScanHostedService. [DisallowConcurrentExecution]
    // plus the clustered ADO job store means the scan body runs once per
    // trigger fire across the whole cluster, never once per node.
    //
    // Constructor dependencies are singletons only - JobFactory resolves jobs
    // from the root service provider (no per-request scope), so a scoped
    // dependency taken directly here would be captured for the job's whole
    // lifetime. Scoped work happens inside the scope created in Execute.
    [DisallowConcurrentExecution]
    public class OverdueScanJob(
        ILogger<OverdueScanJob> logger,
        IConfiguration configuration,
        IServiceScopeFactory scopeFactory) : IJob
    {
        public async Task Execute(IJobExecutionContext context)
        {
            try
            {
                var timeZoneName = configuration.GetSection(ConfigKeys.Workflow.TimeZone).Get<string>()!;
                var today = DateTimeHelper.GetDtOffset().ToDateInZone(timeZoneName);

                await using var scope = scopeFactory.CreateAsyncScope();
                var scanService = scope.ServiceProvider.GetRequiredService<ITaskOverdueScanService>();

                await scanService.Run(today, context.CancellationToken);
            }
            catch (Exception ex)
            {
                // No rethrow: a Quartz refire loop is not wanted. AC19 already
                // covers a missed run - it is caught up the next time the job
                // fires, at most 24 hours later.
                logger.LogError(ex, "Overdue scan failed");
            }
        }
    }
}
