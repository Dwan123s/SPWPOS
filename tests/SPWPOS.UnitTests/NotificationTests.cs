using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SPWPOS.Domain.Enums;
using SPWPOS.Infrastructure.Email;
using SPWPOS.TestSupport;
namespace SPWPOS.UnitTests;

public sealed class NotificationTests
{
    [Fact]
    public async Task SMTP_failure_keeps_password_changed_and_persists_notification_for_retry()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var h = await ServiceHarness.CreateAsync(); var user = await h.RegisterAsync();
        await h.LoginAsync(); await h.AuthAsync(a => a.RequestChangePasswordOtpAsync(user.Id, ServiceHarness.Context, ct));
        var otp = h.Email.Inbox[(ServiceHarness.Address, OtpPurpose.ChangePassword)]; h.Email.FailDelivery = true;
        await h.AuthAsync(a => a.ChangePasswordAsync(user.Id, new(ServiceHarness.Password, otp, ServiceHarness.NewPassword, ServiceHarness.NewPassword), ServiceHarness.Context, ct));
        var notification = await h.DbAsync(db => db.PasswordChangeNotifications.SingleAsync(ct));
        Assert.Null(notification.SentAt); Assert.Equal(1, notification.Attempts);
        Assert.True(await h.DbAsync(db => db.RefreshTokens.AllAsync(x => x.RevokedAt != null, ct)));
        await h.LoginAsync(ServiceHarness.NewPassword);
        h.Email.FailDelivery = false; h.Clock.Advance(TimeSpan.FromMinutes(2));
        await using var scope = h.Provider.CreateAsyncScope();
        await scope.ServiceProvider.GetRequiredService<PasswordNotificationService>().SendPendingAsync(user.Id, ct);
        var sent = await h.DbAsync(db => db.PasswordChangeNotifications.SingleAsync(ct));
        Assert.NotNull(sent.SentAt); Assert.Equal(2, sent.Attempts); Assert.Equal(1, h.Email.Notifications);
    }
}
