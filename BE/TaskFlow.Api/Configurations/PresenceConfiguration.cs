using System.Text.Json;
using System.Text.Json.Serialization;
using TaskFlow.Api.Hubs;
using TaskFlow.Api.Presence;
using TaskFlow.BusinessLogic.Ports;

namespace TaskFlow.Api.Configurations
{
    // f07-presence.md: SignalR presence (board + task:{id} rooms). Grouped
    // here, same Add*/Use* shape as MetricConfiguration, rather than left
    // inline in Program.cs.
    public static class PresenceConfiguration
    {
        public static void AddPresence(this IServiceCollection collection)
        {
            // In-memory singletons - a single API instance this round (C1
            // deferred), no Redis backplane.
            collection.AddSingleton<IPresenceTracker, PresenceTracker>();
            collection.AddSingleton<IConnectionRateLimiter, ConnectionRateLimiter>();
            collection.AddSingleton<IUserPresenceNotifier, PresenceUserNotifier>();

            collection.AddSignalR(options =>
            {
                // f07-presence.md "Server design": killed-tab detection
                // within ~30s.
                options.KeepAliveInterval = TimeSpan.FromSeconds(15);
                options.ClientTimeoutInterval = TimeSpan.FromSeconds(30);
                options.MaximumReceiveMessageSize = 4 * 1024;
            }).AddJsonProtocol(options =>
            {
                options.PayloadSerializerOptions.PropertyNamingPolicy = JsonNamingPolicy.CamelCase;
                options.PayloadSerializerOptions.Converters.Add(new JsonStringEnumConverter());
            });
        }

        public static void UsePresence(this WebApplication app)
        {
            // f07-presence.md "Authentication": CloseOnAuthenticationExpiration
            // closes the connection when the 15-min access token expires
            // rather than trusting it until the client notices; the
            // client's accessTokenFactory reconnects with a fresh one
            // (ADR 0004).
            app.MapHub<PresenceHub>("/hubs/presence", options =>
            {
                options.CloseOnAuthenticationExpiration = true;
            });
        }
    }
}
