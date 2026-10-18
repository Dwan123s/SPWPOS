using System.Net;
using MailKit.Net.Smtp;
using MailKit.Security;
using Microsoft.Extensions.Options;
using MimeKit;
using SPWPOS.Application.Authentication;
using SPWPOS.Application.Authentication.Interfaces;

namespace SPWPOS.Infrastructure.Email;

public interface IEmailConnectionCheck
{
    Task CheckConnectionAsync(CancellationToken ct);
}

public sealed class EmailService(IOptions<EmailSettings> emailOptions, IOptions<OtpSettings> otpOptions) : IEmailService, IEmailConnectionCheck
{
    private readonly EmailSettings settings = emailOptions.Value;

    public Task SendEmailVerificationOtpAsync(string email, string otp, CancellationToken ct) =>
        SendOtpAsync(email, otp, "Xác minh địa chỉ Email", ct);

    public Task SendForgotPasswordOtpAsync(string email, string otp, CancellationToken ct) =>
        SendOtpAsync(email, otp, "Khôi phục mật khẩu tài khoản", ct);

    public Task SendChangePasswordOtpAsync(string email, string otp, CancellationToken ct) =>
        SendOtpAsync(email, otp, "Xác nhận thay đổi mật khẩu", ct);

    public Task SendPasswordChangedAsync(string email, CancellationToken ct) => SendAsync(
        email,
        "Mật khẩu tài khoản đã được thay đổi",
        "Your PLANORA password has been changed. All active sessions have been signed out. If this was not you, please reset your password immediately.",
        BuildPasswordChangedHtmlTemplate(),
        ct);

    private Task SendOtpAsync(string email, string otp, string subjectTitle, CancellationToken ct)
    {
        var minutes = otpOptions.Value.ExpirationMinutes;
        string text = $"PLANORA - {subjectTitle}\n\nMã xác minh của bạn là: {otp}\n\nMã này có hiệu lực trong {minutes} phút. Tuyệt đối không chia sẻ mã này cho bất kỳ ai.\n\nNếu bạn không yêu cầu mã này, vui lòng bỏ qua email.";
        string html = BuildOtpHtmlTemplate(subjectTitle, otp, minutes);
        return SendAsync(email, subjectTitle, text, html, ct);
    }

    public Task SendReminderAsync(string email, string title, string message, CancellationToken ct) =>
        SendAsync(email, title, $"{message}\n\nMở PLANORA để xem công việc và lịch làm việc. Bạn có thể tắt nhắc qua email trong Cài đặt → Thông báo.",
            $"<html lang=\"vi\"><body><h1>PLANORA</h1><h2>{WebUtility.HtmlEncode(title)}</h2><p>{WebUtility.HtmlEncode(message)}</p><p>Mở PLANORA để xem công việc và lịch làm việc.</p><p>Bạn có thể tắt nhắc qua email trong Cài đặt → Thông báo.</p></body></html>", ct);

    private async Task SendAsync(string email, string subject, string text, string? html, CancellationToken ct)
    {
        var message = new MimeMessage();
        message.From.Add(new MailboxAddress(settings.SenderName, settings.SenderEmail));
        message.To.Add(MailboxAddress.Parse(email));
        message.Subject = $"[PLANORA] {subject}";
        message.Body = new BodyBuilder { TextBody = text, HtmlBody = html }.ToMessageBody();

        using var client = new SmtpClient { Timeout = settings.TimeoutSeconds * 1000 };
        await ConnectAsync(client, ct);
        await client.SendAsync(message, ct);
        await client.DisconnectAsync(true, ct);
    }

    public async Task CheckConnectionAsync(CancellationToken ct)
    {
        using var client = new SmtpClient { Timeout = settings.TimeoutSeconds * 1000 };
        await ConnectAsync(client, ct);
        await client.DisconnectAsync(true, ct);
    }

    private async Task ConnectAsync(SmtpClient client, CancellationToken ct)
    {
        var security = settings.EnableSsl ? (settings.Port == 465 ? SecureSocketOptions.SslOnConnect : SecureSocketOptions.StartTls) : SecureSocketOptions.None;
        await client.ConnectAsync(settings.Host, settings.Port, security, ct);
        if (!string.IsNullOrEmpty(settings.Username))
        {
            await client.AuthenticateAsync(settings.Username, settings.Password, ct);
        }
    }

