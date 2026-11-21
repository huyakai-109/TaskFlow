using System.Text.Json.Serialization;
using TaskFlow.Api.Configurations;
using TaskFlow.Api.Logging;
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

builder.Services.AddCorsPolicy(config);
config.ValidateStartupSettings();

builder.Services.AddAutoMapper(cfg =>
{
    cfg.AddMaps(typeof(Program).Assembly);
    cfg.LicenseKey = config["LuckyPennyLicense"];
});

builder.Services.AddPresence();

builder.Host.UseSerilog((ctx, lc) => lc
    .ReadFrom.Configuration(ctx.Configuration)
    .Enrich.With<AccessTokenRedactionEnricher>());

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

app.UsePresence();

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
