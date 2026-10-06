using System.Security.Cryptography;
using Microsoft.Extensions.Options;
using SPWPOS.Application.Authentication;
using SPWPOS.Domain.Entities;
namespace SPWPOS.Infrastructure.Authentication;
// Equalize the expensive password check for unknown emails without keeping any real credential.
public sealed class PasswordTiming(IOptions<PasswordSettings> options)
{
    public string DummyHash { get; } = new Argon2PasswordHasher(options).HashPassword(new ApplicationUser(), Convert.ToBase64String(RandomNumberGenerator.GetBytes(32)));
}
