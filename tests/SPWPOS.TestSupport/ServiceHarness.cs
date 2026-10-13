using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using SPWPOS.Application.Authentication.DTOs;
using SPWPOS.Application.Authentication.Interfaces;
using SPWPOS.Domain.Entities;
using SPWPOS.Domain.Enums;
using SPWPOS.Infrastructure;
using SPWPOS.Infrastructure.Persistence;
namespace SPWPOS.TestSupport;

public sealed class ServiceHarness : IAsyncDisposable
{
    public const string Address = "person@example.com";
    public const string UserName = "test.member";
    public const string Password = "Rainy morning with coffee 42!";
    public const string NewPassword = "An entirely new passphrase 83!";
    public static readonly RequestContext Context = new("127.0.0.1", "SPWPOS.Tests");
    private readonly SqliteConnection connection = new("Data Source=:memory:");
    public TestClock Clock { get; } = new();
    public TestEmailService Email { get; } = new();
    public ServiceProvider Provider { get; private set; } = null!;
    public static async Task<ServiceHarness> CreateAsync(Action<IServiceCollection>? configure = null)
    {
        var h = new ServiceHarness(); await h.connection.OpenAsync();
        var services = new ServiceCollection(); services.AddLogging();
        var config = new ConfigurationBuilder().AddInMemoryCollection(TestInfrastructure.Configuration()).Build();
        services.AddSingleton<IConfiguration>(config); services.AddInfrastructure(config);
        TestInfrastructure.ReplacePersistence(services, h.connection);
        TestInfrastructure.ReplaceSecurityDependencies(services, h.Clock, h.Email);
        configure?.Invoke(services);
        h.Provider = services.BuildServiceProvider();
        await using var scope = h.Provider.CreateAsyncScope();
        await scope.ServiceProvider.GetRequiredService<ApplicationDbContext>().Database.EnsureCreatedAsync();
        return h;
    }
    public async Task<T> AuthAsync<T>(Func<IAuthService, Task<T>> action)
    {
        await using var scope = Provider.CreateAsyncScope();
        return await action(scope.ServiceProvider.GetRequiredService<IAuthService>());
    }
    public Task AuthAsync(Func<IAuthService, Task> action) => AuthAsync(async auth => { await action(auth); return true; });
    public async Task<ApplicationUser> RegisterAsync(bool verified = true)
    {
        await AuthAsync(auth => auth.RegisterAsync(new(Address, Password, Password, "Nguyen", "Van A", UserName), Context, CancellationToken.None));
        if (verified) await AuthAsync(auth => auth.VerifyEmailAsync(new(Address, Email.Inbox[(Address, OtpPurpose.EmailVerification)]), Context, CancellationToken.None));
        return await DbAsync(db => db.Users.AsNoTracking().SingleAsync());
    }
    public Task<TokenResponse> LoginAsync(string password = Password) => AuthAsync(auth => auth.LoginAsync(new(Address, password), Context, CancellationToken.None));
    public async Task<T> DbAsync<T>(Func<ApplicationDbContext, Task<T>> action)
    {
        await using var scope = Provider.CreateAsyncScope();
        return await action(scope.ServiceProvider.GetRequiredService<ApplicationDbContext>());
    }
    public Task DbAsync(Func<ApplicationDbContext, Task> action) => DbAsync(async db => { await action(db); return true; });
    public async ValueTask DisposeAsync() { await Provider.DisposeAsync(); await connection.DisposeAsync(); }
}
