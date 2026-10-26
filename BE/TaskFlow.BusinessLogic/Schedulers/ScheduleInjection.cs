using System.Collections.Specialized;
using TaskFlow.Common.Constants;
using TaskFlow.DataAccess.DbContexts;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Quartz;
using Quartz.Spi;

namespace TaskFlow.BusinessLogic.Schedulers
{
    public static class ScheduleInjection
    {
        public static void AddQuartzScheduler(this IServiceCollection collection, IConfiguration configuration)
        {
            collection.AddJobStore(configuration);
            collection.ConfigureSchedulerFactory(configuration);

            collection.AddSingleton<IScheduleService, ScheduleService>();
            collection.AddSingleton<IJobFactory, JobFactory>();
        }

        private static void ConfigureSchedulerFactory(this IServiceCollection collection, IConfiguration configuration)
        {
            var connectionString = configuration.GetSection(ConfigKeys.Databases.Quartz).Value;
            if (string.IsNullOrEmpty(connectionString))
            {
                throw new ArgumentNullException(nameof(connectionString), "Quartz database connection string is null");
            }

            var properties = new NameValueCollection
            {
                ["quartz.scheduler.instanceName"] = "ClusteredScheduler",
                ["quartz.scheduler.instanceId"] = "AUTO",

                ["quartz.threadPool.type"] = "Quartz.Simpl.DefaultThreadPool, Quartz",
                ["quartz.threadPool.threadCount"] = "25",

                ["quartz.jobStore.useProperties"] = "true",
                ["quartz.jobStore.type"] = "Quartz.Impl.AdoJobStore.JobStoreTX, Quartz",
                ["quartz.jobStore.driverDelegateType"] = "Quartz.Impl.AdoJobStore.StdAdoDelegate, Quartz",
                ["quartz.jobStore.tablePrefix"] = "QRTZ_",
                ["quartz.jobStore.dataSource"] = "myDS",
                ["quartz.dataSource.myDS.connectionString"] = connectionString,
                ["quartz.dataSource.myDS.provider"] = "MySql",
                ["quartz.jobStore.clustered"] = "true",

                ["quartz.serializer.type"] = "newtonsoft",
            };

            var schedulerFactory = SchedulerBuilder.Create(properties).Build();

            collection.AddSingleton<ISchedulerFactory>(_ => schedulerFactory);
        }

        private static void AddJobStore(this IServiceCollection collection, IConfiguration configuration)
        {
            var connectionString = configuration.GetSection(ConfigKeys.Databases.Quartz).Value;
            if (string.IsNullOrEmpty(connectionString))
            {
                throw new ArgumentNullException(nameof(connectionString), "Quartz database connection string is null");
            }
            var serverVersion = new MySqlServerVersion(new Version(8, 4, 0));

            collection.AddDbContext<QuartzContext>(options =>
                options.UseMySql(connectionString, serverVersion));
        }
    }
}
