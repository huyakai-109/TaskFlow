using TaskFlow.Api.Configurations;
using TaskFlow.Common.Constants;
using Serilog;

var builder = WebApplication.CreateBuilder(args);

var config = builder.Configuration;

builder.Services.AddControllers();
builder.Services.AddCoreDependencies(config);

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

app.RunMigration();
app.RunQuartzMigration();

app.Run();
