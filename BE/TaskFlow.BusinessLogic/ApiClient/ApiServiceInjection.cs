using Microsoft.Extensions.DependencyInjection;

namespace TaskFlow.BusinessLogic.ApiClient
{
    public static class ApiServiceInjection
    {
        public static void AddApiClient(this IServiceCollection collection)
        {
            collection.AddHttpClient<IApiService, ApiService>();
        }
    }
}
