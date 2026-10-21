using System.Threading.RateLimiting;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi;
using Microsoft.Extensions.Options;
using Microsoft.AspNetCore.HttpOverrides;
using System.Net;
using System.IO.Compression;
using Microsoft.AspNetCore.ResponseCompression;
using SPWPOS.Api.Middleware;
using SPWPOS.Application.Authentication;
using SPWPOS.Infrastructure;
using SPWPOS.Infrastructure.Persistence;

var builder = WebApplication.CreateBuilder(args);
builder.WebHost.ConfigureKestrel(o => { o.Limits.MaxRequestBodySize = 3 * 1024 * 1024; o.AddServerHeader = false; });
builder.Services.AddInfrastructure(builder.Configuration);
builder.Services.AddControllers().ConfigureApiBehaviorOptions(o => o.InvalidModelStateResponseFactory = context =>
{
    var msg = context.ModelState.Values.SelectMany(v => v.Errors).Select(e => e.ErrorMessage).FirstOrDefault(m => !string.IsNullOrWhiteSpace(m)) ?? "Dữ liệu nhập không hợp lệ. Vui lòng kiểm tra lại.";
    return new BadRequestObjectResult(ApiProblems.Create(context.HttpContext, 400, "VALIDATION_ERROR", msg)) { ContentTypes = { "application/problem+json" } };
});
builder.Services.AddProblemDetails();
builder.Services.AddResponseCompression(o =>
{
    o.EnableForHttps = true;
    o.Providers.Add<BrotliCompressionProvider>();
    o.Providers.Add<GzipCompressionProvider>();
});
builder.Services.Configure<BrotliCompressionProviderOptions>(o => o.Level = CompressionLevel.Fastest);
builder.Services.Configure<GzipCompressionProviderOptions>(o => o.Level = CompressionLevel.Fastest);
builder.Services.AddExceptionHandler<GlobalExceptionHandler>();
builder.Services.AddHttpsRedirection(o => o.HttpsPort = 7043);
builder.Services.AddHsts(o => { o.MaxAge = TimeSpan.FromDays(365); o.IncludeSubDomains = true; });
var trustedProxies = builder.Configuration.GetSection("Security:TrustedProxyAddresses").Get<string[]>() ?? [];
builder.Services.Configure<ForwardedHeadersOptions>(o =>
{
    o.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
    o.ForwardLimit = 1; o.KnownProxies.Clear(); o.KnownNetworks.Clear();
    foreach (var proxy in trustedProxies) o.KnownProxies.Add(IPAddress.Parse(proxy));
});
var origins = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>() ?? [];
if (origins.Any(x => x == "*" || !Uri.TryCreate(x, UriKind.Absolute, out _))) throw new InvalidOperationException("CORS requires explicit origins.");
builder.Services.AddCors(o => o.AddPolicy("frontend", p => p.WithOrigins(origins).WithMethods("GET", "POST", "PUT", "PATCH", "DELETE").WithHeaders("Content-Type", "Authorization")));
if (!builder.Environment.IsDevelopment() && !builder.Environment.IsEnvironment("Testing") &&
    !builder.Configuration.GetValue<bool>("EmailSettings:EnableSsl")) throw new InvalidOperationException("SMTP TLS is required in production.");
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme).AddJwtBearer();
builder.Services.AddOptions<JwtBearerOptions>(JwtBearerDefaults.AuthenticationScheme).Configure<IOptions<JwtSettings>>((o, settings) =>
{
    var jwt = settings.Value;
    o.MapInboundClaims = false; o.SaveToken = false; o.IncludeErrorDetails = false;
    o.TokenValidationParameters = new TokenValidationParameters
    {
        ValidateIssuer = true,
        ValidateAudience = true,
        ValidateLifetime = true,
        ValidateIssuerSigningKey = true,
        RequireExpirationTime = true,
        RequireSignedTokens = true,
        ValidIssuer = jwt.Issuer,
        ValidAudience = jwt.Audience,
        IssuerSigningKey = new SymmetricSecurityKey(Convert.FromBase64String(jwt.SigningKey)),
        ValidAlgorithms = [SecurityAlgorithms.HmacSha256],
        ClockSkew = TimeSpan.FromSeconds(10),
        NameClaimType = "sub",
        RoleClaimType = "role"
    };
    o.Events = new JwtBearerEvents
    {
        OnMessageReceived = context =>
        {
            if (string.IsNullOrEmpty(context.Request.Headers.Authorization) && context.Request.Cookies.TryGetValue(SPWPOS.Api.Controllers.BrowserSessionController.AccessCookie, out var token)) context.Token = token;
            return Task.CompletedTask;
        },
        OnTokenValidated = async context =>
        {
            if (!Guid.TryParse(context.Principal?.FindFirst("sub")?.Value, out var userId) ||
                !Guid.TryParse(context.Principal?.FindFirst("sid")?.Value, out var familyId))
            { context.Fail("Invalid session."); return; }
            var db = context.HttpContext.RequestServices.GetRequiredService<ApplicationDbContext>();
            var clock = context.HttpContext.RequestServices.GetRequiredService<TimeProvider>();
            var now = clock.GetUtcNow();
            var valid = await db.Users.AsNoTracking().AnyAsync(x => x.Id == userId && x.IsActive && x.EmailConfirmed, context.HttpContext.RequestAborted)
                && await db.RefreshTokens.AsNoTracking().AnyAsync(x => x.UserId == userId && x.FamilyId == familyId && x.RevokedAt == null && x.ExpiresAt > now, context.HttpContext.RequestAborted);
            if (!valid) context.Fail("Session revoked or account unavailable.");
        },
        OnChallenge = async context =>
        {
            context.HandleResponse(); context.Response.Headers.WWWAuthenticate = "Bearer";
            await ApiProblems.WriteAsync(context.HttpContext, 401, "UNAUTHORIZED", "Authentication is required or the session is invalid.", context.HttpContext.RequestAborted);
        },
        OnForbidden = context => ApiProblems.WriteAsync(context.HttpContext, 403, "FORBIDDEN", "Access denied.", context.HttpContext.RequestAborted)
    };
});
builder.Services.AddAuthorization();
builder.Services.AddRateLimiter(o =>
{
    o.RejectionStatusCode = 429;
    // Limit simultaneous expensive Argon2 computations as well as requests per IP.
    o.GlobalLimiter = PartitionedRateLimiter.CreateChained(
        PartitionedRateLimiter.Create<HttpContext, string>(c =>
            HttpMethods.IsPost(c.Request.Method) && c.Request.Path.Value is
                "/api/auth/register" or "/api/auth/login" or "/api/browser-session/login" or "/api/auth/reset-password" or "/api/auth/change-password" or "/api/auth/set-password"
            ? RateLimitPartition.GetConcurrencyLimiter("password-computation", _ => new ConcurrencyLimiterOptions
                { PermitLimit = Math.Max(2, Math.Min(Environment.ProcessorCount, 8)), QueueLimit = 0 })
            : RateLimitPartition.GetNoLimiter("other-requests")),
        PartitionedRateLimiter.Create<HttpContext, string>(c => RateLimitPartition.GetSlidingWindowLimiter(c.Connection.RemoteIpAddress?.ToString() ?? "unknown", _ => new SlidingWindowRateLimiterOptions
        { PermitLimit = 60, Window = TimeSpan.FromMinutes(1), SegmentsPerWindow = 6, QueueLimit = 0 })));
    o.AddPolicy("auth-ip", c => RateLimitPartition.GetSlidingWindowLimiter(
        $"{c.Connection.RemoteIpAddress}:{c.Request.Path}", _ => new SlidingWindowRateLimiterOptions
        { PermitLimit = c.Request.Path.Value?.Contains("request-otp", StringComparison.Ordinal) == true ? 5 : 20, Window = TimeSpan.FromMinutes(1), SegmentsPerWindow = 6, QueueLimit = 0 }));
    o.AddPolicy("agent-voice", c => RateLimitPartition.GetSlidingWindowLimiter(
        $"voice:{c.User.FindFirst("sub")?.Value ?? c.Connection.RemoteIpAddress?.ToString()}", _ => new SlidingWindowRateLimiterOptions
        { PermitLimit = 24, Window = TimeSpan.FromMinutes(1), SegmentsPerWindow = 6, QueueLimit = 0 }));
    o.OnRejected = async (context, ct) =>
    {
        context.HttpContext.Response.Headers.RetryAfter = "60";
        await ApiProblems.WriteAsync(context.HttpContext, 429, "RATE_LIMITED", "Too many requests. Please try again later.", ct);
    };
});
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(o =>
{
    o.SwaggerDoc("v1", new OpenApiInfo { Title = "SPWPOS", Version = "v1", Description = "Account, Tasks, Time, Planning, Execution, Insights, Notifications and Administration. Errors use ProblemDetails with success/error extensions." });
    o.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme { Type = SecuritySchemeType.Http, Scheme = "bearer", BearerFormat = "JWT", Description = "Paste the access token from login." });
    o.AddSecurityRequirement(document => new OpenApiSecurityRequirement { [new OpenApiSecuritySchemeReference("Bearer", document)] = [] });
    o.TagActionsBy(api => api.ActionDescriptor.EndpointMetadata.OfType<Microsoft.AspNetCore.Http.TagsAttribute>().FirstOrDefault()?.Tags.ToList() ?? ["Authentication"]);
});
var app = builder.Build();
var firebaseSettings = builder.Configuration.GetSection("Firebase").Get<FirebaseSettings>() ?? new();
if (args.Contains("--check-schema", StringComparer.Ordinal))
{
    try
    {
        using var scope = app.Services.CreateScope();
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(15));
        var pending = (await scope.ServiceProvider.GetRequiredService<ApplicationDbContext>().Database.GetPendingMigrationsAsync(timeout.Token)).Count();
        if (pending > 0) { Console.Error.WriteLine($"Database has {pending} pending migrations. Review the upgrade runbook before restarting."); Environment.ExitCode = 3; }
        else Console.WriteLine("Database schema matches the current migration set. No database changes were made.");
    }
    catch (Exception exception)
    {
        Console.Error.WriteLine($"Schema check failed ({exception.GetType().Name}). No database changes were made.");
        Environment.ExitCode = 1;
    }
    await app.DisposeAsync();
    return;
}
if (args.Contains("--check-ai", StringComparer.Ordinal) || args.Contains("--check-ai-stream", StringComparer.Ordinal) || args.Contains("--check-ai-voice", StringComparer.Ordinal) || args.Contains("--check-ai-quality", StringComparer.Ordinal) || args.Contains("--check-ai-free-time", StringComparer.Ordinal) || args.Contains("--check-ai-urgency", StringComparer.Ordinal) || args.Contains("--check-ai-schedule-review", StringComparer.Ordinal))
{
    try
    {
        using var checkScope = app.Services.CreateScope();
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(args.Contains("--check-ai-quality", StringComparer.Ordinal) ? 120 : 75));
        if (args.Contains("--check-ai-schedule-review", StringComparer.Ordinal))
            await SPWPOS.Infrastructure.Workspace.LiveAiCheck.RunScheduleReviewAsync(
                checkScope.ServiceProvider.GetRequiredService<SPWPOS.Application.Workspace.ITaskInterpreter>(),
                checkScope.ServiceProvider.GetRequiredService<TimeProvider>(), timeout.Token);
        else if (args.Contains("--check-ai-urgency", StringComparer.Ordinal))
            await SPWPOS.Infrastructure.Workspace.LiveAiCheck.RunUrgencyAsync(
                checkScope.ServiceProvider.GetRequiredService<SPWPOS.Application.Workspace.ITaskInterpreter>(),
                checkScope.ServiceProvider.GetRequiredService<TimeProvider>(), timeout.Token);
        else if (args.Contains("--check-ai-free-time", StringComparer.Ordinal))
            await SPWPOS.Infrastructure.Workspace.LiveAiCheck.RunFreeTimeAsync(
                checkScope.ServiceProvider.GetRequiredService<SPWPOS.Application.Workspace.ITaskInterpreter>(),
                checkScope.ServiceProvider.GetRequiredService<TimeProvider>(), timeout.Token);
        else if (args.Contains("--check-ai-quality", StringComparer.Ordinal))
            await SPWPOS.Infrastructure.Workspace.LiveAiCheck.RunQualityAsync(
                checkScope.ServiceProvider.GetRequiredService<SPWPOS.Application.Workspace.ITaskInterpreter>(),
                checkScope.ServiceProvider.GetRequiredService<TimeProvider>(), timeout.Token);
        else if (args.Contains("--check-ai-voice", StringComparer.Ordinal))
        {
            var fileIndex = Array.IndexOf(args, "--voice-file");
            if (fileIndex < 0 || fileIndex + 1 >= args.Length) throw new InvalidOperationException("Voice test file is required.");
            await SPWPOS.Infrastructure.Workspace.LiveAiCheck.RunVoiceAsync(
                checkScope.ServiceProvider.GetRequiredService<SPWPOS.Application.Workspace.IAgentVoiceTranscriber>(), args[fileIndex + 1], timeout.Token);
        }
        else if (args.Contains("--check-ai-stream", StringComparer.Ordinal))
            await SPWPOS.Infrastructure.Workspace.LiveAiCheck.RunStreamingAsync(
                checkScope.ServiceProvider.GetRequiredService<SPWPOS.Application.Workspace.ITaskInterpreter>(),
                checkScope.ServiceProvider.GetRequiredService<TimeProvider>(), timeout.Token);
        else await SPWPOS.Infrastructure.Workspace.LiveAiCheck.RunAsync(
            checkScope.ServiceProvider.GetRequiredService<SPWPOS.Application.Workspace.ITaskInterpreter>(),
            checkScope.ServiceProvider.GetRequiredService<TimeProvider>(), timeout.Token);
    }
    catch (Exception exception)
    {
        // Never print provider bodies, API keys or credentials in diagnostics.
        Console.Error.WriteLine(exception is SPWPOS.Application.Authentication.AuthException failure
            ? $"LIVE AI check failed: {failure.Code}. {failure.Message}"
            : $"LIVE AI check failed ({exception.GetType().Name}). Check AI configuration and connectivity.");
        if (exception.Data["GoogleHttpStatus"] is int googleStatus && exception.Data["GoogleReason"] is string googleReason)
            Console.Error.WriteLine($"LIVE AI provider detail: HTTP={googleStatus}; REASON={googleReason}.");
        Environment.ExitCode = 1;
    }
    await app.DisposeAsync();
    return;
}
if (args.Contains("--check-smtp", StringComparer.Ordinal))
{
    try
    {
        using var checkScope = app.Services.CreateScope();
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        await checkScope.ServiceProvider.GetRequiredService<SPWPOS.Infrastructure.Email.IEmailConnectionCheck>().CheckConnectionAsync(timeout.Token);
        Console.WriteLine("SMTP connection and authentication succeeded. No email sent.");
    }
    catch (Exception exception)
    {
        Console.Error.WriteLine($"SMTP check failed ({exception.GetType().Name}). Check the configured provider and credentials.");
        Environment.ExitCode = 1;
    }
    await app.DisposeAsync();
    return;
}
app.UseExceptionHandler();
app.UseStatusCodePages(async context =>
{
    int status = context.HttpContext.Response.StatusCode;
    await ApiProblems.WriteAsync(context.HttpContext, status, status == 404 ? "NOT_FOUND" : "REQUEST_REJECTED",
        status == 404 ? "Resource not found." : "The request could not be processed.", context.HttpContext.RequestAborted);
});
if (trustedProxies.Length > 0) app.UseForwardedHeaders();
app.Use(async (context, next) =>
{
    context.Response.Headers.XContentTypeOptions = "nosniff";
    context.Response.Headers.XFrameOptions = "DENY";
    context.Response.Headers["Referrer-Policy"] = "no-referrer";
    context.Response.Headers["Permissions-Policy"] = "camera=(), microphone=(self), geolocation=()";
    context.Response.Headers.CacheControl = context.Request.Path.StartsWithSegments("/api") ? "no-store" : "no-cache";
    if (context.Request.Path.StartsWithSegments("/api"))
    {
        context.Response.Headers.ContentSecurityPolicy = "default-src 'none'; frame-ancestors 'none'";
        if (!context.Request.IsHttps)
        { await ApiProblems.WriteAsync(context, 400, "HTTPS_REQUIRED", "HTTPS is required.", context.RequestAborted); return; }
        if (context.Request.ContentLength > 3 * 1024 * 1024)
        { await ApiProblems.WriteAsync(context, 413, "REQUEST_TOO_LARGE", "Request body is too large.", context.RequestAborted); return; }
        var browserSession = context.Request.Path.StartsWithSegments("/api/browser-session");
        var cookieAuthentication = context.Request.Cookies.ContainsKey(SPWPOS.Api.Controllers.BrowserSessionController.AccessCookie) && !context.Request.Headers.Authorization.ToString().StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase);
        if ((browserSession || cookieAuthentication) && !HttpMethods.IsGet(context.Request.Method) && !HttpMethods.IsHead(context.Request.Method))
        {
            var origin = context.Request.Headers.Origin.ToString();
            var sameOrigin = string.IsNullOrEmpty(origin) || origin.Equals($"{context.Request.Scheme}://{context.Request.Host}", StringComparison.OrdinalIgnoreCase);
            if (context.Request.Headers["X-SPWPOS"] != "1" || !sameOrigin)
            { await ApiProblems.WriteAsync(context, 403, "CSRF_REJECTED", "Request origin could not be verified.", context.RequestAborted); return; }
        }
    }
    else if (!context.Request.Path.StartsWithSegments("/swagger"))
    {
        var googleScripts = firebaseSettings.Enabled ? " https://apis.google.com" : "";
        var googleConnections = firebaseSettings.Enabled ? $" https://identitytoolkit.googleapis.com https://securetoken.googleapis.com https://www.googleapis.com https://{firebaseSettings.AuthDomain}" : "";
        var googleFrames = firebaseSettings.Enabled ? $"https://{firebaseSettings.AuthDomain}" : "'none'";
        context.Response.Headers.ContentSecurityPolicy = $"default-src 'self'; script-src 'self'{googleScripts}; style-src 'self'; img-src 'self' data:; connect-src 'self'{googleConnections}; frame-src {googleFrames}; frame-ancestors 'none'; base-uri 'self'; form-action 'self'";
    }
    await next();
});
if (!app.Environment.IsDevelopment()) app.UseHsts();
app.UseHttpsRedirection();
// Compress public files only. Authenticated JSON and other private responses
// retain their existing no-store behavior and are never compressed here.
app.UseWhen(c => c.Request.Path.StartsWithSegments("/app") || c.Request.Path.StartsWithSegments("/assets") ||
    c.Request.Path == "/" || c.Request.Path == "/index.html", branch => branch.UseResponseCompression());
app.UseDefaultFiles();
app.UseStaticFiles(new StaticFileOptions
{
    OnPrepareResponse = context =>
    {
        if (context.Context.Request.Path.StartsWithSegments("/app/assets"))
            context.Context.Response.Headers.CacheControl = "public,max-age=31536000,immutable";
        else if (context.Context.Request.Path.StartsWithSegments("/assets"))
            context.Context.Response.Headers.CacheControl = "public,max-age=3600,must-revalidate";
    }
});
app.UseRouting();
app.UseCors("frontend");
app.UseAuthentication();
app.UseRateLimiter();
app.UseAuthorization();
if (app.Environment.IsDevelopment()) { app.UseSwagger(); app.UseSwaggerUI(); }
app.MapControllers();
app.Run();
public partial class Program;
