using System.Text.Json.Serialization;
using TaskFlow.Api.Configurations;
using TaskFlow.Api.Hubs;
using TaskFlow.Api.Presence;
using TaskFlow.Common.Constants;
using Serilog;

var builder = WebApplication.CreateBuilder(args);

var config = builder.Configuration;

builder.Services.AddControllers()
    .AddJsonOptions(o => o.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter()))
    .AddInvalidModelStateHandling();
builder.Services.AddCoreDependencies(config, skipQuartz: builder.Environment.IsEnvironment("Testing"));

builder.Services.AddApiVersionings();
builder.Services.AddSwaggers();
builder.Services.AddSecurity(config);

builder.AddMetricLogging(config);
builder.Services.AddMetricExporters(config);

var allowedOrigins = config.GetSection(ConfigKeys.Cors.AllowedOrigins).Get<string[]>();
if (allowedOrigins == null || allowedOrigins.Length == 0)
{
    throw new InvalidOperationException($"No CORS origin is configured. Set '{ConfigKeys.Cors.AllowedOrigins}'.");
}

builder.Services.AddCors(options => options
    .AddDefaultPolicy(policy => policy
        .AllowAnyMethod()
        .AllowAnyHeader()
        .AllowCredentials()
        .WithOrigins(allowedOrigins)
    ));

// f02-capacity.md Technical contract T3 (D1): the limit is never a literal at
// the call site - TaskService reads it from configuration once, so it must
// exist and be sane at startup, the same way the CORS allow-list is checked
// above.
var maxInProgressPerAssignee = config.GetSection(ConfigKeys.Workflow.MaxInProgressPerAssignee).Get<int?>();
if (maxInProgressPerAssignee is null or < 1)
{
    throw new InvalidOperationException($"'{ConfigKeys.Workflow.MaxInProgressPerAssignee}' must be configured with a value of at least 1.");
}

// f05-overdue.md Technical contract T4 (D4): "today" for the overdue scan and
// for every synchronous recompute comes from this zone, never the server
// clock - fail fast, the same way as the CORS and capacity-limit checks above.
var workflowTimeZone = config.GetSection(ConfigKeys.Workflow.TimeZone).Get<string>();
if (string.IsNullOrEmpty(workflowTimeZone))
{
    throw new InvalidOperationException($"'{ConfigKeys.Workflow.TimeZone}' must be configured.");
}

try
{
    TimeZoneInfo.FindSystemTimeZoneById(workflowTimeZone);
}
catch (Exception ex) when (ex is TimeZoneNotFoundException or InvalidTimeZoneException)
{
    throw new InvalidOperationException($"'{ConfigKeys.Workflow.TimeZone}' value '{workflowTimeZone}' is not a valid time zone.", ex);
}

// f08-accounts-profile.md "Password policy": fail fast the same way as the
// three settings above, rather than silently falling back to Identity's
// unconfigured default (6).
var passwordRequiredLength = config.GetSection(ConfigKeys.Security.Password.RequiredLength).Get<int?>();
if (passwordRequiredLength is null or < 1)
{
    throw new InvalidOperationException($"'{ConfigKeys.Security.Password.RequiredLength}' must be configured with a value of at least 1.");
}

builder.Services.AddAutoMapper(cfg =>
{
    cfg.AddMaps(typeof(Program).Assembly);
    cfg.LicenseKey = config["LuckyPennyLicense"];
});

// f07-presence.md "Server design": in-memory singletons - a single API
// instance this round (C1 deferred), no Redis backplane.
builder.Services.AddSingleton<IPresenceTracker, PresenceTracker>();
builder.Services.AddSingleton<IConnectionRateLimiter, ConnectionRateLimiter>();
builder.Services.AddSingleton<TaskFlow.BusinessLogic.Services.IUserPresenceNotifier, PresenceUserNotifier>();

builder.Services.AddSignalR(options =>
{
    // f07-presence.md "Server design": killed-tab detection within ~30s.
    options.KeepAliveInterval = TimeSpan.FromSeconds(15);
    options.ClientTimeoutInterval = TimeSpan.FromSeconds(30);
    options.MaximumReceiveMessageSize = 4 * 1024;
}).AddJsonProtocol(options =>
{
    options.PayloadSerializerOptions.PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.CamelCase;
    options.PayloadSerializerOptions.Converters.Add(new JsonStringEnumConverter());
});

builder.Host.UseSerilog((ctx, lc) => lc
    .ReadFrom.Configuration(ctx.Configuration)
    .Enrich.With<TaskFlow.Api.Logging.AccessTokenRedactionEnricher>());

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment() || app.Configuration.GetSection(ConfigKeys.EnableSwagger).Get<bool>())
{
    app.UseSwaggers();
}

app.UseCors();

app.UseHttpsRedirection();

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

// f07-presence.md "Authentication": CloseOnAuthenticationExpiration closes
// the connection when the 15-min access token expires rather than trusting
// it until the client notices; the client's accessTokenFactory reconnects
// with a fresh one (ADR 0004).
app.MapHub<PresenceHub>("/hubs/presence", options =>
{
    options.CloseOnAuthenticationExpiration = true;
});

app.UseMetricExporters();

// BE/TaskFlow.Tests's DatabaseFixture (an xUnit collection fixture, shared by
// every DB-touching test class in the "Database" collection) is the single
// migrator for "mydb_test" - it runs once before any test in that collection
// starts. A WebApplicationFactory-hosted app must not also migrate here: with
// several test classes each building their own host, concurrent
// Database.Migrate() calls raced to CREATE DATABASE and failed CI
// intermittently ("Can't create database 'mydb_test'; database exists").
if (!app.Environment.IsEnvironment("Testing"))
{
    app.RunMigration();
    app.RunQuartzMigration();
}

if (app.Environment.IsDevelopment())
{
    await app.SeedDevelopmentUsersAsync();
}

app.Run();

public partial class Program
{
}
