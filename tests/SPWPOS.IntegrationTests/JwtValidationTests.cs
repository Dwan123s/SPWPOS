using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http.Headers;
using System.Security.Claims;
using System.Security.Cryptography;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using SPWPOS.Application.Authentication;
using SPWPOS.Infrastructure.Persistence;
using SPWPOS.TestSupport;
namespace SPWPOS.IntegrationTests;

public sealed class JwtValidationTests
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;
    [Theory]
    [InlineData("issuer")]
    [InlineData("audience")]
    [InlineData("signature")]
    [InlineData("expired")]
    [InlineData("algorithm")]
    [InlineData("session")]
    public async Task Invalid_JWT_properties_are_rejected(string invalid)
    {
        await using var factory = new ApiFactory(); using var client = await factory.ClientAsync();
        var session = await AuthFlowTests.RegisterLoginAsync(factory, client);
        var jwt = new JwtSecurityTokenHandler().ReadJwtToken(session.Access);
        var settings = factory.Services.GetRequiredService<IOptions<JwtSettings>>().Value;
        var now = DateTime.UtcNow;
        var claims = jwt.Claims.Where(x => x.Type is "sub" or "sid" or "email" or "role").ToList();
        if (invalid == "session") { claims.RemoveAll(x => x.Type == "sid"); claims.Add(new Claim("sid", Guid.NewGuid().ToString())); }
        var key = invalid == "signature" ? RandomNumberGenerator.GetBytes(32) : Convert.FromBase64String(settings.SigningKey);
        var credentials = new SigningCredentials(new SymmetricSecurityKey(key), invalid == "algorithm" ? SecurityAlgorithms.HmacSha384 : SecurityAlgorithms.HmacSha256);
        // Test fixture uses a 64-byte configured key. HS384 has a valid signature here;
        // it must still be rejected by the explicit HS256 algorithm allowlist.
        var forged = new JwtSecurityToken(invalid == "issuer" ? "OtherIssuer" : settings.Issuer,
            invalid == "audience" ? "OtherAudience" : settings.Audience, claims,
            now.AddHours(-1), invalid == "expired" ? now.AddMinutes(-1) : now.AddMinutes(15), credentials);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", new JwtSecurityTokenHandler().WriteToken(forged));
        using var response = await client.GetAsync("/api/auth/me", Ct);
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        var body = await response.Content.ReadAsStringAsync(Ct); Assert.DoesNotContain("IDX", body);
    }
    [Fact]
    public async Task Inactive_account_is_rejected_even_with_valid_signature_and_session()
    {
        await using var factory = new ApiFactory(); using var client = await factory.ClientAsync();
        var session = await AuthFlowTests.RegisterLoginAsync(factory, client);
        await using var scope = factory.Services.CreateAsyncScope(); var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var user = await db.Users.SingleAsync(Ct); user.IsActive = false; await db.SaveChangesAsync(Ct);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", session.Access);
        using var denied = await client.GetAsync("/api/auth/me", Ct); Assert.Equal(HttpStatusCode.Unauthorized, denied.StatusCode);
    }
    [Fact]
    public async Task CORS_only_allows_configured_origin_and_does_not_trust_forged_proxy_headers()
    {
        await using var factory = new ApiFactory(); using var client = await factory.ClientAsync();
        using var allowedRequest = new HttpRequestMessage(HttpMethod.Options, "/api/auth/login");
        allowedRequest.Headers.Add("Origin", "https://localhost:5173"); allowedRequest.Headers.Add("Access-Control-Request-Method", "POST");
        using var allowed = await client.SendAsync(allowedRequest, Ct);
        Assert.Equal("https://localhost:5173", Assert.Single(allowed.Headers.GetValues("Access-Control-Allow-Origin")));
        using var blockedRequest = new HttpRequestMessage(HttpMethod.Options, "/api/auth/login");
        blockedRequest.Headers.Add("Origin", "https://attacker.example"); blockedRequest.Headers.Add("Access-Control-Request-Method", "POST");
        using var blocked = await client.SendAsync(blockedRequest, Ct); Assert.False(blocked.Headers.Contains("Access-Control-Allow-Origin"));
        using var plain = new HttpRequestMessage(HttpMethod.Get, "http://localhost/api/auth/me"); plain.Headers.Add("X-Forwarded-Proto", "https");
        using var spoofed = await client.SendAsync(plain, Ct); Assert.Equal(HttpStatusCode.BadRequest, spoofed.StatusCode);
    }
}
