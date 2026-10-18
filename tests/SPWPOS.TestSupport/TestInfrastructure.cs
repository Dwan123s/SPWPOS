using System.Collections.Concurrent;
using System.Security.Cryptography;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using SPWPOS.Application.Authentication.Interfaces;
using SPWPOS.Domain.Enums;
using SPWPOS.Infrastructure.Persistence;
namespace SPWPOS.TestSupport;

public sealed class TestClock : TimeProvider
{
    private DateTimeOffset now = DateTimeOffset.UtcNow;
    public override DateTimeOffset GetUtcNow() => now;
    public void Advance(TimeSpan interval) => now += interval;
}
// Test-only fake. No test inbox/OTP endpoint is registered in the actual API.
public sealed class TestEmailService : IEmailService
{
    public ConcurrentDictionary<(string Email, OtpPurpose Purpose), string> Inbox { get; } = new();
    public int Notifications { get; private set; }
    public bool FailDelivery { get; set; }
    public List<(string Email, string Title, string Message)> Reminders { get; } = [];
    public Task SendReminderAsync(string email, string title, string message, CancellationToken ct)
    {
        if (FailDelivery) throw new IOException("Test transport failure.");
        Reminders.Add((email, title, message)); return Task.CompletedTask;
    }
    public Task SendEmailVerificationOtpAsync(string email, string otp, CancellationToken ct) => Send(email, otp, OtpPurpose.EmailVerification);
    public Task SendForgotPasswordOtpAsync(string email, string otp, CancellationToken ct) => Send(email, otp, OtpPurpose.ForgotPassword);
    public Task SendChangePasswordOtpAsync(string email, string otp, CancellationToken ct) => Send(email, otp, OtpPurpose.ChangePassword);
    private Task Send(string email, string otp, OtpPurpose purpose)
    {
        if (FailDelivery) throw new IOException("Test transport failure.");
        Inbox[(email, purpose)] = otp; return Task.CompletedTask;
    }
    public Task SendPasswordChangedAsync(string email, CancellationToken ct)
    {
        if (FailDelivery) throw new IOException("Test transport failure.");
        Notifications++; return Task.CompletedTask;
    }
}
public sealed class SqliteTestModelCustomizer(ModelCustomizerDependencies dependencies) : ModelCustomizer(dependencies)
{
    public override void Customize(ModelBuilder modelBuilder, DbContext context)
    {
        base.Customize(modelBuilder, context);
        var converter = new ValueConverter<DateTimeOffset, long>(x => x.UtcTicks, x => new DateTimeOffset(x, TimeSpan.Zero));
        foreach (var property in modelBuilder.Model.GetEntityTypes().SelectMany(x => x.GetProperties()))
            if (property.ClrType == typeof(DateTimeOffset) || property.ClrType == typeof(DateTimeOffset?)) property.SetValueConverter(converter);
    }
}
public static class TestInfrastructure
{
    public static Dictionary<string, string?> Configuration() => new()
    {
        ["ConnectionStrings:DefaultConnection"] = "Server=unused;Database=unused;Integrated Security=True;Encrypt=True",
        ["JwtSettings:Issuer"] = "SPWPOS",
        ["JwtSettings:Audience"] = "SPWPOS.Client",
        ["JwtSettings:SigningKey"] = Convert.ToBase64String(RandomNumberGenerator.GetBytes(64)),
        ["OtpSettings:SecretKey"] = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32)),
        ["EmailSettings:Host"] = "test.example",
        ["EmailSettings:SenderEmail"] = "no-reply@example.com",
        ["EmailSettings:EnableSsl"] = "true",
        ["AllowedHosts"] = "localhost",
        ["AdminBootstrap:Email"] = "",
        ["AiSettings:Enabled"] = "false",
        ["Firebase:Enabled"] = "false"
    };
    public static void ReplacePersistence(IServiceCollection services, SqliteConnection connection)
    {
        services.RemoveAll<ApplicationDbContext>();
        services.RemoveAll<DbContextOptions<ApplicationDbContext>>();
        services.AddDbContext<ApplicationDbContext>(o => o.UseSqlite(connection).ReplaceService<IModelCustomizer, SqliteTestModelCustomizer>());
    }
    public static void ReplaceSqlServerPersistence(IServiceCollection services, string connection)
    {
        services.RemoveAll<ApplicationDbContext>();
        services.RemoveAll<DbContextOptions<ApplicationDbContext>>();
        services.AddDbContext<ApplicationDbContext>(o => o.UseSqlServer(connection));
    }
    public static void ReplaceSecurityDependencies(IServiceCollection services, TestClock clock, TestEmailService email)
    {
        services.RemoveAll<TimeProvider>(); services.AddSingleton<TimeProvider>(clock);
        services.RemoveAll<IEmailService>(); services.AddSingleton<IEmailService>(email);
    }
}
