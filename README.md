# SPWPOS — Personal Workload Planning System

Hệ thống web cho **Smart Personal Workload Planning and Optimization System**, mở rộng từ Authentication sang Task, Time, Planning, Execution, Intelligence, Notification và Administration. Backend .NET 10 / SQL Server, frontend tiếng Việt chạy cùng API; tất cả API nhận/trả DTO.

Mở **https://localhost:7043/** để sử dụng giao diện. Swagger ở `/swagger`. Chạy toàn bộ bằng `scripts/Start-Development.ps1`.

- [Hướng dẫn UI, chạy hệ thống, demo, AI và Admin](docs/SYSTEM_GUIDE.md)
- [Đối chiếu 72 use case](docs/USE_CASE_COVERAGE.md)
- [Kết quả kiểm thử](docs/VERIFICATION.md)
- [Bản sửa sau đánh giá ngày 05/10/2026](docs/FIX_2026-10-05.md): 113 tests pass; 8 SQL Server cases chưa chạy lại. Bộ kiểm tra trước merge bổ sung Node.js 20.18+ cho frontend regression tests.

Frontend ES modules/CSS nằm trong `src/SPWPOS.Api/wwwroot/assets`; không cần npm/build frontend. Các service workload nằm trong `SPWPOS.Infrastructure/Workspace`, hợp đồng/planner trong `SPWPOS.Application/Workspace`; entity trong `SPWPOS.Domain/Entities/WorkspaceEntities.cs`. Migration `AddWorkloadSystem` bổ sung 10 bảng. Phần dưới mô tả chi tiết nền tảng Authentication và cấu hình vận hành.

## Cấu trúc và trách nhiệm

```text
SPWPOS.slnx
global.json                       # .NET 10; cho phép SDK feature band mới hơn
Directory.Build.props             # nullable, warnings-as-errors, NuGet lock files
dotnet-tools.json                 # dotnet-ef 10.0.12
appsettings.example.json          # không chứa secret
compose.yaml                      # SQL Server Developer + Mailpit cho development
src/
  SPWPOS.Domain/
    Entities/                     # ApplicationUser, EmailOtp, RefreshToken,
                                  # PasswordResetTicket, SecurityAuditLog,
                                  # SecurityRateLimit, PasswordChangeNotification
    Enums/                        # OtpPurpose, SecurityEventType
  SPWPOS.Application/
    Authentication/
      DTOs/                       # request validation + response whitelist
      Interfaces/                 # IAuthService, IOtpService, IJwtTokenService,
                                  # IEmailService, ISecurityAuditService
      AuthException.cs
      Settings.cs
  SPWPOS.Infrastructure/
    Authentication/               # AuthService, Argon2id, OTP, JWT, blocklist
    Security/                     # audit và rate limit theo account
    Email/                        # SMTP/TLS, durable password notification worker
    Persistence/
      ApplicationDbContext.cs
      AccountTransaction.cs       # transaction + SQL Server sp_getapplock
      Configurations/             # FK, unique constraints, indexes
      Migrations/                 # InitialIdentityMigration + notification outbox
    DependencyInjection.cs
  SPWPOS.Api/
    Controllers/AuthController.cs
    Middleware/                   # GlobalExceptionHandler, ApiProblems
    Program.cs                    # JWT, HTTPS, CORS, rate limit, Swagger
    appsettings.json
    Properties/launchSettings.json
tests/
  SPWPOS.UnitTests/               # password/OTP/service/notification tests
  SPWPOS.IntegrationTests/        # HTTP flows + SQL Server concurrency tests
  SPWPOS.TestSupport/             # isolated fake SMTP, clock, relational test DB
scripts/
  Initialize-Dotnet.ps1          # chọn SDK 10 và đặt PATH trước mọi SDK tool
  Initialize-Development.ps1
  Start-Development.ps1          # setup → containers → migration → API; dừng khi lỗi
  Test-BeforeMerge.ps1
docs/
  requests.http                  # đủ 13 endpoint, dùng REST Client/Postman
  schema.sql                     # SQL Server migration script idempotent
  SECURITY.md                    # threat controls và checklist trước merge
  VERIFICATION.md                # kết quả thực thi và giới hạn kiểm chứng
```

