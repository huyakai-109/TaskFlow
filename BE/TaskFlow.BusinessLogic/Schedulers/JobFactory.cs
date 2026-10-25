using Microsoft.Extensions.Logging;
using Quartz;
using Quartz.Spi;

namespace TaskFlow.BusinessLogic.Schedulers
{
    public class JobFactory(
        ILogger<JobFactory> logger,
        IServiceProvider serviceProvider) : IJobFactory
    {

        /// <summary>
        /// Add transient for IJob
        /// </summary>
        public IJob NewJob(TriggerFiredBundle bundle, IScheduler scheduler)
        {
            var job = serviceProvider.GetService(bundle.JobDetail.JobType);
            return (IJob)job!;
        }

        public void ReturnJob(IJob job)
        {
            logger.LogTrace("Job {jobName} complete!", job.GetType());
        }
    }
}
