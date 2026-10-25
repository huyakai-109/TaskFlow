using TaskFlow.BusinessLogic.Schedulers;
using TaskFlow.BusinessLogic.Schedulers.BackgroundJobs;
using Microsoft.Extensions.Hosting;

namespace TaskFlow.BusinessLogic.HostedServices
{
    public class ExampleHostedService(IScheduleService scheduleService) : BackgroundService
    {
        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            await scheduleService.ScheduleJob<ExampleJob>($"ExampleJob",
                DateTime.Now.AddMinutes(1)); ;
        }
    }
}
