# Security design và checklist

## Credential storage

- Password: Argon2id/libsodium, 32 MiB / 3 iterations / p=1, salt RNG 16 byte riêng, PHC lưu cost/version. Không encryption reversible, MD5/SHA1/SHA256 password; không trim/truncate. Mật khẩu mới có 8–32 UTF-16 code units, gồm số ASCII, chữ thường, chữ hoa và ký tự đặc biệt không phải khoảng trắng. Chặn username/email và phần trước @, `password`, `websitename`, `PLANORA` không phân biệt hoa thường; giữ blocklist. Reset và change đều kiểm tra hash để chặn dùng lại mật khẩu hiện tại; không lưu lịch sử mật khẩu. Login/rehash vẫn hỗ trợ credential cũ tới 128 ký tự.
- OTP: secure numeric RNG, HMAC-SHA256 bằng secret ngoài DB, bind issuance id/user/purpose. 5 phút, tối đa 5 attempts, single-use, invalidate khi resend. Constant-time compare hash. OTP không tồn tại trong API response, audit, console hoặc database plaintext.
- Refresh/reset credential: 64 random bytes, URL-safe, chỉ lưu SHA256. SHA256 được dùng ở đây vì token có entropy cao; không dùng cho password. Reset ticket bound UserId + ForgotPassword và TTL 10 phút.
- JWT key và OTP key riêng, Base64 ≥32 random bytes; không commit key/password SMTP/SQL. User Secrets chỉ development. Production dùng environment/secret manager có ACL.

## Session và state

- EmailConfirmed=false chặn login. 403 EMAIL_NOT_VERIFIED chỉ sau password đúng; incorrect/unknown đều INVALID_CREDENTIALS.
- Lockout 5 failures / 15 phút. Rate limit IP và account bền vững bổ sung để tránh brute force/spam. Wrong current password cũng tăng failed count.
- JWT ký HS256, issuer/audience/lifetime/signature/algorithm validation, access 15 phút, skew 10 giây. User active/verified + session family kiểm tra DB mỗi protected request. Không serialize SecurityStamp vào JWT.
- Refresh lifetime tối đa 7 ngày tính từ login; rotation không kéo dài deadline family. Mỗi lần refresh revoke token cũ. Reuse bất kỳ revoked token nào trong family revoke cả family, gồm access token; client phải serialize refresh requests, không retry token cũ khi response mất.
- Logout revoke current family bằng Bearer sid; logout-all revoke mọi device và update stamp. Reset/change password revoke toàn bộ session, invalidate mọi OTP/ticket và update SecurityStamp atomically.
- Change password bắt buộc **current password + ChangePassword OTP + password mới khác password cũ**. Có email notification; durable outbox retry nếu SMTP tạm lỗi.
- Forgot/reset password hoạt động cho account active, gồm account chưa verify email. Reset không thay đổi EmailConfirmed; tài khoản chưa verify vẫn không được login và cần request OTP EmailVerification mới sau reset.
- Security-sensitive operations dùng một distributed SQL application lock theo normalized email + DB transaction; counter của invalid proof vẫn commit. Unique email/hash/active-OTP constraints bảo vệ thêm. Mọi service gọi OTP phải giữ transaction đến SaveChanges/Commit.

## Public/API controls

- HTTPS-only API; HSTS ngoài Development. Trusted proxy IP phải cấu hình tường minh trước khi tin X-Forwarded-For/Proto. Không chấp nhận tùy ý header từ client.
- CORS explicit origins/methods/headers, không AllowAnyOrigin+credentials. AllowedHosts phải khớp deploy. Swagger chỉ development.
- 16 KiB max body; Kestrel còn chặn chunked body vượt limit; DataAnnotations cho DTO và Identity password validator. EF parameterized SQL; sp_getapplock resource parameterized.
- Error ProblemDetails chuẩn + success/error/traceId, không echo field values hoặc stack trace. Security log metadata chỉ internal code, agent truncate/strip control characters; không password/hash/OTP/access/refresh/reset token. Không bật EnableSensitiveDataLogging hoặc SMTP protocol logging.
- Register/resend/forgot response chung; forgot/resend cooldown/quota cũng response chung. Middleware IP vẫn có thể 429 độc lập với account. SMTP synchronous có khác biệt timing; proxy/bulk traffic protection cần được đánh giá khi triển khai internet-facing.
- Rate middleware IP là per-instance, account throttle và OTP quota là shared DB. Multi-instance cần gateway/shared IP limiter nếu muốn tổng hạn mức IP thống nhất. Concurrency limiter chặn RAM/CPU exhaustion do Argon2.

## Operations

- Production SQL dùng service principal/login quyền tối thiểu, Encrypt=True/certificate validated. Migration bằng job có quyền schema riêng. Developer edition/sa/TrustServerCertificate chỉ local development.
- Audit và notification giữ user reference hợp lý. Đặt retention cho used OTP, used tickets, expired sessions, audit và hashed anonymous-email throttle rows theo volume/privacy policy. Giữ revoked refresh rows đến hết expiry family để tiếp tục phát hiện reuse.
- Không đổi OTP key khi còn code active trừ emergency; key rotation sẽ invalidate OTP active. Rotate JWT signing key cũng invalidate access JWT cũ. Đổi password cost giữ hasher đọc được cost cũ trong giới hạn. Test restore/backup và secret recovery.
- Outbox không chứa OTP/password/token. Notification delivery at-least-once; SMTP accept trước crash có thể gửi trùng. Theo dõi pending queue, retry attempts và logger failure type; alert provider outages. Đừng log content email.
- Các API Account/Administration tương lai đổi email, disable user hoặc sửa password/session phải giữ cùng khóa account/transaction. Nếu cho đổi email, cần thiết kế lock cũ + mới theo thứ tự cố định và thu hồi session. Không chỉnh trực tiếp password bằng generic EF CRUD.

## Trước merge/deploy

- [ ] `dotnet restore --locked-mode` và Release build không warnings/errors.
- [ ] Unit và HTTP integration tests đều pass.
- [ ] SQL Server concurrency suite pass, **không skip**: verify OTP, request OTP, refresh reuse, reset single-use, unique register.
- [ ] Migrations applied lên SQL test mới; snapshot không pending; review schema/constraints và idempotent script.
- [ ] `dotnet list package --vulnerable --include-transitive`; không dependency known-vulnerable, review update/downgrade transitive.
- [ ] Smoke test SMTP real/TLS/certificate/sender/domain; đúng expiry text/purpose; provider không lưu secret vào app log.
- [ ] Thử đủ Register → Verify → Login → Me; Change → mọi session cũ invalid; Forgot → OTP → ticket → Reset → Login mới.
- [ ] JWT invalid issuer/audience/signature/expiry, inactive account và revoked family bị chặn.
- [ ] Failed OTP 5 lần persisted; request parallel không double-consume; refresh parallel winner cũng bị revoke khi token cũ reused.
- [ ] Test rate limit theo IP/account, CORS allowlist, HTTPS/HSTS, body size và error schema.
- [ ] Scan Git/artifacts/logs: không secret production, password/hash/OTP/token từ test thật; không fake email service trong API registration.
- [ ] Secrets riêng cho từng environment; proxy allowlist/AllowedHosts đúng deploy; production Swagger tắt.
- [ ] Benchmark Argon2 concurrent; DB indexes/query latency/session checks và notification worker đáp ứng tải.
- [ ] Frontend/BFF token storage và CSRF/XSS policy được kiểm chứng với kiến trúc thực tế.
