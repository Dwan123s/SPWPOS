using System.Security.Cryptography;
using System.Text;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using SPWPOS.Application.Authentication;
using SPWPOS.Application.Authentication.Interfaces;
using SPWPOS.Domain.Entities;
using SPWPOS.Domain.Enums;
using SPWPOS.Infrastructure.Persistence;
namespace SPWPOS.Infrastructure.Authentication;

public sealed class OtpService(ApplicationDbContext db, IOptions<OtpSettings> options, TimeProvider clock) : IOtpService
{
    private readonly OtpSettings settings = options.Value;
    private readonly byte[] secret = Convert.FromBase64String(options.Value.SecretKey);
    public async Task<string> GenerateOtpAsync(Guid userId, OtpPurpose purpose, CancellationToken ct)
    {
        var now = clock.GetUtcNow();
        var previous = await db.EmailOtps.Where(x => x.UserId == userId && x.Purpose == purpose)
            .OrderByDescending(x => x.CreatedAt).FirstOrDefaultAsync(ct);
        if (previous is not null && now - previous.CreatedAt < TimeSpan.FromSeconds(settings.ResendCooldownSeconds))
            throw new AuthException("OTP_RESEND_COOLDOWN", "Please wait before requesting another code.", 429);
        if (await db.EmailOtps.CountAsync(x => x.UserId == userId && x.Purpose == purpose && x.CreatedAt > now.AddMinutes(-15), ct) >= 5)
            throw new AuthException("RATE_LIMITED", "Too many code requests. Please try again later.", 429);
        await InvalidateOtpAsync(userId, purpose, ct);
        // Persist invalidation first so the filtered unique active-OTP index remains valid.
        await db.SaveChangesAsync(ct);
        string otp = new(Enumerable.Range(0, settings.Length).Select(_ => (char)('0' + RandomNumberGenerator.GetInt32(10))).ToArray());
        var entity = new EmailOtp
        { UserId = userId, Purpose = purpose, ExpiresAt = now.AddMinutes(settings.ExpirationMinutes), CreatedAt = now, MaxAttempts = settings.MaxAttempts };
        entity.OtpHash = ComputeHash(entity, otp);
        db.EmailOtps.Add(entity);
        return otp;
    }
    public async Task VerifyOtpAsync(Guid userId, OtpPurpose purpose, string otp, CancellationToken ct)
    {
        var entity = await db.EmailOtps.Where(x => x.UserId == userId && x.Purpose == purpose)
            .OrderByDescending(x => x.CreatedAt).FirstOrDefaultAsync(ct);
        if (entity is null) throw Invalid();
        if (entity.AttemptCount >= entity.MaxAttempts)
            throw new AuthException("OTP_TOO_MANY_ATTEMPTS", "Request a new verification code.");
        if (entity.IsUsed) throw Invalid();
        if (entity.ExpiresAt <= clock.GetUtcNow())
        {
            Use(entity);
            throw new AuthException("OTP_EXPIRED", "The verification code has expired.");
        }
        var submittedHash = ComputeHash(entity, otp);
        if (otp.Length != settings.Length || !CryptographicOperations.FixedTimeEquals(entity.OtpHash, submittedHash))
        {
            entity.AttemptCount++;
            if (entity.AttemptCount >= entity.MaxAttempts)
            {
                Use(entity);
                throw new AuthException("OTP_TOO_MANY_ATTEMPTS", "Request a new verification code.");
            }
            throw Invalid();
        }
        Use(entity);
    }
    public async Task InvalidateOtpAsync(Guid userId, OtpPurpose? purpose, CancellationToken ct)
    {
        var active = await db.EmailOtps.Where(x => x.UserId == userId && !x.IsUsed && (purpose == null || x.Purpose == purpose)).ToListAsync(ct);
        foreach (var otp in active) Use(otp);
    }
    private byte[] ComputeHash(EmailOtp entity, string otp)
    {
        // Bind HMAC to the issuance id, account, and purpose to prevent hash swapping and
        // cross-account/purpose reuse, including identical 6-digit codes.
        var input = Encoding.UTF8.GetBytes($"{entity.Id:N}:{entity.UserId:N}:{(int)entity.Purpose}:{otp}");
        return HMACSHA256.HashData(secret, input);
    }
    private void Use(EmailOtp entity) { entity.IsUsed = true; entity.UsedAt = clock.GetUtcNow(); }
    private static AuthException Invalid() => new("OTP_INVALID", "Invalid or already used verification code.");
}
