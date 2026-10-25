using TaskFlow.Common.Constants;
using TaskFlow.DataAccess.DbContexts;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace TaskFlow.Repositories.UoW
{
    public static class UnitOfWorkInjection
    {
        public static void AddUnitOfWork(this IServiceCollection collection, IConfiguration configuration)
        {
            var connectionString = configuration.GetSection(ConfigKeys.Databases.MainDb).Value;
            if (string.IsNullOrEmpty(connectionString))
            {
                throw new ArgumentNullException(nameof(connectionString), "Database connection string is null");
            }

            var serverVersion = new MySqlServerVersion(new Version(8, 4, 0));

            collection.AddDbContext<MyDbContext>(options =>
                options.UseMySql(connectionString, serverVersion));

            collection.AddScoped<IUnitOfWork, UnitOfWork>();
        }
    }
}
