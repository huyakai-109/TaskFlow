using System.Text.Json.Serialization;
using TaskFlow.Api.Configurations;
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

builder.Services.AddAutoMapper(cfg =>
{
    cfg.AddMaps(typeof(Program).Assembly);
    cfg.LicenseKey = config["LuckyPennyLicense"];
});

builder.Host.UseSerilog((ctx, lc) => lc
    .ReadFrom.Configuration(ctx.Configuration));

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
