# Kết quả xác minh — 04/10/2026 (hệ thống đầy đủ)

Cập nhật **05/10/2026** sau bản sửa từ đánh giá hệ thống: Release build 0 warnings/errors; **68 unit + 39 HTTP integration + 6 frontend module tests = 113 passed**, 0 failed. **8 ca SQL Server bị skip** vì chưa cấu hình `SPWPOS_TEST_SQLSERVER`; kết quả 106 tests với SQL Server phía dưới là lịch sử, không phải toàn bộ đã chạy lại lần này. Tất cả JavaScript modules qua syntax check. Xem [chi tiết bản sửa và phạm vi kiểm chứng](FIX_2026-10-05.md).

Backend và UI đã đối chiếu báo cáo 72 use case và bộ UML v2. Migration `AlignWithUmlV2` đã áp dụng thành công trên SQL Server local; giữ dữ liệu Identity/workspace cũ. Xem [ánh xạ chi tiết](UML_ALIGNMENT.md).

| Kiểm tra mới nhất | Kết quả |
|---|---|
| Release build | Pass, 0 warnings / 0 errors |
| Unit/service/planner | **60 passed**, báo cáo `TestResults/reiko_DWAN_2026-10-04_22_47_35_net10.0.trx` |
| HTTP + SQL Server integration | **46 passed, 0 skipped**, báo cáo `TestResults/reiko_DWAN_2026-10-04_22_47_38_net10.0.trx` |
| Tổng test | **106 passed** |
| SQL Server thực | 7 concurrency + 1 migration với dữ liệu schema cũ; database test riêng |
| EF model snapshot | Không có pending model changes |
| Idempotent migration script | `docs/schema.sql`, có `AlignWithUmlV2` và backfill dữ liệu |
| NuGet locked restore và vulnerability audit | Pass; không phát hiện dependency có known vulnerability trong nguồn audit |
| JavaScript modules | Tất cả file `wwwroot/assets/*.js` qua Node syntax check |
| UI HTTPS sau migration | HTTP 200; login QA → workspace/profile/settings → intelligence thành công; Swagger 60 paths |
| Kiểm tra trực quan UI v2 | Chưa thực hiện: browser tool chặn truy cập tab đang ở trang lỗi mạng theo URL policy; không dùng cách vòng qua |
| Responsive/console/screenshot trước v2 | Trước thay đổi UML, dashboard/tasks/insights đạt viewport 390×844, console không có error; `artifacts/ui-dashboard.jpg`, `artifacts/ui-mobile.jpg` là bằng chứng phiên bản trước |
| Startup scripts | 10 ca độc lập pass trên Windows PowerShell 5.1 và PowerShell 7 |
| Gmail SMTP thật | TLS/authentication pass bằng MailKit của API; resend OTP success và OTP mới commit sau SMTP send |
| Windows startup thực | Docker healthy, restore/build/migration và API chạy bằng Windows PowerShell 5.1; giữ Gmail và security keys |
| Database hiện tại | 39 bảng ứng dụng + __EFMigrationsHistory; 4 migrations; 2 user đều có profile; không còn các mã General/Learning/Personal |
| Sao lưu trước nâng cấp | SQL COPY_ONLY + CHECKSUM; `artifacts/backups/SPWPOS-pre-uml-v2-20261004.bak`, thư mục artifacts đã gitignore |

Ca mới kiểm tra hard/soft deadline, persisted observations, proposal→items→apply→supersede/discard, execution segments tách thời gian pause, timer trên 24 giờ không bị truncate, chỉnh actual giữ timer/raw estimate, giữ observations khi soft-delete, deadline cha/con, profile/event/preferences, opt-out thông báo, job AI thất bại, scenario/conflict không persist và backfill database cũ. Các test authentication và concurrency cũ tiếp tục đạt.

Planner tests bao gồm hard deadline, events/overlap, dependencies, non-split, locked/unaffected preservation, cold start, bias/capacity, exception, cycle, parent tránh tính trùng, DST và preferred hours với fallback cho deadline gấp. HTTP suite gồm owner isolation thực giữa hai tài khoản, stale/single-use proposal, delayed review, conflict scan/start rejection, scenario không persist, bounded NLP, CSRF cookie session không trả token và admin lock/revoke.

Chạy toàn bộ checks bằng `scripts/Test-BeforeMerge.ps1 -RequireSqlServer` khi API cùng configuration đã dừng. Test SQL không skip trong lần xác minh trên. AI provider thật chưa được gọi vì chưa có API key/model của người dùng; fallback rules đã kiểm thử. Mailpit và Gmail SMTP thật đã kiểm tra; Gmail Inbox/Spam nhận thư và CI/deployment ngoài máy này chưa được kiểm tra. Xem [phân tích lỗi và bản sửa](SYSTEM_REVIEW.md).

## Lịch sử xác minh nền tảng Authentication — 03/10/2026

Gmail SMTP/startup setup bổ sung: `tests/scripts/SmtpConfiguration.Tests.ps1` đã pass 10 ca độc lập trên Windows PowerShell 5.1 và PowerShell 7: giữ external SMTP, mặc định Mailpit khi thiếu, explicit reset Mailpit, ghi Gmail STARTTLS/identity qua stdin, reject App Password ngắn/email sai, giữ credential database dù SQL env cũ, giữ keys khi restart, explicit key rotation và reject startup chạy chồng. Fixture không truy cập User Secrets thật hoặc gửi email. Test Gmail SMTP thực được chạy riêng bằng cấu hình đã lưu, không in credential/OTP.