Dependency: Api → Infrastructure → Application → Domain. Domain dùng kiểu Identity theo yêu cầu. Application định nghĩa contract/DTO; Infrastructure thực thi service cần Identity/EF/crypto/email. Không thêm generic repository vì `DbContext` đã cung cấp unit of work. Module Task/Planning/Notification/Admin có thể lấy user id từ claim `sub`, sử dụng `IAuthService`, và FK đến `ApplicationUser.Id`; không truy cập password/OTP/token hash qua API.

## NuGet được pin

| Package | Version | Mục đích |
|---|---|---|
| Microsoft.AspNetCore.Identity.EntityFrameworkCore | 10.0.12 | Identity stores |
| Microsoft.EntityFrameworkCore.SqlServer | 10.0.12 | SQL Server |
| Microsoft.EntityFrameworkCore.Design | 10.0.12 | Migration/design time |
| Microsoft.AspNetCore.Authentication.JwtBearer | 10.0.12 | JWT signature/issuer/audience/lifetime validation |
| Sodium.Core | 1.4.1 | Argon2id qua libsodium 1.0.22 (transitive) |
| MailKit | 4.17.0 | SMTP bắt buộc TLS trong production |
| Swashbuckle.AspNetCore | 10.2.3 | Swagger UI + JWT Bearer |
| Microsoft.EntityFrameworkCore.Sqlite | 10.0.12 | Database relational chỉ cho test |
| Microsoft.AspNetCore.Mvc.Testing | 10.0.12 | HTTP integration tests |
| Microsoft.NET.Test.Sdk | 18.10.1 | Test runner |
| xunit.v3 | 3.2.2 | Test framework |
| xunit.runner.visualstudio | 3.1.4 | VSTest adapter |

`packages.lock.json` được commit tại mỗi project để khóa dependencies transitive. Restore bằng `dotnet restore --locked-mode`.

