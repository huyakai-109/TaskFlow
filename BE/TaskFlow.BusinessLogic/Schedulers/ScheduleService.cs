using Microsoft.Extensions.Logging;
using Quartz;
using Quartz.Spi;

namespace TaskFlow.BusinessLogic.Schedulers
{
    public interface IScheduleService
    {
        Task<bool> ScheduleJob<T>(string id, string cronExpression, DateTimeOffset? endAt = null, Dictionary<string, string>? jobData = null)
            where T : IJob;

        Task<bool> ScheduleJob<T>(string id, DateTimeOffset startAt, Dictionary<string, string>? jobData = null)
            where T : IJob;
    }

    public class ScheduleService(
        ILogger<ScheduleService> logger,
        ISchedulerFactory schedulerFactory,
        IJobFactory jobFactory) : IScheduleService
    {
        public async Task<bool> ScheduleJob<T>(string id, string cronExpression, DateTimeOffset? endAt = null, Dictionary<string, string>? jobData = null)
            where T : IJob
        {
            try
            {
                logger.LogInformation("Scheduling job {id} with cron expression ({cronExpression}): {typeName}",
                    id, cronExpression, typeof(T).Name);

                var scheduler = await schedulerFactory.GetScheduler();
                scheduler.JobFactory = jobFactory;

                var jobKey = new JobKey($"{id}.jobKey");

                if (await scheduler.CheckExists(jobKey))
                {
                    logger.LogInformation("Delete previous job: {jobKey}", jobKey);
                    await scheduler.DeleteJob(jobKey);
                }

                var triggerKey = new TriggerKey($"{id}.triggerKey");

                var jobDetailBuilder = JobBuilder.Create<T>()
                    .WithIdentity(jobKey);

                if (jobData != null)
                {
                    foreach (var data in jobData)
                    {
                        jobDetailBuilder.UsingJobData(data.Key, data.Value);
                    }
                }

                var jobDetail = jobDetailBuilder.Build();

                var trigger = TriggerBuilder.Create()
                    .ForJob(jobDetail)
                    .WithIdentity(triggerKey)
                    .WithCronSchedule(cronExpression)
                    .EndAt(endAt)
                    .Build();

                await scheduler.ScheduleJob(jobDetail, trigger);
                await scheduler.Start();

                logger.LogInformation("Scheduling complete for: {id}", id);

                return true;
            }
            catch (Exception ex)
            {
                logger.LogError("Schedule job error: {ex}", ex);
                return false;
            }
        }

        public async Task<bool> ScheduleJob<T>(string id, DateTimeOffset startAt, Dictionary<string, string>? jobData = null)
            where T : IJob
        {
            try
            {
                logger.LogInformation("Scheduling job {id} at {startAt}: {typeName}",
                    id, startAt, typeof(T).Name);

                var scheduler = await schedulerFactory.GetScheduler();
                scheduler.JobFactory = jobFactory;

                var jobKey = new JobKey($"{id}.jobKey");
                var triggerKey = new TriggerKey($"{id}.triggerKey");

                if (await scheduler.CheckExists(jobKey))
                {
                    logger.LogInformation("Delete previous job: {jobKey}", jobKey);
                    await scheduler.DeleteJob(jobKey);
                }

                var jobDetailBuilder = JobBuilder.Create<T>()
                    .WithIdentity(jobKey)
                    .RequestRecovery();

                if (jobData != null)
                {
                    foreach (var data in jobData)
                    {
                        jobDetailBuilder.UsingJobData(data.Key, data.Value);
                    }
                }

                var jobDetail = jobDetailBuilder.Build();

                var trigger = TriggerBuilder.Create()
                    .ForJob(jobDetail)
                    .WithIdentity(triggerKey)
                    .StartAt(startAt)
                    .Build();

                await scheduler.ScheduleJob(jobDetail, trigger);
                await scheduler.Start();

                logger.LogInformation("Scheduling complete for: {id}", id);

                return true;
            }
            catch (Exception ex)
            {
                logger.LogError("Schedule job error: {ex}", ex);
                return false;
            }
        }
    }
}
