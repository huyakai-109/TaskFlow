using Microsoft.Extensions.Logging;
using Quartz;
using Quartz.Spi;

namespace TaskFlow.BusinessLogic.Schedulers
{
    public interface IScheduleService
    {
        Task<bool> ScheduleJob<T>(string id, string cronExpression, DateTimeOffset? endAt = null, Dictionary<string, string>? jobData = null, TimeZoneInfo? timeZone = null)
            where T : IJob;

        Task<bool> ScheduleJob<T>(string id, DateTimeOffset startAt, Dictionary<string, string>? jobData = null)
            where T : IJob;
    }

    public class ScheduleService(
        ILogger<ScheduleService> logger,
        ISchedulerFactory schedulerFactory,
        IJobFactory jobFactory) : IScheduleService
    {
        // f05-overdue.md Technical contract T5: a static, test-seamed helper so
        // a unit test can assert GetNextFireTimeUtc() without standing up a
        // scheduler.
        public static ITrigger BuildCronTrigger(string cronExpression, TimeZoneInfo? timeZone, TriggerKey? triggerKey = null, JobKey? jobKey = null, DateTimeOffset? endAt = null)
        {
            var builder = TriggerBuilder.Create();

            if (triggerKey != null)
            {
                builder = builder.WithIdentity(triggerKey);
            }

            if (jobKey != null)
            {
                builder = builder.ForJob(jobKey);
            }

            return builder
                // A cron trigger's own schedule decides fire times; TriggerBuilder's
                // default start time is "now" at build time, which would clamp
                // GetFireTimeAfter(...) to that instant and break deterministic
                // testing (and has no effect on real scheduling, since the cron
                // expression - not the start time - drives recurring fires).
                // DateTimeOffset.MinValue overflows Quartz's own
                // ComputeFirstFireTimeUtc (it computes StartTimeUtc.AddSeconds(-1)
                // when actually scheduling), so a fixed, safely-in-the-past epoch
                // is used instead.
                .StartAt(new DateTimeOffset(2000, 1, 1, 0, 0, 0, TimeSpan.Zero))
                .WithCronSchedule(cronExpression, b =>
                {
                    b.InTimeZone(timeZone ?? TimeZoneInfo.Utc);
                    b.WithMisfireHandlingInstructionFireAndProceed();
                })
                .EndAt(endAt)
                .Build();
        }

        public async Task<bool> ScheduleJob<T>(string id, string cronExpression, DateTimeOffset? endAt = null, Dictionary<string, string>? jobData = null, TimeZoneInfo? timeZone = null)
            where T : IJob
        {
            try
            {
                logger.LogInformation("Scheduling job {id} with cron expression ({cronExpression}): {typeName}",
                    id, cronExpression, typeof(T).Name);

                var scheduler = await schedulerFactory.GetScheduler();
                scheduler.JobFactory = jobFactory;

                var jobKey = new JobKey($"{id}.jobKey");
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

                var newTrigger = BuildCronTrigger(cronExpression, timeZone, triggerKey, jobKey, endAt);

                // T5/ADR 0003: schedule only when missing or changed. The old
                // delete-then-recreate raced between two starting cluster
                // nodes (one could delete a job running on another node) and
                // discarded the persisted trigger, so a run missed while every
                // node was down was never made up. Keeping an unchanged
                // trigger lets a missed fire run once the scheduler restarts.
                if (await scheduler.CheckExists(triggerKey))
                {
                    var existingTrigger = await scheduler.GetTrigger(triggerKey);
                    if (existingTrigger is ICronTrigger existingCronTrigger
                        && newTrigger is ICronTrigger newCronTrigger
                        && existingCronTrigger.CronExpressionString == newCronTrigger.CronExpressionString
                        && string.Equals(existingCronTrigger.TimeZone.Id, newCronTrigger.TimeZone.Id, StringComparison.Ordinal))
                    {
                        logger.LogInformation("Trigger unchanged, keeping existing schedule: {id}", id);
                        await scheduler.Start();
                        return true;
                    }
                }

                await scheduler.ScheduleJob(jobDetail, [newTrigger], replace: true);
                await scheduler.Start();

                logger.LogInformation("Scheduling complete for: {id}", id);

                return true;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Schedule job error");
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
                logger.LogError(ex, "Schedule job error");
                return false;
            }
        }
    }
}
