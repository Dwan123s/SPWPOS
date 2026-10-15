using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Data.Sqlite;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using SPWPOS.Infrastructure.Persistence;
namespace SPWPOS.TestSupport;

public class ApiFactory : WebApplicationFactory<Program>
{
    private readonly SqliteConnection connection = new("Data Source=:memory:");
    private readonly string? sqlConnection;
    private readonly string? sqlDatabase;
    private bool sqlDatabaseCreated;
    private bool sqlDatabaseDeleted;
    public TestClock Clock { get; } = new();
    public TestEmailService Email { get; } = new();
    public ApiFactory(bool useSqlServer = false)
    {
        if (!useSqlServer) connection.Open();
        else
        {
            var source = Environment.GetEnvironmentVariable("SPWPOS_TEST_SQLSERVER") ?? throw new InvalidOperationException("SPWPOS_TEST_SQLSERVER is required.");
            sqlDatabase = "SPWPOS_Tests_" + Guid.NewGuid().ToString("N");
            var builder = new SqlConnectionStringBuilder(source) { InitialCatalog = sqlDatabase };
            sqlConnection = builder.ConnectionString;
        }
    }
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Development");
        builder.ConfigureLogging(logging => logging.ClearProviders());
        builder.ConfigureTestServices(services => services.AddDataProtection().UseEphemeralDataProtectionProvider());
        builder.ConfigureAppConfiguration((_, config) =>
        {
            // Tests must never inherit workstation secrets, external providers or
            // live JSON watchers. Configuration disposal races otherwise obscure
            // the actual HTTP result when many hosts are started and stopped.
            config.Sources.Clear();
            config.AddInMemoryCollection(TestInfrastructure.Configuration());
        });
        builder.ConfigureServices(services =>
        {
            if (sqlConnection is null) TestInfrastructure.ReplacePersistence(services, connection);
            else TestInfrastructure.ReplaceSqlServerPersistence(services, sqlConnection);
            TestInfrastructure.ReplaceSecurityDependencies(services, Clock, Email);
        });
    }
    public async Task<HttpClient> ClientAsync()
    {
        var client = CreateClient(new WebApplicationFactoryClientOptions { BaseAddress = new Uri("https://localhost"), AllowAutoRedirect = false });
        await using var scope = Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        if (sqlConnection is null) await db.Database.EnsureCreatedAsync();
        else { sqlDatabaseCreated = true; await db.Database.MigrateAsync(); }
        return client;
    }
    protected override void Dispose(bool disposing)
    {
        if (disposing) { DeleteTestDatabase(); connection.Dispose(); }
        base.Dispose(disposing);
    }
    public override async ValueTask DisposeAsync()
    {
        DeleteTestDatabase(); await base.DisposeAsync().ConfigureAwait(false); await connection.DisposeAsync().ConfigureAwait(false);
    }
    private void DeleteTestDatabase()
    {
        if (sqlConnection is null || !sqlDatabaseCreated || sqlDatabaseDeleted) return;
        if (new SqlConnectionStringBuilder(sqlConnection).InitialCatalog != sqlDatabase || !sqlDatabase!.StartsWith("SPWPOS_Tests_", StringComparison.Ordinal))
            throw new InvalidOperationException("Refusing to delete a database outside the generated test namespace.");
        using var db = new ApplicationDbContext(new DbContextOptionsBuilder<ApplicationDbContext>().UseSqlServer(sqlConnection).Options);
        db.Database.EnsureDeleted(); sqlDatabaseDeleted = true;
    }
}
