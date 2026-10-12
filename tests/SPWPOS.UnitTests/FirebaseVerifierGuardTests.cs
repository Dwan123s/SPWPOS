using Microsoft.Extensions.Options;
using System.Security.Cryptography;
using System.Text.Json;
using SPWPOS.Application.Authentication;
using SPWPOS.Infrastructure.Authentication;

namespace SPWPOS.UnitTests;

[CollectionDefinition("Firebase environment", DisableParallelization = true)]
public sealed class FirebaseEnvironmentCollection;

[Collection("Firebase environment")]
public sealed class FirebaseVerifierGuardTests
{
    [Fact]
    public async Task Configured_missing_file_does_not_silently_fall_back_to_other_credentials()
    {
        using var verifier = new FirebaseTokenVerifier(Options.Create(new FirebaseSettings
        { Enabled = true, ProjectId = "planora-test", ServiceAccountPath = Path.Combine(Path.GetTempPath(), Guid.NewGuid() + ".json") }));
        var error = await Assert.ThrowsAsync<AuthException>(() => verifier.VerifyGoogleAsync("not-a-jwt", TestContext.Current.CancellationToken));
        Assert.Equal("GOOGLE_UNAVAILABLE", error.Code);
    }
    [Theory]
    [InlineData("planora-test", "GOOGLE_IDENTITY_INVALID")]
    [InlineData("another-project", "GOOGLE_UNAVAILABLE")]
    public async Task Server_key_must_belong_to_configured_project(string credentialProject, string expectedCode)
    {
        using var rsa = RSA.Create(2048);
        var path = Path.Combine(Path.GetTempPath(), Guid.NewGuid() + ".json");
        try
        {
            await File.WriteAllTextAsync(path, JsonSerializer.Serialize(new
            {
                type = "service_account", project_id = credentialProject, private_key_id = "test-key",
                private_key = rsa.ExportPkcs8PrivateKeyPem(), client_email = $"test@{credentialProject}.iam.gserviceaccount.com",
                client_id = "123", token_uri = "https://oauth2.googleapis.com/token"
            }), TestContext.Current.CancellationToken);
            using var verifier = new FirebaseTokenVerifier(Options.Create(new FirebaseSettings
            { Enabled = true, ProjectId = "planora-test", ServiceAccountPath = path }));
            // Malformed tokens are rejected locally after loading the key; no Google user is contacted.
            var error = await Assert.ThrowsAsync<AuthException>(() => verifier.VerifyGoogleAsync("not-a-jwt", TestContext.Current.CancellationToken));
            Assert.Equal(expectedCode, error.Code);
        }
        finally { File.Delete(path); }
    }
    [Fact]
    public async Task Disabled_feature_rejects_tokens_without_loading_credentials()
    {
        using var verifier = new FirebaseTokenVerifier(Options.Create(new FirebaseSettings()));
        var error = await Assert.ThrowsAsync<AuthException>(() => verifier.VerifyGoogleAsync("untrusted-token", TestContext.Current.CancellationToken));
        Assert.Equal("GOOGLE_UNAVAILABLE", error.Code); Assert.Equal(503, error.StatusCode);
    }
    [Fact]
    public async Task Emulator_environment_cannot_bypass_signed_token_verification()
    {
        var original = Environment.GetEnvironmentVariable("FIREBASE_AUTH_EMULATOR_HOST");
        try
        {
            Environment.SetEnvironmentVariable("FIREBASE_AUTH_EMULATOR_HOST", "localhost:9099");
            using var verifier = new FirebaseTokenVerifier(Options.Create(new FirebaseSettings
            {
                Enabled = true, ProjectId = "planora-test", ApiKey = "public-key", AppId = "public-app"
            }));
            var error = await Assert.ThrowsAsync<AuthException>(() => verifier.VerifyGoogleAsync("unsigned-emulator-token", TestContext.Current.CancellationToken));
            Assert.Equal("GOOGLE_UNAVAILABLE", error.Code); Assert.Equal(503, error.StatusCode);
        }
        finally { Environment.SetEnvironmentVariable("FIREBASE_AUTH_EMULATOR_HOST", original); }
    }
}
