using TaskFlow.BusinessLogic.ApiClient;
using TaskFlow.BusinessLogic.Cache;
using TaskFlow.BusinessLogic.HostedServices;
using TaskFlow.BusinessLogic.Schedulers;
using TaskFlow.BusinessLogic.Services;
using TaskFlow.BusinessLogic.Storage;
using TaskFlow.Repositories.UoW;

namespace TaskFlow.Api.Configurations
{
    public static class CoreDependencyConfiguration
    {
        public static void AddCoreDependencies(this IServiceCollection collection, IConfiguration configuration)
        {
            collection.AddHttpContextAccessor();
            collection.AddServices();
            collection.AddUnitOfWork(configuration);
            collection.AddCacheService(configuration);

            collection.AddApiClient();
            collection.AddQuartzScheduler(configuration);
            collection.AddHostedServices();
            collection.AddStorage(configuration);
        }

        private static void AddServices(this IServiceCollection collection)
        {
            collection.AddScoped<IAuthService, AuthService>();
            collection.AddScoped<IExampleService, ExampleService>();
        }

        private static void AddHostedServices(this IServiceCollection collection)
        {
            collection.AddHostedService<ExampleHostedService>();
        }
    }
}
