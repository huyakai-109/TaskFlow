using Microsoft.Extensions.Logging;
using Quartz;

namespace TaskFlow.BusinessLogic.Schedulers.BackgroundJobs
{
    [DisallowConcurrentExecution]
    public class ExampleJob(ILogger<ExampleJob> logger) : IJob
    {
        public async Task Execute(IJobExecutionContext context)
        {
            while (true)
            {
                logger.LogInformation("This is from ExampleJob");
                await Task.Delay(TimeSpan.FromSeconds(10));
            }
        }
    }
}
