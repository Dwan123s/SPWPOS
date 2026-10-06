using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using SPWPOS.Application.Authentication;
using SPWPOS.Application.Authentication.Interfaces;
using SPWPOS.Domain.Entities;
namespace SPWPOS.Infrastructure.Authentication;

public sealed class JwtTokenService(IOptions<JwtSettings> options, TimeProvider clock) : IJwtTokenService
{
    private readonly JwtSettings settings = options.Value;
    public (string Token, DateTimeOffset ExpiresAt) GenerateAccessToken(ApplicationUser user, Guid familyId, IEnumerable<string> roles)
    {
        var now = clock.GetUtcNow(); var expiry = now.AddMinutes(settings.AccessTokenMinutes);
        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, user.Id.ToString()),
            new(JwtRegisteredClaimNames.Email, user.Email!),
            new(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),
            new(JwtRegisteredClaimNames.Iat, now.ToUnixTimeSeconds().ToString(System.Globalization.CultureInfo.InvariantCulture), ClaimValueTypes.Integer64),
            new("sid", familyId.ToString())
        };
        var roleList = roles.ToArray();
        foreach (var role in roleList.Length == 0 ? ["User"] : roleList) claims.Add(new Claim("role", role));
        var credentials = new SigningCredentials(new SymmetricSecurityKey(Convert.FromBase64String(settings.SigningKey)), SecurityAlgorithms.HmacSha256);
        var token = new JwtSecurityToken(settings.Issuer, settings.Audience, claims, now.UtcDateTime, expiry.UtcDateTime, credentials);
        return (new JwtSecurityTokenHandler().WriteToken(token), expiry);
    }
    public string GenerateRefreshToken() => Base64UrlEncoder.Encode(RandomNumberGenerator.GetBytes(64));
    // SHA256 is appropriate for high-entropy random tokens; it is NEVER used for passwords.
    public string HashToken(string token) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(token)));
}
