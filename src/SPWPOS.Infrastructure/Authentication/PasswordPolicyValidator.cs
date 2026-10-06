using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Options;
using SPWPOS.Application.Authentication;
using SPWPOS.Domain.Entities;
namespace SPWPOS.Infrastructure.Authentication;

public sealed class PasswordPolicyValidator(IOptions<PasswordSettings> options) : IPasswordValidator<ApplicationUser>
{
    private static readonly HashSet<string> Blocklist = new(StringComparer.OrdinalIgnoreCase)
    { "123456789", "1234567890", "123456789012", "password", "password123", "Password123!", "qwerty123", "qwerty12345", "abcdefghij", "letmein12345", "admin123456" };
    public Task<IdentityResult> ValidateAsync(UserManager<ApplicationUser> manager, ApplicationUser user, string? password)
    {
        var s = options.Value;
        var valid = password is not null && password.Length >= s.MinLength && password.Length <= s.MaxLength && !Blocklist.Contains(password);
        return Task.FromResult(valid ? IdentityResult.Success : IdentityResult.Failed(new IdentityError
        { Code = "PASSWORD_POLICY_FAILED", Description = $"Use {s.MinLength}–{s.MaxLength} characters and avoid common passwords." }));
    }
}
