# Rà soát hệ thống SPWPOS — 04/10/2026

## Nguyên nhân lỗi Gmail và SQL startup

Hai bản User Secrets cùng ID được sử dụng: bản trong Windows APPDATA thật và bản APPDATA bị virtualize trong cache ứng dụng Codex. Terminal người dùng đã lưu Gmail thành công vào bản thật; API/lệnh chạy từ Codex vẫn đọc Mailpit và một connection string cũ từ cache. Hai mật khẩu SQL được gán luân phiên vào container, trong khi mật khẩu login đã lưu trong database volume chỉ có một giá trị. Vì vậy SQL Server khởi động nhưng health check đăng nhập `sa` thất bại.

Đã xác minh bằng metadata của hai secret store, so sánh credential trong bộ nhớ và thử đăng nhập SQL. Không in App Password, password SQL, signing key, OTP hay token. Password SQL có hiệu lực đã được kiểm tra trước khi cập nhật connection string trong store thật; Gmail/App Password và những cấu hình khác được giữ nguyên.

## Các bản sửa

| Phần | Thay đổi | Kết quả |
|---|---|---|
| `scripts/Initialize-Dotnet.ps1` | Chọn APPDATA thật qua filesystem loopback khi đường dẫn có quyền truy cập; tránh cache MSIX | CLI/EF/API và terminal dùng cùng cấu hình |
| `scripts/Initialize-Development.ps1` | Giữ SMTP đã lưu, ưu tiên password database đã lưu hơn biến SQL cũ, giữ JWT/OTP keys; chỉ rotate khi yêu cầu | Restart không mất Gmail, không tự làm OTP/JWT cũ sai |
| `scripts/Configure-Gmail.ps1` | Nhập App Password ẩn, JSON stdin, đọc lại để xác minh lưu thành công; restart đúng API của checkout | Credential không vào code, command history hay process arguments |
| `scripts/Start-Development.ps1` | Nhận diện API đã chạy, khóa file trong quá trình startup, giải phóng trước khi chạy API | Tránh khởi động chồng hoặc chạy API trùng cổng |
| `compose.yaml` | External SQL volume được setup quản lý; health check truyền password bằng env thay vì `-P` | Giữ dữ liệu; không đưa password vào argv của sqlcmd |
| `EmailService` + CLI `--check-smtp` | Dùng chung bước TLS/authentication của MailKit cho gửi email và diagnostic | Kiểm tra cấu hình thật không cần gửi thư hoặc expose diagnostic endpoint |
| `PlanningEngine` | Tính phiên giữ ngoài horizon vào thời lượng đã xếp; không tính phiên missed đã qua như công việc đã hoàn thành | Không lập trùng phiên ngoài horizon; missed work được lập lại |

Legacy volume `swp_spwpos-sql-data` được giữ nguyên. Cảnh báo Compose về label của volume cũ vẫn có thể xuất hiện, nhưng không làm SQL unhealthy; không xóa volume để xử lý cảnh báo đó.

## Phạm vi đã rà soát và chạy hồi quy

| Nhóm | Nội dung kiểm tra |
|---|---|
| Authentication/Account | Argon2id, salt, policy, verify email, OTP purpose/expiry/reuse/counters, login/lockout, reset/change password, security stamp và session revocation |
| JWT/Refresh | Issuer/audience/signature/lifetime, rotation, reuse detection, revoke family, cookie HttpOnly/Secure và CSRF |
| API | DTO validation, ProblemDetails, HTTPS, request size, CORS, proxy trust, rate limit và audit không chứa secret |
| Task/Time | Ownership thực giữa người dùng, categories/subtasks/dependencies/cycles, giờ rảnh, timezone/DST và sự kiện |
| Planning/Execution | Deadline, capacity/bias, preserved sessions, proposal revision/single-use, conflict scan, review delay, timer và giới hạn một active session |
| Insights/Notifications/Admin | Workload/risk/scenario không persist, notification retry/dedupe, admin lock/unlock và revoke sessions |
| Persistence | SQL Server migration/model snapshot, unique/FK constraints, transaction và 7 ca concurrency bằng database test riêng |
| Deployment local | Windows PowerShell 5.1/7, private .NET SDK 10, Docker health, startup pipeline và SMTP thật |

## Bằng chứng kiểm tra mới nhất

- Locked restore, Release build: 0 warning, 0 error.
- 58 unit/service/planner + 38 integration = **96 passed, 0 skipped**.
- Báo cáo TRX: `TestResults/reiko_DWAN_2026-10-04_22_16_40_net10.0.trx` và `TestResults/reiko_DWAN_2026-10-04_22_16_45_net10.0.trx`.
- 10 kiểm tra script độc lập pass trên Windows PowerShell 5.1 và PowerShell 7; không gửi email hoặc chạm User Secrets thật trong fixture.
- EF không có pending model changes; vulnerability audit không phát hiện known-vulnerable dependency trong nguồn audit.
- SQL Server healthy, database SPWPOS giữ 24 bảng và 3 migrations.
- Gmail TLS/authentication thành công bằng Python SMTP check, sau đó kiểm tra lại thành công bằng **MailKit thật của API** qua `--check-smtp`.
- Pipeline `Start-Development.ps1 -SkipCertificateTrust` đã chạy lại bằng Windows PowerShell 5.1: Docker healthy, restore/build/migration thành công, cấu hình SMTP external được giữ.
- Endpoint `resend-verification-otp` thực đã trả success cho tài khoản Gmail chưa xác minh; OTP mới được commit vào database sau khi SMTP gửi thành công. Không đọc/in OTP hoặc OTP hash để kiểm tra.

SMTP chấp nhận thư và OTP đã commit là bằng chứng phía server; chưa kiểm tra trực tiếp Gmail Inbox/Spam của người dùng. Xác minh email và đăng nhập bằng mã vẫn cần người dùng thực hiện. Đây là rà soát code và regression tại môi trường local, không phải chứng nhận pentest hoặc kiểm thử tải production. AI provider thật chưa được gọi và deployment/CI bên ngoài máy chưa được kiểm tra.

## Chạy lại

```powershell
cd D:\SWP
.\scripts\Start-Development.ps1 -SkipCertificateTrust
```

Không cần cấu hình lại Gmail hoặc tạo App Password mới cho bản sửa này. UI: https://localhost:7043/; lấy email OTP mới nhất trong Gmail và nhập trong 5 phút. Khi hết hạn, bấm Gửi lại mã sau cooldown.

## Đối chiếu UML v2 sau bản sửa startup
Bộ UML mới đã được lưu tại docs/reference/UML-v2, không chạy SQL portable hoặc Tools trực tiếp. Migration AlignWithUmlV2 bổ sung 16 bảng, giữ dữ liệu hiện có, sau SQL backup COPY_ONLY/CHECKSUM. Database có 39 bảng ứng dụng và 4 migrations; API chạy lại thành công. Xem UML_ALIGNMENT.md và VERIFICATION.md cho mapping toàn bộ ERD, R1–R9, 106 test và giới hạn kiểm tra trực quan UI phiên bản mới.

