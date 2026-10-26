using TaskFlow.BusinessLogic.ApiClient;
using TaskFlow.BusinessLogic.Cache;
using TaskFlow.BusinessLogic.Schedulers;
using TaskFlow.BusinessLogic.Services;
using TaskFlow.BusinessLogic.Storage;
using TaskFlow.Repositories.UoW;

namespace TaskFlow.Api.Configurations
{
    public static class CoreDependencyConfiguration
    {
        // skipQuartz: the "Testing" host (WebApplicationFactory) must never touch
        // the real Quartz scheduler or the "quartz" database.
        public static void AddCoreDependencies(this IServiceCollection collection, IConfiguration configuration, bool skipQuartz = false)
        {
            collection.AddHttpContextAccessor();
            collection.AddServices();
            collection.AddUnitOfWork(configuration);
            collection.AddCacheService(configuration);

            collection.AddApiClient();
            if (!skipQuartz)
            {
                collection.AddQuartzScheduler(configuration);
            }

            collection.AddStorage(configuration);
        }

        private static void AddServices(this IServiceCollection collection)
        {
            collection.AddScoped<IAuthService, AuthService>();
            collection.AddScoped<IExampleService, ExampleService>();
        }
    }
}
