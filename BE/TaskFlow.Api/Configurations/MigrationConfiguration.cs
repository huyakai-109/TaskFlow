using TaskFlow.Common.Constants;
using TaskFlow.DataAccess.DbContexts;
using Microsoft.EntityFrameworkCore;

namespace TaskFlow.Api.Configurations
{
    public static class MigrationConfiguration
    {
        public static void RunMigration(this WebApplication app)
        {
            var autoMigration = app.Configuration.GetSection(ConfigKeys.AutoMigration).Get<bool>();
            if (!autoMigration)
            {
                return;
            }

            using var scope = app.Services.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<MyDbContext>();
            context.Database.Migrate();
        }

        public static void RunQuartzMigration(this WebApplication app)
        {
            var autoMigration = app.Configuration.GetSection(ConfigKeys.AutoMigration).Get<bool>();
            if (!autoMigration)
            {
                return;
            }

            using var scope = app.Services.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<QuartzContext>();
            context.Database.Migrate();
        }
    }
}
