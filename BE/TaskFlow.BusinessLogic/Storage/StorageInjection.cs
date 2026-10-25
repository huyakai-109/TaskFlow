using TaskFlow.Common.Constants;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Minio;

namespace TaskFlow.BusinessLogic.Storage
{
    public static class StorageInjection
    {
        public static void AddStorage(this IServiceCollection collection, IConfiguration configuration)
        {
            var endpoint = configuration[ConfigKeys.MinIO.Endpoint];
            var accessKey = configuration[ConfigKeys.MinIO.AccessKey];
            var secretKey = configuration[ConfigKeys.MinIO.SecretKey];
            var region = configuration[ConfigKeys.MinIO.Region];
            var secure = configuration.GetValue<bool>(ConfigKeys.MinIO.Secure);
            collection.AddMinio(configureClient => configureClient
                .WithEndpoint(endpoint)
                .WithCredentials(accessKey, secretKey)
                .WithSSL(secure)
                .WithRegion(region)
                .Build());

            collection.AddScoped<IStorageService, StorageService>();
        }
    }
}
