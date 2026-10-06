using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Options;
using SPWPOS.Application.Authentication;
using SPWPOS.Domain.Entities;
using SPWPOS.Infrastructure.Authentication;
namespace SPWPOS.UnitTests;

public sealed class PasswordTests
{
    private readonly ApplicationUser user = new();
    private readonly Argon2PasswordHasher hasher = new(Options.Create(new PasswordSettings()));
    [Fact]
    public void Hash_is_phc_argon2id_with_unique_salt()
    {
        var first = hasher.HashPassword(user, "a long password 123!");
        var second = hasher.HashPassword(user, "a long password 123!");
        Assert.StartsWith("$argon2id$v=19$m=32768,t=3,p=1$", first);
        Assert.NotEqual(first, second); Assert.NotEqual(first.Split('$')[4], second.Split('$')[4]);
        Assert.True(Sodium.PasswordHash.ArgonHashStringVerify(first, "a long password 123!"));
    }
    [Theory]
    [InlineData("a long password 123!", true)]
    [InlineData("wrong password", false)]
    [InlineData(" a long password 123!", false)]
    [InlineData("a long password 123! ", false)]
    public void Verify_respects_exact_password(string submitted, bool success)
    {
        var hash = hasher.HashPassword(user, "a long password 123!");
        Assert.Equal(success ? PasswordVerificationResult.Success : PasswordVerificationResult.Failed, hasher.VerifyHashedPassword(user, hash, submitted));
    }
    [Theory]
    [InlineData("$argon2id$v=19$m=2147483647,t=3,p=1$bad$bad")]
    [InlineData("$argon2id$v=19$m=32768,t=99999,p=1$bad$bad")]
    [InlineData("invalid")]
    [InlineData("$argon2id$v=19$m=32768,t=3,p=8$bad$bad")]
    public void Corrupt_or_excessive_parameters_fail_closed(string hash) => Assert.Equal(PasswordVerificationResult.Failed, hasher.VerifyHashedPassword(user, hash, "password"));
    [Fact]
    public void Long_unicode_password_is_supported_without_truncation()
    {
        string password = new('界', 128);
        var hash = hasher.HashPassword(user, password);
        Assert.Equal(PasswordVerificationResult.Success, hasher.VerifyHashedPassword(user, hash, password));
        Assert.Throws<ArgumentException>(() => hasher.HashPassword(user, password + "x"));
    }
    [Fact]
    public void Stronger_parameters_request_rehash()
    {
        var low = new Argon2PasswordHasher(Options.Create(new PasswordSettings { MemoryKiB = 19456 }));
        Assert.Equal(PasswordVerificationResult.SuccessRehashNeeded, hasher.VerifyHashedPassword(user, low.HashPassword(user, "long passphrase"), "long passphrase"));
    }
}
