using System.Text;
using TaskFlow.Common.Helpers;
using Microsoft.Extensions.Logging;

namespace TaskFlow.BusinessLogic.ApiClient
{
    public interface IApiService
    {
        Task<TResult?> Get<TResult>(string url, Dictionary<string, string>? headers = null, int timeout = 30, CancellationToken? token = null, bool force = false)
            where TResult : class;

        Task<TResult?> Post<TResult>(string url, object? rawData, Dictionary<string, string>? headers = null, int timeout = 30, CancellationToken? token = null, bool force = false)
            where TResult : class;
    }

    public class ApiService(
        HttpClient httpClient,
        ILogger<ApiService> logger) : IApiService
    {
        public async Task<TResult?> Get<TResult>(string url, Dictionary<string, string>? headers = null, int timeout = 30, CancellationToken? token = null, bool force = false)
            where TResult : class
        {
            var client = !force ? httpClient : new HttpClient();

            if (force)
            {
                client.Timeout = TimeSpan.FromSeconds(timeout);
            }

            ApplyHeaders(client, headers);

            var response = token == null
                ? await client.GetAsync(url)
                : await client.GetAsync(url, token.Value);

            TResult? result = null;
            if (response.IsSuccessStatusCode)
            {
                var resultAsString = await response.Content.ReadAsStringAsync();

                if (typeof(TResult) == typeof(string))
                {
                    result = (TResult)(object)resultAsString;
                }
                else
                {
                    result = resultAsString.ToObject<TResult>();
                }
            }
            else
            {
                logger.LogError("Request failed (Status: {statusCode}): {responseJson}", response.StatusCode, response.ToJson());
            }

            return result;
        }

        public async Task<TResult?> Post<TResult>(string url, object? rawData, Dictionary<string, string>? headers = null, int timeout = 30, CancellationToken? token = null, bool force = false)
            where TResult : class
        {
            var client = !force ? httpClient : new HttpClient();

            if (force)
            {
                client.Timeout = TimeSpan.FromSeconds(timeout);
            }

            ApplyHeaders(client, headers);

            var jData = rawData.ToJson();
            var content = new StringContent(jData, Encoding.UTF8, "application/json");

            var response = token == null
                ? await client.PostAsync(url, content)
                : await client.PostAsync(url, content, token.Value);

            TResult? result = null;
            if (response.IsSuccessStatusCode)
            {
                var resultAsString = await response.Content.ReadAsStringAsync();

                if (typeof(TResult) == typeof(string))
                {
                    result = (TResult)(object)resultAsString;
                }
                else
                {
                    result = resultAsString.ToObject<TResult>();
                }
            }
            else
            {
                logger.LogError("Request failed (Status: {statusCode}): {responseJson}", response.StatusCode, response.ToJson());
            }

            return result;
        }

        private static void ApplyHeaders(HttpClient client, Dictionary<string, string>? headers = null)
        {
            if (headers == null || headers.Count == 0) return;

            client.DefaultRequestHeaders.Clear();

            foreach (var header in headers)
            {
                client.DefaultRequestHeaders.Add(header.Key, header.Value);
            }
        }
    }
}