Môi trường: Windows x64, .NET SDK 10.0.401 được cài tại `%LOCALAPPDATA%\SPWPOS\dotnet`, runtime .NET/ASP.NET Core 10.0.12.

| Kiểm tra | Kết quả |
|---|---|
| NuGet restore locked mode | Pass, lock files cho 7 project |
| Release build / publish | Pass, 0 warnings / 0 errors; libsodium.dll trong bộ publish Windows x64 |
| Unit/service tests | **41 passed** |
| HTTP integration tests | **21 passed** |
| SQL Server concurrency/migration tests | **5 skipped** do chưa có SQL Server test |
| EF pending model changes | Không có; snapshot khớp model |
| Idempotent SQL migration script | Đã sinh `docs/schema.sql` |
| Package vulnerability audit, gồm transitive | Không phát hiện known-vulnerable package trong nguồn NuGet đang dùng |
| Live Kestrel HTTPS + Swagger | API khởi động bằng User Secrets; OpenAPI có 13 paths và Bearer scheme |
| Live GET /api/auth/me không token | HTTP 401, đúng ProblemDetails + error UNAUTHORIZED |
| Docker Compose config | Parse hợp lệ; chưa chạy container vì Docker backend stopped/500 |
| PowerShell scripts | Parse hợp lệ; setup User Secrets và script kiểm tra trước merge đã chạy |

Các test HTTP dùng TestServer và SQLite relational, cùng service/controller/middleware thật, thay SMTP bằng fake chỉ trong test assembly. Có kiểm tra issuer/audience/signature/lifetime/algorithm/session của JWT, account inactive, CORS, forged proxy header, DTO validation, HTTPS, body size, rate limit, audit và session revocation.

Argon2id PHC được kiểm tra chéo bằng `Sodium.PasswordHash.ArgonHashStringVerify`, cùng test salt khác nhau, exact password/Unicode và rehash cost. Password-change notification failure được test: password/session state đã commit và notification retry được giữ trong DB.

Chưa xác minh tại máy này: apply schema lên SQL Server thật, semantics `sp_getapplock` khi request đồng thời, image startup và SMTP thật của deployment. Đã cung cấp SQL Server test suite, compose, GitHub Actions và `.\scripts\Test-BeforeMerge.ps1 -RequireSqlServer` để chạy các kiểm tra đó. Cần connection `SPWPOS_TEST_SQLSERVER`; merge khi toàn bộ 5 SQL tests pass, không skip. CI workflow được tạo trong repository files nhưng chưa chạy trên GitHub.

HTTPS development certificate chưa được trust trên máy; live smoke dùng certificate bypass **chỉ cho localhost development**. Chạy `dotnet dev-certs https --trust` trước khi dùng Swagger/browser local. Production phải dùng certificate hợp lệ.

Test reports và vulnerability JSON nằm trong `TestResults/` (gitignored); đây là kết quả execution local, không chứa secret/token/password thật. API smoke process đã dừng sau khi kiểm tra; khởi động lại bằng lệnh trong README sau khi SQL Server sẵn sàng.

## Sửa startup — 04/10/2026

Đã chạy setup bằng **Windows PowerShell 5.1.26100.9444**: SDK 10.0.401 được chọn trước SDK tools; RNG instance và User Secrets JSON stdin hoạt động; `dotnet tool restore`, locked restore và build đều pass (0 warnings/errors). `Start-Development.ps1` cung cấp một lệnh chạy toàn bộ pipeline và dừng ngay nếu một bước lỗi. Docker Desktop vẫn báo engine stopped; startup preflight kiểm tra engine với timeout 20 giây thay vì tiếp tục migration với connection/password chưa được chuẩn bị. Tài liệu đã cập nhật cho cả PowerShell 5.1 và 7.

## Khởi động thực tế sau khi cài WSL — 04/10/2026

WSL và Docker engine đã chạy. Tài khoản hiện tại được cấp Modify trong riêng workspace `D:\SWP` để restore ghi được `obj`. Sửa đọc connection string bằng CLR `set_ConnectionString`/`get_Item`: PowerShell dictionary adapter trước đó khiến mật khẩu SQL không được đọc lại. Setup giữ mật khẩu đã lưu và dùng `tcp:127.0.0.1,1433`, khớp bind IPv4 của Docker, tránh timeout với `localhost` trên máy này.

SQL development hiện dùng volume `spwpos-sql-data`; volume cũ `swp_spwpos-sql-data` được giữ nguyên. SQL Server và Mailpit healthy. Startup bằng Windows PowerShell 5.1 thành công, áp dụng cả hai migrations và chạy API HTTPS. Chứng chỉ development đã được trust. Swagger HTTP 200, OpenAPI có 13 paths; `/api/auth/me` không token trả HTTP 401 với `UNAUTHORIZED`; Mailpit HTTP 200.

SQL Server concurrency suite: **5 passed, 0 failed, 0 skipped**. Đã sửa assertion migration để kiểm tra tất cả migrations đã áp dụng thay vì giả định chỉ có một migration. Report: `TestResults/reiko_DWAN_2026-10-04_17_31_08_net10.0.trx`. Các kết quả SQL skipped/Docker stopped/certificate chưa trust phía trên là trạng thái trước lần khởi động này.