Chọn [Sodium.Core 1.4.1](https://www.nuget.org/packages/Sodium.Core/1.4.1) vì có bản cập nhật năm 2026, hỗ trợ runtime .NET qua .NET Standard/net9, và gọi thư viện [libsodium Argon2id](https://doc.libsodium.org/password_hashing), thay vì tự triển khai thuật toán. Hasher tùy chỉnh chỉ làm adapter Identity, sinh salt và mã hóa/đọc PHC. Đã test tương thích bằng build net10.0 và cross-check PHC với verifier của libsodium. Native runtime package cần được giữ trong output publish; hỗ trợ môi trường Windows x64/Linux phù hợp với libsodium package, không dành cho browser/WASM.

Password dùng **32 MiB, 3 iterations, p=1**, salt 16 byte từ `RandomNumberGenerator`, hash 32 byte. Format `$argon2id$v=19$m=32768,t=3,p=1$salt$hash`. Không dùng pepper mặc định để tránh phụ thuộc key rotation ngoài yêu cầu; OTP có secret HMAC riêng. Verification giới hạn tham số đọc từ PHC và dùng constant-time comparison; nâng cấu hình sẽ trả `SuccessRehashNeeded` để rehash ở login. Benchmark CPU/RAM và tải concurrent trước khi tăng cost. PBKDF2 fallback không được sử dụng.

Password tối thiểu 10, tối đa 128 ký tự UTF-16 theo `string.Length`, không trim/truncate, hỗ trợ Unicode/password manager. Có blocklist nhỏ gồm `Password123!` và các mẫu phổ biến; đây không phải corpus đầy đủ về breached passwords. Có thể mở rộng blocklist trong validator. `PasswordSettings:MinLength` có thể tăng; `MaxLength=128` được kiểm tra cùng DTO để chính sách thống nhất.

## Chạy development trên Windows

Cần .NET 10 SDK, Windows PowerShell 5.1 hoặc PowerShell 7, Docker Desktop với Linux containers. Nếu SDK được cài cho task này, đường dẫn là `%LOCALAPPDATA%\SPWPOS\dotnet\dotnet.exe`; script tự nhận đường dẫn đó và thêm vào PATH trước khi gọi SDK tools. Máy chỉ có runtime .NET 6 phải bổ sung SDK trước. Script dùng RNG instance tương thích cả PowerShell 5.1 và 7, lưu User Secrets bằng JSON stdin để giữ đúng giá trị rỗng và không đưa secret vào process arguments.

Mở Docker Desktop, chờ engine **Running**, sau đó chạy trong terminal:

```powershell
cd D:\SWP
.\scripts\Start-Development.ps1
```

Script tự chuẩn bị secrets, khởi động SQL Server/Mailpit, restore tools/packages, trust HTTPS development certificate (có thể hỏi xác nhận của Windows), apply migration và chạy API. Khi lỗi, script dừng ngay; không tiếp tục các bước sau. Docker engine được kiểm tra trước khi thay đổi secrets, với timeout 20 giây để tránh treo khi chỉ ứng dụng Desktop mở nhưng engine chưa chạy. `-SkipCertificateTrust` bỏ qua bước trust nếu đã cấu hình certificate. Script setup cũng đặt `ConnectionStrings__DefaultConnection` cho EF CLI trong cùng process. Nếu cần thực hiện từng bước:

```powershell
cd D:\SWP
.\scripts\Initialize-Development.ps1 -StartContainers
# SDK, JWT key, OTP key và SQL password được chuẩn bị bằng secure RNG.
# Secret lưu trong User Secrets; giữ SMTP đã cấu hình, mặc định Mailpit khi chưa có.
dotnet tool restore
dotnet restore --locked-mode
dotnet dev-certs https --trust

# Setup đã đặt environment connection cho design-time factory trong cùng terminal.
dotnet ef database update --project src/SPWPOS.Infrastructure
dotnet run --project src/SPWPOS.Api --launch-profile https
```

API: `https://localhost:7043`; Swagger: `https://localhost:7043/swagger`; Mailpit inbox: `http://localhost:8025`. Script không in secret. Restart thông thường giữ khóa JWT/OTP, password SQL và SMTP đã cấu hình. Chỉ truyền `-RotateSecurityKeys` khi chủ động thay khóa development; thao tác đó làm JWT/OTP cũ không còn hợp lệ. SQL password được giữ từ connection string trong User Secrets để khớp Docker volume hiện có.

Nếu terminal mới vẫn trỏ `dotnet` vào runtime .NET 6 ở `C:\Program Files\dotnet`, chạy `. .\scripts\Initialize-Dotnet.ps1` để chọn SDK 10 trong process hiện tại. Không cần sửa `global.json` xuống .NET 6. Không chạy tiếp migration/API khi setup hoặc container startup chưa thành công.

SQL development dùng `tcp:127.0.0.1,1433`, khớp port IPv4 được Docker bind. Volume mặc định là `spwpos-sql-data`; có thể đổi bằng `SPWPOS_SQL_VOLUME_NAME` trước khi chạy script. Volume cũ `swp_spwpos-sql-data` được giữ nguyên khi chuyển cấu hình, không bị xóa. Nếu restore báo `Access denied` trong `obj`, kiểm tra tài khoản hiện tại có quyền Modify trong thư mục dự án.

Nếu Docker chưa sẵn sàng, mở Docker Desktop rồi chạy setup với `-StartContainers`; script tạo external SQL volume khi chưa có, giữ nguyên khi đã tồn tại. SQL image được pin CU27, Mailpit pin 1.31.3 và chỉ bind loopback. Mailpit hiển thị OTP trong **email inbox** dành cho test, không phải API/console log. Production dùng SQL account riêng có quyền tối thiểu và SMTP thật; không dùng `sa`, SQL Developer edition hoặc Mailpit cho production.

Muốn tự dùng SQL Server có sẵn: đặt connection bằng User Secrets (API runtime) và `ConnectionStrings__DefaultConnection` (EF CLI), ví dụ Integrated Security hoặc SQL credentials từ secret store. Không chạy migration tự động khi API khởi động; deployment dùng migration job riêng.

## Configuration và environment

`appsettings.example.json` minh họa toàn bộ shape; secret để trống. ASP.NET Core nạp appsettings, User Secrets khi Development, và environment variables. Không cần chép secret vào appsettings.

| Variable | Giá trị/quy tắc |
|---|---|
| `ConnectionStrings__DefaultConnection` | SQL connection string, production Encrypt=True và certificate hợp lệ |
| `JwtSettings__SigningKey` | Base64 của ≥32 random bytes, khác OTP key |
| `JwtSettings__Issuer` / `JwtSettings__Audience` | Issuer/audience frontend đúng deployment |
| `JwtSettings__AccessTokenMinutes` | 15 mặc định, chỉ cho phép 1–15 |
| `JwtSettings__RefreshTokenDays` | 7 mặc định, chỉ cho phép 1–7 |
| `OtpSettings__SecretKey` | Base64 của ≥32 random bytes, ngoài DB |
| `OtpSettings__Length` | 6 mặc định; cho phép 6–8 |
| `OtpSettings__ExpirationMinutes` / `OtpSettings__MaxAttempts` | 5 / 5, không cho tăng quá 5 |
| `OtpSettings__ResendCooldownSeconds` | 60 hoặc dài hơn |
| `PasswordSettings__MemoryKiB` / `PasswordSettings__Iterations` | 32768 / 3, không dưới 19456 / 3 |
| `EmailSettings__Host` / `EmailSettings__Port` | SMTP server; 587 STARTTLS hoặc 465 TLS |
| `EmailSettings__Username` / `EmailSettings__Password` | Credential SMTP qua User Secrets/secret manager |
| `EmailSettings__SenderEmail` / `EmailSettings__SenderName` | Địa chỉ/domain được provider xác nhận |
| `EmailSettings__EnableSsl` | `true` bắt buộc production |
| `Cors__AllowedOrigins__0` | Origin cụ thể, ví dụ https://app.example.com |
| `AllowedHosts` | Hostname deployment, ví dụ api.example.com |
| `Security__TrustedProxyAddresses__0` | Tùy chọn: địa chỉ IP proxy tin cậy duy nhất |
| `ASPNETCORE_ENVIRONMENT` | Development hoặc Production |
| `ASPNETCORE_URLS` | Endpoint HTTPS theo certificate deployment |
| `SPWPOS_SQL_PASSWORD` | Chỉ Docker development, script sinh ngẫu nhiên |
| `SPWPOS_TEST_SQLSERVER` | Connection đến SQL Server test có quyền tạo/xóa **test DB** |

Sinh key bằng `[Convert]::ToBase64String([Security.Cryptography.RandomNumberGenerator]::GetBytes(32))` trong script và lưu trực tiếp vào secret store; không gửi key cho người khác. Hệ thống validate options khi khởi động và từ chối secret thiếu/yếu về độ dài. Độ entropy của secret là trách nhiệm provisioning.

### SMTP thật

Gmail: chạy `scripts/Configure-Gmail.ps1`, nhập Gmail gửi và App Password trong terminal, rồi restart API. Xem [hướng dẫn Gmail SMTP](docs/GMAIL_SMTP.md). Script khởi động giữ cấu hình SMTP đã lưu; chỉ chuyển về Mailpit khi yêu cầu `-UseMailpit`.

```powershell
dotnet user-secrets set "EmailSettings:Host" "smtp.your-provider.example" --project src/SPWPOS.Api
dotnet user-secrets set "EmailSettings:Port" "587" --project src/SPWPOS.Api
dotnet user-secrets set "EmailSettings:EnableSsl" "true" --project src/SPWPOS.Api
dotnet user-secrets set "EmailSettings:SenderEmail" "no-reply@your-domain.example" --project src/SPWPOS.Api
# Đặt Username/Password bằng secret manager hoặc User Secrets trên máy của bạn.
```

Port 587 sử dụng **STARTTLS bắt buộc**, 465 dùng TLS từ lúc connect. Không tắt certificate validation. Timeout 15 giây. Không cấu hình SMTP protocol logger. OTP email có HTML/plain text, subject theo purpose, code, thời hạn và cảnh báo không chia sẻ. Email password-change không chứa password; outbox được commit cùng đổi password và retry bằng worker mỗi 30 giây, backoff tối đa 1 giờ. Delivery là at-least-once: crash sau SMTP accept trước DB commit có thể gửi lặp notification. Không lưu nội dung OTP email trong outbox.

## Endpoint và response

| Method | Path | Authentication |
|---|---|---|
| POST | `/api/auth/register` | Public |
| POST | `/api/auth/verify-email` | Public |
| POST | `/api/auth/resend-verification-otp` | Public |
| POST | `/api/auth/login` | Public |
| POST | `/api/auth/refresh-token` | Random refresh token trong body |
| POST | `/api/auth/logout` | Bearer JWT; thu hồi family/session hiện tại |
| POST | `/api/auth/logout-all` | Bearer JWT; thu hồi mọi device |
| POST | `/api/auth/forgot-password` | Public, response chung |
| POST | `/api/auth/verify-forgot-password-otp` | Public |
| POST | `/api/auth/reset-password` | Reset ticket trong body |
| POST | `/api/auth/change-password/request-otp` | Bearer JWT |
| POST | `/api/auth/change-password` | Bearer JWT + current password + OTP |
| GET | `/api/auth/me` | Bearer JWT |

Success: `{"success":true,"data":{...}}`. Error: RFC ProblemDetails với `status`, `title`, `type`, `instance`, `traceId` và extension `success:false`, `error:{code,message}`. Content type lỗi `application/problem+json`. Không echo input, token hoặc stack trace. DTO invalid trả `VALIDATION_ERROR`; password policy trả `PASSWORD_POLICY_FAILED`; account lockout trả 423 `ACCOUNT_LOCKED`.

Register/resend/forgot trả 200 cùng thông báo chung, kể cả email không tồn tại hoặc đã đăng ký. Resend/forgot trong cooldown/quota cũng trả chung để hạn chế enumeration. OTP mới **chỉ được gửi khi** chưa vượt cooldown/quota. Rate limit IP vẫn trả 429. Request change-password OTP có authentication nên cooldown trả 429 `OTP_RESEND_COOLDOWN`.

Login dùng cùng lỗi `INVALID_CREDENTIALS` cho email sai/password sai; unknown email vẫn chạy một Argon2id check với dummy hash. `EMAIL_NOT_VERIFIED` 403 chỉ trả **sau khi password đúng**, hạn chế email enumeration. Không auto-login sau register/verify/reset/change.

Access JWT có `sub`, `email`, `role`, `jti`, `iat`, `exp`, `sid`. `sid` là id family, không phải credential; không đưa SecurityStamp vào JWT. Kiểm tra HS256, issuer, audience, signature, lifetime, clock skew 10 giây. Mỗi protected request kiểm tra user active/verified và family còn refresh token active trong DB, nên logout/reset/change/reuse thu hồi được access JWT cũ ngay khi transaction commit. Đổi lại, protected API cần DB query; DB unavailable không cho truy cập.

## Test thủ công các luồng

Dùng Swagger Authorize hoặc `docs/requests.http` (Postman có thể import raw HTTP/curl tương đương). Biến OTP/token là placeholder; copy từ inbox và response, không commit giá trị thật.

1. **Register → OTP → Login:** gọi register với password dài (ví dụ trong HTTP file); email chưa xác nhận login trả 403 khi password đúng. Đọc Mailpit/SMTP inbox, gửi verify-email. Login trả access/refresh; Authorize access token, GET me trả profile. Verify cùng OTP lần hai trả lỗi. `Password123!` bị blocklist từ chối.
2. **Change Password:** login, gọi change-password/request-otp với Bearer. Gửi currentPassword + code purpose ChangePassword + newPassword + confirm. Thành công yêu cầu login lại: me bằng access cũ, refresh cũ và login password cũ đều thất bại; password mới login được. Không có endpoint chỉ đổi bằng current password.
3. **Forgot Password:** gọi forgot-password, lấy OTP, gọi verify-forgot-password-otp để nhận resetToken (10 phút). Reset-password bằng ticket + password mới; mọi session cũ bị revoke, ticket và OTP không reuse; login password mới. Email không tồn tại trả cùng response của forgot-password. Account active nhưng chưa verify cũng có thể reset password; reset không xác nhận email nên vẫn phải verify email trước login. Account inactive không nhận OTP reset.
4. **Refresh:** mỗi refresh thay token cũ bằng token mới, phải cập nhật cả hai biến. Thử dùng lại token cũ: toàn bộ family bị revoke; token mới vừa phát hành cũng không còn dùng được. Session trên device khác vẫn hoạt động. Không retry song song cùng refresh token.

## Database và migration

Schema đầy đủ nằm trong `docs/schema.sql` (idempotent). Các bảng Identity: `AspNetUsers`, `AspNetRoles`, `AspNetRoleClaims`, `AspNetUserRoles`, `AspNetUserClaims`, `AspNetUserLogins`, `AspNetUserTokens`.

| Bảng bổ sung | State / indexes |
|---|---|
| EmailOtps | UserId, Purpose, HMAC hash, expiry, attempts, used state; unique filtered (UserId,Purpose) WHERE IsUsed=0; index expiry và UserId/Purpose/CreatedAt |
| RefreshTokens | UserId, FamilyId, SHA256 hash token 64 random bytes, expiry, revoked/replaced state, device/IP; unique TokenHash; family/user/expiry indexes; replacement FK NoAction |
| PasswordResetTickets | UserId, ForgotPassword purpose, random token hash, 10 phút, UsedAt; unique TokenHash; user/expiry index |
| SecurityAuditLogs | nullable UserId, event, IP/agent, UTC time, result, allowlisted code; user/time index; user deletion SetNull |
| SecurityRateLimits | hash của normalized-email/operation, count/window/last-request; persistent per-account throttling, gồm email chưa tồn tại |
| PasswordChangeNotifications | UserId, CreatedAt, NextAttemptAt, Attempts, SentAt; durable notification retry, không chứa OTP/token/password |

NormalizedEmail unique trong DB, username index Identity unique. Account/security child FK cascade; audit giữ event khi user bị xóa bằng SetNull. Guid khóa chính cho account/OTP/token/ticket/audit/notification; timestamp UTC `datetimeoffset`. Không expose Identity entity.

```powershell
# Migration initial đã có sẵn; không generate lại cho lần chạy đầu.
dotnet tool restore
dotnet ef database update --project src/SPWPOS.Infrastructure

# Khi thay đổi schema:
dotnet ef migrations add YourMigrationName --project src/SPWPOS.Infrastructure --output-dir Persistence/Migrations
dotnet ef migrations has-pending-model-changes --project src/SPWPOS.Infrastructure
dotnet ef migrations script --idempotent --project src/SPWPOS.Infrastructure -o docs/schema.sql
```

EF CLI dùng design-time factory để không cần JWT/SMTP secrets khi generate schema. Update cần connection env/`--connection`. Không ghi connection có password vào command history khi có thể dùng env/secret injection.

Concurrency: tất cả mutation theo cùng khóa normalized-email bằng `sp_getapplock`, `Exclusive`, transaction owner, 15 giây timeout; hoạt động giữa nhiều API instance dùng cùng DB. Refresh/reset lookup owner trước rồi **đọc lại state trong lock**, không sử dụng snapshot cũ. Transaction chứa password/hash/stamp, OTP/ticket consumption, refresh revocation và audit. Failed OTP/login phải commit counter; infrastructure failures rollback. Không bật execution retry mù quáng quanh operation có gửi email. SMTP OTP gửi trong transaction; provider nhận thư rồi DB commit thất bại có thể tạo code không dùng được, người dùng request lại sau cooldown.

## Test tự động và điều kiện merge

```powershell
dotnet test
# Unit/service + HTTP end-to-end dùng relational SQLite; SQL race tests được skip
# khi SPWPOS_TEST_SQLSERVER chưa được cấu hình.

# Test bằng SQL Server development vừa tạo, trong cùng shell setup:
$env:SPWPOS_TEST_SQLSERVER = "Server=tcp:127.0.0.1,1433;Database=master;User Id=sa;Password=$env:SPWPOS_SQL_PASSWORD;Encrypt=True;TrustServerCertificate=True"
.\scripts\Test-BeforeMerge.ps1 -RequireSqlServer
```

Test SQL Server tạo database ngẫu nhiên `SPWPOS_Tests_<guid>`, apply migration thật, test request đồng thời và chỉ xóa database do factory đó tạo. Không trỏ vào production server. SQLite test có converter DateTimeOffset riêng và không mô phỏng `sp_getapplock`; chỉ SQL suite xác minh concurrency production.

Trước merge chạy: locked restore; Release build warnings-as-errors; password/hash/Unicode/rehash; OTP expiry/wrong/reuse/purpose/cooldown/quota; login verified/failed/lockout; change current+OTP/same-password/session revocation; forgot/reset ticket expiry/reuse; refresh rotation/expiry/reuse/family isolation; notification retry; DTO/error/HTTPS/size/IP/account limit/audit; SQL concurrent verification, issuance, refresh, reset, register; EF snapshot; dependency vulnerability audit. Checklist chi tiết trong `docs/SECURITY.md`.

Swagger chỉ bật Development. API Bearer trả token trong body cho client phù hợp. Frontend cùng origin dùng `/api/browser-session` với cookie HttpOnly+Secure+SameSite và kiểm tra header/origin cho mutation; không đặt refresh token trong localStorage. Các tab dùng Web Locks để phối hợp refresh và kiểm tra lại phiên sau khi lấy khóa. Nếu trình duyệt không hỗ trợ Web Locks, phiên hết hạn yêu cầu đăng nhập lại. Endpoint đọc workspace và phân tích không ghi lịch sử; worker quét thông báo mỗi phút, nên thông báo định kỳ có thể chậm tới lượt quét tiếp theo. Chỉ completed job diagnostics và superseded risk snapshots cũ hơn 30 ngày được dọn theo lô; dữ liệu công việc và security audit được giữ.

## Tình trạng xác minh tại workspace

Lần rà soát mới nhất: Release build net10.0 với 0 warnings/errors; **58 unit/service/planner + 38 integration = 96 tests pass, không skip**. SQL Server thật có 7 concurrency cases; 10 kiểm tra script pass trên PowerShell 5.1/7. Gmail SMTP đã qua kiểm tra TLS/authentication bằng MailKit và endpoint resend OTP đã gửi thư, commit OTP mới. Chi tiết và giới hạn xác minh trong [báo cáo rà soát](docs/SYSTEM_REVIEW.md) và [kết quả kiểm thử](docs/VERIFICATION.md).

## Bộ tài liệu UML v2
Hệ thống đã đối chiếu báo cáo 72 use case và bộ UML do người dùng cung cấp. Xem [UML_ALIGNMENT](docs/UML_ALIGNMENT.md) cho mapping toàn bộ bảng và quy tắc R1–R9; bản tham chiếu nằm tại docs/reference/UML-v2. Migration AlignWithUmlV2 bổ sung persistence và backfill schema cũ. Các tùy chọn/histories mới có trong API và UI. Xem [VERIFICATION](docs/VERIFICATION.md) cho kết quả 106 test và phạm vi kiểm chứng thực tế.

