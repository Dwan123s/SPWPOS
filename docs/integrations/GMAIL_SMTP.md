# Gửi email xác minh và nhắc nhở bằng Gmail

PLANORA dùng cùng cấu hình SMTP cho OTP, email đổi mật khẩu và nhắc deadline / phiên làm việc. Email nhắc nhở mặc định tắt; bật **Cài đặt → Thông báo → Gửi nhắc nhở qua email** để gửi đến email đã xác minh của tài khoản. Tùy chọn này độc lập với thông báo trong ứng dụng và không tắt email bảo mật.

Worker kiểm tra mỗi phút khi API đang chạy. Nhắc nhở được lưu trong database trước khi gửi, thử lại khi SMTP lỗi và bỏ qua khi công việc đã xong, đã xóa, phiên đã đổi giờ hoặc không còn phù hợp. Không cần mở trình duyệt để nhận email; backend phải đang chạy. Cấu hình Mailpit (`localhost:1025`) chỉ đưa email vào hộp thư development, chưa gửi đến Gmail thật.

Khi nâng cấp, áp dụng migration `AddReminderEmailDelivery` trước khi khởi động backend mới. Migration thêm cài đặt email và trạng thái gửi, giữ các thông báo hiện có; tài khoản hiện tại vẫn cần tự bật email nhắc nhở.

## Chuẩn bị tài khoản gửi

Đây là tài khoản Gmail dùng để **gửi** email cho tất cả người dùng SPWPOS, không phải mỗi người dùng phải cấu hình Gmail riêng.

1. Đăng nhập tài khoản gửi tại https://myaccount.google.com/security và bật **Xác minh 2 bước**.
2. Mở https://myaccount.google.com/apppasswords, tạo App Password tên `SPWPOS`.
3. Giữ mật khẩu ứng dụng 16 ký tự để nhập trong terminal. Không dùng mật khẩu đăng nhập Gmail, không gửi mật khẩu trong chat và không thêm vào Git.

Google có thể không cho tạo App Password nếu tài khoản tổ chức bị quản trị viên giới hạn, bật Advanced Protection hoặc chỉ dùng khóa bảo mật cho xác minh 2 bước. Dùng tài khoản được hỗ trợ hoặc provider khác.

Nguồn: [Google — App Passwords](https://support.google.com/accounts/answer/185833), [Google — SMTP cho ứng dụng](https://support.google.com/a/answer/176600).

## Cấu hình trên máy Windows

Trong PowerShell:

```powershell
# Từ thư mục repository PLANORA
.\scripts\Configure-Gmail.ps1 -RestartApi
```

Script hỏi địa chỉ Gmail gửi, rồi hỏi App Password bằng ô nhập ẩn. Script tự nhận SDK riêng của dự án, lưu cấu hình bằng JSON stdin vào User Secrets của `SPWPOS.Api`; mật khẩu không nằm trong command history/process arguments và không được in ra. `-RestartApi` dừng đúng API Debug/Release của checkout này rồi khởi động lại; giữ terminal mở sau khi API chạy. Bỏ switch nếu chỉ muốn lưu cấu hình.

| Cấu hình | Giá trị |
|---|---|
| Host | `smtp.gmail.com` |
| Port | `587` |
| EnableSsl | `true` (STARTTLS bắt buộc) |
| Username / SenderEmail | Tài khoản Gmail gửi đã nhập |
| Password | App Password đã nhập |
| SenderName | `SPWPOS` |

User Secrets dành cho development trên máy cá nhân và không phải kho secret được mã hóa. Production cần secret manager hoặc environment variables được triển khai an toàn. Các biến `EmailSettings__*` có độ ưu tiên cao hơn User Secrets; kiểm tra nếu script cảnh báo override.

## Khởi động và kiểm tra

1. Dừng API hiện tại bằng `Ctrl+C` trong terminal đang chạy API.
2. Khởi động lại:

```powershell
.\scripts\Start-Development.ps1 -SkipCertificateTrust
```

3. Mở https://localhost:7043/, đăng ký tài khoản hoặc chọn **Gửi lại mã** cho tài khoản chưa xác minh. Cooldown là 60 giây.
4. Mở Gmail của **người nhận**, kiểm tra Inbox, Spam và All Mail; dùng email mới nhất. OTP có hiệu lực 5 phút và chỉ dùng một lần.

Script khởi động giữ nguyên Gmail SMTP đã lưu. Muốn chuyển lại Mailpit một cách chủ động:

```powershell
.\scripts\Start-Development.ps1 -UseMailpit -SkipCertificateTrust
```

Thao tác này thay cấu hình SMTP trong User Secrets bằng Mailpit và xóa credential SMTP cũ khỏi các key đó. Các environment override vẫn có ưu tiên cao hơn.

Khởi động SQL giữ mật khẩu trong connection string đã lưu; bỏ qua `SPWPOS_SQL_PASSWORD` cũ khác mật khẩu này để tránh health check lỗi trên volume hiện có. Không chạy nhiều script khởi động đồng thời. Cảnh báo volume `swp_spwpos-sql-data` cũ được giữ nguyên không yêu cầu xóa dữ liệu.

## Khi email không đến

- Lỗi xác thực SMTP / `535`: kiểm tra Username và App Password; mật khẩu Gmail thông thường không dùng được. Google thu hồi App Password khi đổi mật khẩu Google Account; cần tạo lại.
- Timeout: kiểm tra kết nối mạng/firewall cho TCP `587`; không tắt TLS hoặc certificate validation.
- Gmail giới hạn gửi: chờ hoặc dùng provider transactional phù hợp cho production.
- API chưa restart: process đang chạy chưa nhận cấu hình mới; dừng và chạy lại.
- Email vẫn vào Mailpit: kiểm tra biến `EmailSettings__Host`/`Port` đang override User Secrets.

Chỉ sau khi nhận được OTP thật trong Gmail mới xác nhận delivery thành công. Script cấu hình không tự gửi thư hay ghi OTP vào log.

## Kiểm tra kết nối bằng chính MailKit của API

Sau khi build Release:

```powershell
. .\scripts\Initialize-Dotnet.ps1
& $SpwposDotnet run --no-build --configuration Release --project src/SPWPOS.Api --launch-profile https -- --check-smtp
```

Lệnh kiểm tra TLS và SMTP authentication, không gửi email, không mở HTTP listener và không in credential. Exit code 0 nghĩa là kết nối/xác thực thành công; vẫn cần gửi OTP để kiểm tra gửi thư.

Ở máy đang dùng Codex dạng Windows packaged app, APPDATA có thể được virtualize vào LocalCache. Script SDK chọn đường dẫn filesystem loopback tới APPDATA thật nếu có quyền truy cập, để terminal và API đọc cùng User Secrets. Không tạo network share hoặc thay ACL; nếu share hiện có không truy cập được thì giữ đường dẫn process hiện tại. Các script chuẩn của dự án cần được dùng khi khởi động từ môi trường đó.
