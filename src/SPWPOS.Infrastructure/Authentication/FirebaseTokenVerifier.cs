using FirebaseAdmin;
using FirebaseAdmin.Auth;
using Google.Apis.Auth.OAuth2;
using Microsoft.Extensions.Options;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using SPWPOS.Application.Authentication;
using SPWPOS.Application.Authentication.DTOs;
using SPWPOS.Application.Authentication.Interfaces;

namespace SPWPOS.Infrastructure.Authentication;

public sealed class FirebaseTokenVerifier : IFirebaseTokenVerifier, IDisposable
{
    private readonly FirebaseSettings settings;
    private readonly Lazy<Task<FirebaseApp>> application;
    public FirebaseTokenVerifier(IOptions<FirebaseSettings> options)
    {
        settings = options.Value;
        application = new(async () => FirebaseApp.Create(new AppOptions
        {
            ProjectId = settings.ProjectId,
            Credential = await LoadCredentialAsync()
        }, "PLANORA-" + Guid.NewGuid().ToString("N")));
    }
    private async Task<GoogleCredential> LoadCredentialAsync()
    {
        if (string.IsNullOrWhiteSpace(settings.ServiceAccountPath))
            return await GoogleCredential.GetApplicationDefaultAsync();
        // Accept only a service account key, never other credential types from a JSON file.
        using var input = File.OpenRead(settings.ServiceAccountPath);
        var credential = ServiceAccountCredential.FromServiceAccountData(input);
        if (!string.Equals(credential.ProjectId, settings.ProjectId, StringComparison.Ordinal))
            throw new InvalidOperationException("Firebase service account belongs to a different project.");
        return GoogleCredential.FromServiceAccountCredential(credential);
    }
    public async Task<GoogleIdentity> VerifyGoogleAsync(string idToken, CancellationToken ct)
    {
        if (!settings.Enabled) throw new AuthException("GOOGLE_UNAVAILABLE", "Đăng nhập Google hiện chưa khả dụng.", 503);
        // Admin SDK accepts unsigned emulator tokens when this environment variable is set.
        // Production sign-in must always use signed Firebase tokens.
        if (!string.IsNullOrEmpty(Environment.GetEnvironmentVariable("FIREBASE_AUTH_EMULATOR_HOST")))
            throw new AuthException("GOOGLE_UNAVAILABLE", "Đăng nhập Google hiện chưa khả dụng.", 503);
        FirebaseApp app;
        try { app = await application.Value.WaitAsync(ct); }
        catch (Exception error) when (error is InvalidOperationException or IOException or UnauthorizedAccessException)
        { throw new AuthException("GOOGLE_UNAVAILABLE", "Đăng nhập Google hiện chưa khả dụng.", 503); }
        FirebaseToken token;
        UserRecord firebaseUser;
        try
        {
            var firebaseAuth = FirebaseAuth.GetAuth(app);
            token = await firebaseAuth.VerifyIdTokenAsync(idToken, true, ct);
            // The .NET SDK revocation check does not check UserRecord.Disabled.
            firebaseUser = await firebaseAuth.GetUserAsync(token.Uid, ct);
        }
        catch (FirebaseAuthException error) when (error.AuthErrorCode is AuthErrorCode.InvalidIdToken or AuthErrorCode.ExpiredIdToken or AuthErrorCode.RevokedIdToken or AuthErrorCode.UserNotFound)
        { throw new AuthException("GOOGLE_IDENTITY_INVALID", "Phiên Google không hợp lệ. Hãy đăng nhập lại.", 401); }
        var claims = JObject.Parse(JsonConvert.SerializeObject(token.Claims));
        var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        var authTime = claims.Value<long?>("auth_time");
        var validAfter = new DateTimeOffset(DateTime.SpecifyKind(firebaseUser.TokensValidAfterTimestamp, DateTimeKind.Utc)).ToUnixTimeSeconds();
        if (firebaseUser.Disabled || claims["firebase"]?.Value<string>("sign_in_provider") != "google.com" ||
            claims.Value<bool?>("email_verified") != true || authTime is null || authTime < validAfter || authTime > now + 30 || now - authTime > 300)
            throw new AuthException("GOOGLE_IDENTITY_INVALID", "Hãy chọn lại tài khoản Google có email đã xác minh.", 401);
        var name = (claims.Value<string>("name") ?? "").Split(' ', 2, StringSplitOptions.RemoveEmptyEntries);
        return new GoogleIdentity(token.Uid, claims.Value<string>("email") ?? "", true,
            name.ElementAtOrDefault(0) ?? "", name.ElementAtOrDefault(1) ?? "");
    }
    public void Dispose()
    {
        if (application.IsValueCreated && application.Value.IsCompletedSuccessfully) application.Value.Result.Delete();
    }
}
