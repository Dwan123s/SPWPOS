using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Options;
using Sodium;
using SPWPOS.Application.Authentication;
using SPWPOS.Domain.Entities;
namespace SPWPOS.Infrastructure.Authentication;

public sealed class Argon2PasswordHasher(IOptions<PasswordSettings> options) : IPasswordHasher<ApplicationUser>
{
    private readonly PasswordSettings settings = options.Value;
    public string HashPassword(ApplicationUser user, string password)
    {
        ArgumentNullException.ThrowIfNull(password);
        if (password.Length > settings.MaxLength) throw new ArgumentException("Password exceeds maximum length.", nameof(password));
        var salt = RandomNumberGenerator.GetBytes(16);
        var hash = Derive(password, salt, settings.MemoryKiB, settings.Iterations);
        return $"$argon2id$v=19$m={settings.MemoryKiB},t={settings.Iterations},p=1${B64(salt)}${B64(hash)}";
    }
    public PasswordVerificationResult VerifyHashedPassword(ApplicationUser user, string hashedPassword, string providedPassword)
    {
        if (providedPassword.Length > settings.MaxLength || hashedPassword.Length > 256) return PasswordVerificationResult.Failed;
        try
        {
            var parts = hashedPassword.Split('$');
            if (parts.Length != 6 || parts[0] != "" || parts[1] != "argon2id" || parts[2] != "v=19") return PasswordVerificationResult.Failed;
            var parameters = parts[3].Split(',');
            if (parameters.Length != 3 || !parameters[0].StartsWith("m=", StringComparison.Ordinal) ||
                !parameters[1].StartsWith("t=", StringComparison.Ordinal) || parameters[2] != "p=1") return PasswordVerificationResult.Failed;
            int memory = int.Parse(parameters[0][2..], CultureInfo.InvariantCulture);
            int iterations = int.Parse(parameters[1][2..], CultureInfo.InvariantCulture);
            // Bound parsed parameters to avoid resource exhaustion on corrupted hashes.
            if (memory is < 19456 or > 262144 || iterations is < 3 or > 10) return PasswordVerificationResult.Failed;
            var salt = FromB64(parts[4]); var expected = FromB64(parts[5]);
            if (salt.Length != 16 || expected.Length != 32) return PasswordVerificationResult.Failed;
            var actual = Derive(providedPassword, salt, memory, iterations);
            if (!CryptographicOperations.FixedTimeEquals(actual, expected)) return PasswordVerificationResult.Failed;
            return memory < settings.MemoryKiB || iterations < settings.Iterations
                ? PasswordVerificationResult.SuccessRehashNeeded : PasswordVerificationResult.Success;
        }
        catch (Exception ex) when (ex is FormatException or OverflowException or ArgumentException)
        { return PasswordVerificationResult.Failed; }
    }
    private static byte[] Derive(string password, byte[] salt, int memory, int iterations)
    {
        byte[] bytes = Encoding.UTF8.GetBytes(password);
        try { return PasswordHash.ArgonHashBinary(bytes, salt, iterations, checked(memory * 1024), 32, PasswordHash.ArgonAlgorithm.Argon_2ID13); }
        finally { CryptographicOperations.ZeroMemory(bytes); }
    }
    private static string B64(byte[] bytes) => Convert.ToBase64String(bytes).TrimEnd('=');
    private static byte[] FromB64(string text) => Convert.FromBase64String(text.PadRight((text.Length + 3) / 4 * 4, '='));
}
