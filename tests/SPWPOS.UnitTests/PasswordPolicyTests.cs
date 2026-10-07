using Microsoft.Extensions.Options;
using SPWPOS.Application.Authentication;
using SPWPOS.Domain.Entities;
using SPWPOS.Infrastructure.Authentication;
namespace SPWPOS.UnitTests;

public sealed class PasswordPolicyTests
{
    [Theory]
    [InlineData("Abcdef1!", true)]
    [InlineData("Ab1!aaaaaaaaaaaaaaaaaaaaaaaaaaaa", true)]
    [InlineData("Abcde1!", false)]
    [InlineData("Ab1!aaaaaaaaaaaaaaaaaaaaaaaaaaaaa", false)]
    [InlineData("abcdefgh1!", false)]
    [InlineData("ABCDEFGH1!", false)]
    [InlineData("Abcdefgh!", false)]
    [InlineData("Abcdefgh1", false)]
    [InlineData("Abcdef1 ", false)]
    [InlineData("Abcdef1\n", false)]
    [InlineData("Abcdef١!", false)]
    [InlineData("SafePASSWORD1!", false)]
    [InlineData("SafeWebSiteName1!", false)]
    [InlineData("SafePlAnOrA1!", false)]
    [InlineData("SafeMEMBER1!", false)]
    [InlineData("Member@example.test1!", false)]
    [InlineData("Rainy morning with coffee 42!", true)]
    [InlineData(" Abcdef1! ", true)]
    [InlineData(null, false)]
    public async Task Enforces_password_requirements(string? password, bool accepted)
    {
        var validator = new PasswordPolicyValidator(Options.Create(new PasswordSettings()));
        var user = new ApplicationUser { UserName = "member@example.test", Email = "member@example.test" };
        var result = await validator.ValidateAsync(null!, user, password);
        Assert.Equal(accepted, result.Succeeded);
        if (!accepted) Assert.Equal("PASSWORD_POLICY_FAILED", Assert.Single(result.Errors).Code);
    }

    [Fact]
    public async Task Username_is_a_literal_case_insensitive_substring()
    {
        var validator = new PasswordPolicyValidator(Options.Create(new PasswordSettings()));
        var user = new ApplicationUser { UserName = "a.b+", Email = "a.b+@example.test" };
        Assert.False((await validator.ValidateAsync(null!, user, "SafeA.B+1!")).Succeeded);
        Assert.True((await validator.ValidateAsync(null!, user, "SafeAxB+1!")).Succeeded);
    }
}
