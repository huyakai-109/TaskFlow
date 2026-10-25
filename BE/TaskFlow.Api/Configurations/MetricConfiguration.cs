using OpenTelemetry.Instrumentation.AspNetCore;
using OpenTelemetry.Logs;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;

namespace TaskFlow.Api.Configurations
{
    public static class MetricConfiguration
    {
        public static void AddMetricLogging(this WebApplicationBuilder builder, IConfiguration config)
        {
            builder.Logging
                .AddOpenTelemetry(options =>
                {
                    options.IncludeFormattedMessage = true;
                    options.IncludeScopes = true;

                    var resBuilder = ResourceBuilder.CreateDefault();
                    resBuilder.AddService(config.GetSection("ServiceName").Get<string>()!);
                    options.SetResourceBuilder(resBuilder);

                    options.AddOtlpExporter();
                });
        }

        public static void AddMetricExporters(this IServiceCollection collection, IConfiguration config)
        {
            collection.AddHealthChecks();
            collection.Configure<AspNetCoreTraceInstrumentationOptions>(options =>
            {
                options.Filter = ctx => ctx.Request.Path != "/metrics";
            });
            collection.AddOpenTelemetry()
                .ConfigureResource(builder =>
                {
                    builder.AddService(config.GetSection("ServiceName").Get<string>()!);
                })
                .WithTracing(builder =>
                {
                    builder.AddAspNetCoreInstrumentation();
                    builder.AddHttpClientInstrumentation();
                    builder.AddEntityFrameworkCoreInstrumentation();
                    builder.AddOtlpExporter();
                })
                .WithMetrics(builder =>
                {
                    builder.AddPrometheusExporter();
                    builder.AddAspNetCoreInstrumentation();
                    builder.AddHttpClientInstrumentation();
                    builder.AddRuntimeInstrumentation();
                    builder.AddProcessInstrumentation();
                    builder.AddMeter(
                        "Microsoft.AspNetCore.Hosting",
                        "Microsoft.AspNetCore.Routing",
                        "Microsoft.AspNetCore.Diagnostics",
                        "Microsoft.AspNetCore.RateLimiting",
                        "Microsoft.AspNetCore.HeaderParsing",
                        "Microsoft.AspNetCore.Server.Kestrel",
                        "Microsoft.AspNetCore.Http.Connections");
                });
        }

        public static void UseMetricExporters(this WebApplication app)
        {
            app.MapHealthChecks("/healthz");
            app.MapPrometheusScrapingEndpoint();
        }
    }
}