    private static string BuildOtpHtmlTemplate(string subjectTitle, string otpCode, int expirationMinutes)
    {
        return $"""
        <!DOCTYPE html>
        <html lang="vi">
        <head>
          <meta charset="utf-8">
          <meta name="viewport" content="width=device-width, initial-scale=1.0">
          <title>PLANORA - {WebUtility.HtmlEncode(subjectTitle)}</title>
        </head>
        <body style="margin:0;padding:0;background-color:#f4f6fb;font-family:-apple-system,BlinkMacSystemFont,'Segoe UI',Roboto,Helvetica,Arial,sans-serif;color:#1e293b;">
          <table role="presentation" width="100%" cellspacing="0" cellpadding="0" border="0" style="background-color:#f4f6fb;padding:30px 15px;">
            <tr>
              <td align="center">
                <table role="presentation" width="100%" cellspacing="0" cellpadding="0" border="0" style="max-width:560px;background-color:#ffffff;border-radius:16px;overflow:hidden;box-shadow:0 4px 12px rgba(0,0,0,0.05);border:1px solid #e2e8f0;">
                  <tr>
                    <td style="background:linear-gradient(135deg,#0891b2 0%,#0e7490 100%);padding:28px 32px;text-align:center;">
                      <h1 style="margin:0;color:#ffffff;font-size:24px;font-weight:700;letter-spacing:-0.5px;">PLANORA</h1>
                      <p style="margin:6px 0 0 0;color:#cffafe;font-size:13px;">Personal Workload Planning &amp; Optimization</p>
                    </td>
                  </tr>
                  <tr>
                    <td style="padding:32px 32px 24px 32px;">
                      <h2 style="margin:0 0 12px 0;color:#0f172a;font-size:18px;font-weight:600;">{WebUtility.HtmlEncode(subjectTitle)}</h2>
                      <p style="margin:0 0 20px 0;color:#475569;font-size:14px;line-height:1.6;">
                        Xin chào! Bạn vừa yêu cầu mã xác minh từ hệ thống PLANORA. Vui lòng sử dụng mã bên dưới để hoàn tất thao tác:
                      </p>
                      <table role="presentation" width="100%" cellspacing="0" cellpadding="0" border="0" style="margin:20px 0;background-color:#ecfeff;border:2px dashed #06b6d4;border-radius:12px;">
                        <tr>
                          <td align="center" style="padding:20px;">
                            <span style="font-family:'Courier New',Courier,monospace;font-size:36px;font-weight:700;color:#0891b2;letter-spacing:10px;display:inline-block;">{WebUtility.HtmlEncode(otpCode)}</span>
                            <p style="margin:10px 0 0 0;color:#64748b;font-size:12px;">
                              Mã có hiệu lực trong <strong>{expirationMinutes} phút</strong>.
                            </p>
                          </td>
                        </tr>
                      </table>
                      <div style="background-color:#fffbe6;border:1px solid #ffe58f;border-radius:8px;padding:12px 16px;margin-top:20px;">
                        <p style="margin:0;color:#b78103;font-size:12px;line-height:1.5;">
                          <strong>Cảnh báo an toàn:</strong> Tuyệt đối không chia sẻ mã OTP này cho bất kỳ ai để bảo vệ tài khoản của bạn.
                        </p>
                      </div>
                    </td>
                  </tr>
                  <tr>
                    <td style="background-color:#f8fafc;padding:20px 32px;border-top:1px solid #f1f5f9;text-align:center;">
                      <p style="margin:0;color:#94a3b8;font-size:12px;line-height:1.5;">
                        Nếu bạn không thực hiện yêu cầu này, vui lòng bỏ qua email.<br>
                        &copy; PLANORA Workload Management System.
                      </p>
                    </td>
                  </tr>
                </table>
              </td>
            </tr>
          </table>
        </body>
        </html>
        """;
    }

    private static string BuildPasswordChangedHtmlTemplate()
    {
        return """
        <!DOCTYPE html>
        <html lang="vi">
        <head>
          <meta charset="utf-8">
          <meta name="viewport" content="width=device-width, initial-scale=1.0">
          <title>PLANORA - Mật khẩu đã thay đổi</title>
        </head>
        <body style="margin:0;padding:0;background-color:#f4f6fb;font-family:-apple-system,BlinkMacSystemFont,'Segoe UI',Roboto,Helvetica,Arial,sans-serif;color:#1e293b;">
          <table role="presentation" width="100%" cellspacing="0" cellpadding="0" border="0" style="background-color:#f4f6fb;padding:30px 15px;">
            <tr>
              <td align="center">
                <table role="presentation" width="100%" cellspacing="0" cellpadding="0" border="0" style="max-width:560px;background-color:#ffffff;border-radius:16px;overflow:hidden;box-shadow:0 4px 12px rgba(0,0,0,0.05);border:1px solid #e2e8f0;">
                  <tr>
                    <td style="background:linear-gradient(135deg,#0891b2 0%,#0e7490 100%);padding:28px 32px;text-align:center;">
                      <h1 style="margin:0;color:#ffffff;font-size:24px;font-weight:700;letter-spacing:-0.5px;">PLANORA</h1>
                      <p style="margin:6px 0 0 0;color:#cffafe;font-size:13px;">Personal Workload Planning &amp; Optimization</p>
                    </td>
                  </tr>
                  <tr>
                    <td style="padding:32px 32px 24px 32px;">
                      <h2 style="margin:0 0 12px 0;color:#0f172a;font-size:18px;font-weight:600;">Mật khẩu tài khoản đã được thay đổi</h2>
                      <p style="margin:0 0 16px 0;color:#475569;font-size:14px;line-height:1.6;">
                        Mật khẩu tài khoản PLANORA của bạn vừa được thay đổi thành công. Tất cả các phiên đăng nhập trên các thiết bị khác đã tự động đăng xuất để bảo vệ an toàn cho tài khoản.
                      </p>
                      <div style="background-color:#fef2f2;border:1px solid #fecaca;border-radius:8px;padding:14px 16px;margin-top:20px;">
                        <p style="margin:0;color:#991b1b;font-size:12px;line-height:1.5;">
                          <strong>Nếu không phải bạn thực hiện:</strong> Vui lòng sử dụng chức năng Khôi phục mật khẩu ngay lập tức hoặc liên hệ quản trị viên.
                        </p>
                      </div>
                    </td>
                  </tr>
                  <tr>
                    <td style="background-color:#f8fafc;padding:20px 32px;border-top:1px solid #f1f5f9;text-align:center;">
                      <p style="margin:0;color:#94a3b8;font-size:12px;line-height:1.5;">
                        &copy; PLANORA Workload Management System.
                      </p>
                    </td>
                  </tr>
                </table>
              </td>
            </tr>
          </table>
        </body>
        </html>
        """;
    }
}
