using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;

namespace TaskFlow.BusinessLogic.Cache
{
    public interface ICacheManager
    {
        Task<T?> GetCachedData<T>(string cacheKey);

        Task SetCachedData<T>(string cacheKey, T data, TimeSpan cacheDuration);

        Task RemoveCacheData(string cacheKey);
    }

    public class CacheManager(IDistributedCache distributedCache, ILogger<CacheManager> logger) : ICacheManager
    {
        public async Task<T?> GetCachedData<T>(string cacheKey)
        {
            try
            {
                var jsonData = await distributedCache.GetStringAsync(cacheKey);
                if (jsonData == null)
                    return default;

                return JsonConvert.DeserializeObject<T>(jsonData);
            }
            catch (Exception ex)
            {
                logger.LogError("Redis connection error: {error}", ex.Message);
                return default;
            }
        }

        public async Task RemoveCacheData(string cacheKey)
        {
            try
            {
                await distributedCache.RemoveAsync(cacheKey);
            }
            catch (Exception ex)
            {
                logger.LogError("Redis connection error: {error}", ex.Message);
            }
        }

        public async Task SetCachedData<T>(string cacheKey, T data, TimeSpan cacheDuration)
        {
            try
            {
                var options = new DistributedCacheEntryOptions
                {
                    AbsoluteExpirationRelativeToNow = cacheDuration
                };

                var jsonData = JsonConvert.SerializeObject(data);
                await distributedCache.SetStringAsync(cacheKey, jsonData, options);
            }
            catch (Exception ex)
            {
                logger.LogError("Redis connection error: {error}", ex.Message);
            }
        }
    }
}
