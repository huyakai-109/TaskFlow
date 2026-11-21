using TaskFlow.Common.Constants;

namespace TaskFlow.Api.Configurations
{
    public static class CorsConfiguration
    {
        // Fails fast rather than allowing any origin - never reintroduce
        // any-origin together with credentials (coding-standards.md Section 6).
        public static void AddCorsPolicy(this IServiceCollection collection, IConfiguration configuration)
        {
            var allowedOrigins = configuration.GetSection(ConfigKeys.Cors.AllowedOrigins).Get<string[]>();
            if (allowedOrigins == null || allowedOrigins.Length == 0)
            {
                throw new InvalidOperationException($"No CORS origin is configured. Set '{ConfigKeys.Cors.AllowedOrigins}'.");
            }

            collection.AddCors(options => options
                .AddDefaultPolicy(policy => policy
                    .AllowAnyMethod()
                    .AllowAnyHeader()
                    .AllowCredentials()
                    .WithOrigins(allowedOrigins)
                ));
        }
    }
}
