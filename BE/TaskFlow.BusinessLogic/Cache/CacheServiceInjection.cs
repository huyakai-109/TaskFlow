using TaskFlow.Common.Constants;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace TaskFlow.BusinessLogic.Cache
{
    public static class CacheServiceInjection
    {
        public static void AddCacheService(this IServiceCollection collection, IConfiguration configuration)
        {
            collection.AddScoped<ICacheManager, CacheManager>();

            collection.AddRedis(configuration);
        }

        private static void AddRedis(this IServiceCollection collection, IConfiguration configuration)
        {
            var connectionString = configuration.GetSection(ConfigKeys.Databases.Redis).Value;
            collection.AddStackExchangeRedisCache(options =>
            {
                options.Configuration = connectionString;
                options.InstanceName = "MyInstance";
            });
        }
    }
}
