using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using MySqlConnector;
using TaskFlow.Common.Constants;
using TaskFlow.DataAccess.DbContexts;

namespace TaskFlow.Tests.Fixtures
{
    // Guards every test run against the real "mydb": this fixture may only
    // migrate and clean a database whose name ends with "_test".
    public class DatabaseFixture : IAsyncLifetime
    {
        private static readonly MySqlServerVersion ServerVersion = new(new Version(8, 4, 0));

        public string ConnectionString { get; }

        public DatabaseFixture()
        {
            var configuration = new ConfigurationBuilder()
                .SetBasePath(AppContext.BaseDirectory)
                .AddJsonFile("appsettings.Testing.json", optional: false)
                .AddEnvironmentVariables()
                .Build();

            ConnectionString = configuration.GetSection(ConfigKeys.Databases.MainDb).Value
                ?? throw new InvalidOperationException("Test database connection string is not configured.");

            EnsureTestDatabase(ConnectionString);
        }

        public async Task InitializeAsync()
        {
            await using var context = CreateContext();
            await context.Database.MigrateAsync();
        }

        public Task DisposeAsync()
        {
            return Task.CompletedTask;
        }

        public MyDbContext CreateContext()
        {
            var options = new DbContextOptionsBuilder<MyDbContext>()
                .UseMySql(ConnectionString, ServerVersion)
                .Options;

            return new MyDbContext(options);
        }

        // Deletes rows from the given tables so tests don't depend on run order
        // or on data left behind by another test.
        public async Task ResetTablesAsync(params string[] tableNames)
        {
            EnsureTestDatabase(ConnectionString);

            await using var context = CreateContext();
            foreach (var tableName in tableNames)
            {
                // Table names come only from hardcoded literals passed by tests in this
                // project, never from external input, so string composition is safe here;
                // identifiers can't be passed as SQL parameters.
#pragma warning disable EF1002
                await context.Database.ExecuteSqlRawAsync($"DELETE FROM `{tableName}`");
#pragma warning restore EF1002
            }
        }

        private static void EnsureTestDatabase(string connectionString)
        {
            var databaseName = new MySqlConnectionStringBuilder(connectionString).Database;

            if (string.IsNullOrEmpty(databaseName) || !databaseName.EndsWith("_test", StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException(
                    $"Refusing to migrate or clean database '{databaseName}': the test fixture only operates " +
                    "on a database whose name ends with '_test'. This guard protects 'mydb' from test runs.");
            }
        }
    }

    [CollectionDefinition(Name)]
    public class DatabaseCollection : ICollectionFixture<DatabaseFixture>
    {
        public const string Name = "Database";
    }
}
