using System.Security.Cryptography;
using System.Text;
using Microsoft.EntityFrameworkCore;
using SPWPOS.Application.Authentication;
using SPWPOS.Domain.Entities;
using SPWPOS.Infrastructure.Persistence;
namespace SPWPOS.Infrastructure.Security;
// Persistent and shared across replicas; invoked under the account transaction.
public sealed class AccountRateLimiter(ApplicationDbContext db, TimeProvider clock)
{
    public async Task CheckAsync(string normalizedEmail, string operation, int limit, TimeSpan window, TimeSpan? cooldown, CancellationToken ct)
    {
        var now = clock.GetUtcNow();
        var id = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes($"{normalizedEmail}:{operation}")));
        var state = await db.SecurityRateLimits.SingleOrDefaultAsync(x => x.Id == id, ct);
        if (state is null)
        {
            state = new SecurityRateLimit { Id = id, WindowStart = now, LastRequestAt = now, Count = 1 };
            db.SecurityRateLimits.Add(state); return;
        }
        if (cooldown is { } interval && now - state.LastRequestAt < interval)
            throw new AuthException("OTP_RESEND_COOLDOWN", "Please wait before requesting another code.", 429);
        if (now - state.WindowStart >= window) { state.WindowStart = now; state.Count = 0; }
        if (state.Count >= limit) throw new AuthException("RATE_LIMITED", "Too many requests. Please try again later.", 429);
        state.Count++; state.LastRequestAt = now;
    }
}
