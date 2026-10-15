using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using SPWPOS.Application.Authentication;
using SPWPOS.Application.Authentication.Interfaces;
using SPWPOS.Domain.Entities;
using SPWPOS.Infrastructure.Authentication;
using SPWPOS.Infrastructure.Email;
using SPWPOS.Infrastructure.Persistence;
using SPWPOS.Infrastructure.Security;
namespace SPWPOS.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddDbContext<ApplicationDbContext>(o => o.UseSqlServer(configuration.GetConnectionString("DefaultConnection")
            ?? throw new InvalidOperationException("ConnectionStrings:DefaultConnection is required.")));
        services.AddIdentityCore<ApplicationUser>(o =>
        {
            o.User.RequireUniqueEmail = true;
            o.User.AllowedUserNameCharacters = "";
            o.Password.RequiredLength = 8; o.Password.RequireDigit = true;
            o.Password.RequireNonAlphanumeric = true; o.Password.RequireUppercase = true; o.Password.RequireLowercase = true;
            o.Lockout.AllowedForNewUsers = true; o.Lockout.MaxFailedAccessAttempts = 5;
            o.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(15);
            o.SignIn.RequireConfirmedEmail = true;
        }).AddRoles<IdentityRole<Guid>>().AddEntityFrameworkStores<ApplicationDbContext>();
        services.Replace(ServiceDescriptor.Scoped<IPasswordHasher<ApplicationUser>, Argon2PasswordHasher>());
        services.AddScoped<IPasswordValidator<ApplicationUser>, PasswordPolicyValidator>();
        services.AddSingleton(TimeProvider.System);
        services.AddScoped<SPWPOS.Infrastructure.Workspace.AiUsageCollector>();
        services.AddScoped<SPWPOS.Application.Workspace.IAiUsageService, SPWPOS.Infrastructure.Workspace.AiUsageService>();
        services.AddSingleton<PasswordTiming>();
        services.AddScoped<IAuthService, AuthService>();
        services.AddSingleton<IFirebaseTokenVerifier, FirebaseTokenVerifier>();
        services.AddOptions<FirebaseSettings>().BindConfiguration("Firebase")
            .Validate(x => x.Valid, "Firebase settings are invalid.").ValidateOnStart();
        services.AddScoped<IOtpService, OtpService>();
        services.AddScoped<IJwtTokenService, JwtTokenService>();
        services.AddScoped<EmailService>();
        services.AddScoped<IEmailService>(provider => provider.GetRequiredService<EmailService>());
        services.AddScoped<IEmailConnectionCheck>(provider => provider.GetRequiredService<EmailService>());
        services.AddScoped<PasswordNotificationService>();
        services.AddHostedService<PasswordNotificationWorker>();
        services.AddScoped<ISecurityAuditService, SecurityAuditService>();
        services.AddScoped<AccountRateLimiter>();
        services.AddSingleton<SPWPOS.Application.Workspace.PlanningEngine>();
        services.AddScoped<SPWPOS.Infrastructure.Workspace.WorkspaceHistoryMaintenance>();
        services.AddScoped<SPWPOS.Infrastructure.Workspace.WorkspaceService>();
        services.AddScoped<SPWPOS.Application.Workspace.IWorkspaceService>(p => p.GetRequiredService<SPWPOS.Infrastructure.Workspace.WorkspaceService>());
        services.AddScoped<SPWPOS.Application.Workspace.IPlanningAgentService>(p => p.GetRequiredService<SPWPOS.Infrastructure.Workspace.WorkspaceService>());
        services.AddSingleton<SPWPOS.Infrastructure.Workspace.NotificationJobStatus>();
        services.AddHostedService<SPWPOS.Infrastructure.Workspace.WorkspaceNotificationWorker>();
        services.AddHostedService<SPWPOS.Infrastructure.Workspace.AdminBootstrapWorker>();
        services.AddSingleton<SPWPOS.Infrastructure.Workspace.IVertexAccessTokenProvider, SPWPOS.Infrastructure.Workspace.VertexAccessTokenProvider>();
        services.AddHttpClient<SPWPOS.Application.Workspace.ITaskInterpreter, SPWPOS.Infrastructure.Workspace.TaskInterpreter>(client =>
        { client.Timeout = TimeSpan.FromSeconds(20); client.MaxResponseContentBufferSize = 131072; })
            // Vertex API-key compatibility includes a credential in the request URL.
            // Disable this client's URL/header logging even if the app enables Trace logging.
            .RemoveAllLoggers()
            .ConfigurePrimaryHttpMessageHandler(() => new HttpClientHandler { AllowAutoRedirect = false });
        services.AddHttpClient<SPWPOS.Application.Workspace.IAgentVoiceTranscriber, SPWPOS.Infrastructure.Workspace.TaskInterpreter>(client =>
        { client.Timeout = TimeSpan.FromSeconds(20); client.MaxResponseContentBufferSize = 131072; })
            .RemoveAllLoggers()
            .ConfigurePrimaryHttpMessageHandler(() => new HttpClientHandler { AllowAutoRedirect = false });
        services.AddOptions<JwtSettings>().BindConfiguration("JwtSettings")
            .Validate(x => StrongKey(x.SigningKey) && !string.IsNullOrWhiteSpace(x.Issuer) && !string.IsNullOrWhiteSpace(x.Audience)
                && x.AccessTokenMinutes is >= 1 and <= 15 && x.RefreshTokenDays is >= 1 and <= 7, "JWT settings are invalid; use a Base64 key with at least 32 random bytes.").ValidateOnStart();
        services.AddOptions<OtpSettings>().BindConfiguration("OtpSettings")
            .Validate(x => StrongKey(x.SecretKey) && x.Length is >= 6 and <= 8 && x.ExpirationMinutes is >= 1 and <= 5
                && x.MaxAttempts is >= 1 and <= 5 && x.ResendCooldownSeconds >= 60, "OTP settings are invalid.").ValidateOnStart();
        services.AddOptions<PasswordSettings>().BindConfiguration("PasswordSettings")
            .Validate(x => x.MinLength == 8 && x.MaxLength == 32 && x.MemoryKiB is >= 19456 and <= 262144
                && x.Iterations is >= 3 and <= 10 && x.Parallelism == 1, "Password settings are invalid.").ValidateOnStart();
        services.AddOptions<EmailSettings>().BindConfiguration("EmailSettings")
            .Validate(x => !string.IsNullOrWhiteSpace(x.Host) && System.Net.Mail.MailAddress.TryCreate(x.SenderEmail, out _)
                && x.Port is > 0 and <= 65535 && x.TimeoutSeconds is >= 1 and <= 30, "SMTP settings are invalid.").ValidateOnStart();
        return services;
    }
    public static bool StrongKey(string value)
    {
        try { return Convert.FromBase64String(value).Length >= 32; }
        catch (FormatException) { return false; }
    }
}
