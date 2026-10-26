using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;

namespace TaskFlow.Tests.Http
{
    // Boots the real Program.cs under the "Testing" environment so the R1 fix
    // (envelope on model-validation failure) is exercised end to end, without
    // ever touching the real Quartz scheduler/database (see item 4, CoreDependencyConfiguration).
    public class TestWebApplicationFactory : WebApplicationFactory<Program>
    {
        // Program.cs reads builder.Configuration and validates Cors:AllowedOrigins
        // before WebApplicationBuilder.Build() runs, so a WebApplicationFactory
        // config/environment hook (which only replays once Build() is reached)
        // is too late. Real process environment variables, set before the host
        // starts, are the only override point that early code sees.
        public TestWebApplicationFactory()
        {
            Environment.SetEnvironmentVariable("ASPNETCORE_ENVIRONMENT", "Testing");
            Environment.SetEnvironmentVariable("Cors__AllowedOrigins__0", "http://localhost");
        }

        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.UseEnvironment("Testing");
        }
    }
}
