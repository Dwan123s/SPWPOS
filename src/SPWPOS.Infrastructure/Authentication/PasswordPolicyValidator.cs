using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Options;
using System.Text.RegularExpressions;
using SPWPOS.Application.Authentication;
using SPWPOS.Domain.Entities;
namespace SPWPOS.Infrastructure.Authentication;

public sealed class PasswordPolicyValidator(IOptions<PasswordSettings> options) : IPasswordValidator<ApplicationUser>
{
    private static readonly Regex Composition = new(
        @"\A(?=[\s\S]*[0-9])(?=[\s\S]*[a-z])(?=[\s\S]*[A-Z])(?=[\s\S]*[^A-Za-z0-9\s])[\s\S]*\z",
        RegexOptions.CultureInvariant, TimeSpan.FromMilliseconds(100));
    private static readonly string[] ForbiddenText = ["password", "websitename", "planora"];
    private static readonly HashSet<string> Blocklist = new(StringComparer.OrdinalIgnoreCase)
    { "123456789", "1234567890", "123456789012", "password", "password123", "Password123!", "qwerty123", "qwerty12345", "abcdefghij", "letmein12345", "admin123456" };
    public Task<IdentityResult> ValidateAsync(UserManager<ApplicationUser> manager, ApplicationUser user, string? password)
    {
        var s = options.Value;
        var username = user.UserName?.Trim();
        var emailName = user.Email?.Split('@')[0].Trim();
        var valid = password is not null && password.Length >= s.MinLength && password.Length <= s.MaxLength
            && Composition.IsMatch(password) && !Blocklist.Contains(password)
            && !ForbiddenText.Any(value => password.Contains(value, StringComparison.OrdinalIgnoreCase))
            && (string.IsNullOrEmpty(username) || !password.Contains(username, StringComparison.OrdinalIgnoreCase))
            && (string.IsNullOrEmpty(emailName) || !password.Contains(emailName, StringComparison.OrdinalIgnoreCase));
        return Task.FromResult(valid ? IdentityResult.Success : IdentityResult.Failed(new IdentityError
        { Code = "PASSWORD_POLICY_FAILED", Description = $"Use {s.MinLength}–{s.MaxLength} characters including a number, lowercase and uppercase letters, and a special character other than whitespace. Do not include your username, 'password', 'websitename', or 'PLANORA', or use a common password." }));
    }
}
