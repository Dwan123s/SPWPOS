<p align="center">
  <img src="src/SPWPOS.Api/wwwroot/assets/planora-brand.png" alt="PLANORA" width="420" />
</p>

<p align="center">
  <a href="https://github.com/Dwan123s/SPWPOS/actions/workflows/ci.yml">
    <img src="https://github.com/Dwan123s/SPWPOS/actions/workflows/ci.yml/badge.svg?branch=main&amp;event=push" alt="Continuous Integration" />
  </a>
  <a href="https://github.com/Dwan123s/SPWPOS/releases">
    <img src="https://img.shields.io/badge/release-unreleased-blue" alt="Release: unreleased" />
  </a>
</p>

# PLANORA — SPWPOS

**Smart Personal Workload Planning and Optimization System** — ứng dụng quản lý công việc và lập kế hoạch cá nhân dựa trên thời gian rảnh, deadline và năng lực thực tế.

PLANORA kết hợp quản lý công việc, đề xuất lịch, theo dõi phiên làm việc và phân tích khối lượng công việc trong một giao diện tiếng Việt. Ghi chú được phân tích hoàn toàn bằng AI; thuật toán lập lịch kiểm tra thời gian và ràng buộc trước khi áp dụng. Ghi nhanh và chia nhỏ công việc vẫn hỗ trợ AI tùy chọn.

## Tính năng

- **Quản lý công việc:** nhóm công việc, task/subtask, phụ thuộc, ưu tiên, deadline và tiến độ.
- **Ghi chú & agent:** lưu note, phân tích nhiều công việc, tự tạo và áp dụng lịch hoặc xem trước để chỉnh sửa.
- **Trợ lý AI:** chat tiếng Việt để cập nhật giờ rảnh, cuộc họp và task; kiểm tra/xếp lịch, xem trước, tự áp dụng và hoàn tác thay đổi hợp lệ.
- **Lập kế hoạch:** thiết lập giờ rảnh, sự kiện cố định và thời gian nghỉ; xem trước, áp dụng hoặc lập lại lịch.
- **Theo dõi thực hiện:** bắt đầu, tạm dừng, tiếp tục và kết thúc phiên; ghi nhận thời gian thực tế.
- **Phân tích workload:** so sánh estimate/actual, đánh giá năng lực, cảnh báo rủi ro deadline và mô phỏng kịch bản.
- **Tài khoản và thông báo:** đăng nhập Google qua Firebase, xác minh email bằng OTP, quản lý phiên đăng nhập, khôi phục mật khẩu và nhắc lịch.
- **Quản trị:** quản lý trạng thái tài khoản, thu hồi phiên và xem tình trạng hệ thống.

## Công nghệ

| Thành phần | Công nghệ |
|---|---|
| Backend | .NET 8 LTS, ASP.NET Core Web API, Entity Framework Core |
| Database | SQL Server |
| Frontend | React 19, Tailwind CSS 4, Vite 8; responsive, liquid glass |
| Authentication | ASP.NET Core Identity, Firebase Authentication (Google), JWT, cookie HttpOnly, Argon2id |
| Email | MailKit; Mailpit cho development |
| Kiểm thử | xUnit, HTTP integration tests, SQL Server concurrency tests, Vitest, React Testing Library |

Frontend viết bằng JSX trong `frontend/`, build thành tài nguyên tĩnh và được phục vụ cùng API. React gọi API bằng cookie HttpOnly trên cùng origin. Dependency được khóa bằng `package-lock.json` và `packages.lock.json`.

## Chạy trên Windows

### Yêu cầu

- .NET **SDK 8.0.425 trở lên trong nhánh 8.0**.
- Có thể chạy `scripts/Install-Dotnet8.ps1` để cài SDK riêng cho dự án từ Microsoft và kiểm tra SHA-512. Script tìm SDK trong thư mục riêng của dự án, `%USERPROFILE%\.dotnet` và các đường dẫn trên PATH; có thể cài đồng thời các phiên bản .NET khác.
- Docker Desktop với Linux containers; engine đang chạy.
- Windows PowerShell 5.1 hoặc PowerShell 7.
- Git để clone repository.
- Node.js **24.15+** và npm. Nếu Node hiện tại chưa phù hợp, script Windows tự cài Node 24 LTS dạng portable riêng cho dự án, kiểm tra SHA-256 từ nguồn chính thức.

### Khởi động

```powershell
git clone https://github.com/Dwan123s/SPWPOS.git
cd SPWPOS
powershell.exe -NoProfile -ExecutionPolicy Bypass -File .\scripts\Start-Development.ps1
```

Script build React/Tailwind, chuẩn bị cấu hình development, khởi động SQL Server/Mailpit, khôi phục dependency, thiết lập HTTPS certificate, cập nhật database và chạy API. Windows có thể yêu cầu xác nhận tin cậy certificate ở lần đầu. Giữ terminal mở; nhấn `Ctrl+C` để dừng.

| Dịch vụ | Địa chỉ |
|---|---|
| Ứng dụng | https://localhost:7043/ |
| Swagger | https://localhost:7043/swagger |
| Mailpit — hộp thư OTP development | http://localhost:8025/ |

Nếu certificate đã được thiết lập, thêm `-SkipCertificateTrust` vào lệnh khởi động. Cấu hình SMTP hiện có được giữ khi restart; xem [hướng dẫn Gmail SMTP](docs/integrations/GMAIL_SMTP.md) để gửi email thật.

Để bật nút Google, làm theo [hướng dẫn thiết lập Firebase Authentication](docs/integrations/FIREBASE_GOOGLE_LOGIN.md). Firebase Admin xác minh token tại server; ứng dụng vẫn dùng database và phiên cookie hiện tại.

SQL Server development dùng `127.0.0.1,11433` trên Windows; container vẫn dùng port `1433`. Cổng host riêng tránh xung đột với SQL Server đã cài trên máy. Script cập nhật connection string và giữ nguyên credential cùng volume dữ liệu hiện có.

Nếu cổng `11433` bị chiếm hoặc Windows chặn bind, chọn cổng host khác trong terminal trước khi khởi động:

```powershell
$env:SPWPOS_SQL_PORT = '15433'
powershell.exe -NoProfile -ExecutionPolicy Bypass -File .\scripts\Start-Development.ps1
```

Đặt cùng giá trị `SPWPOS_SQL_PORT` ở mỗi lần khởi động khi dùng cổng tùy chỉnh; script dùng giá trị này cho cả Docker Compose và connection string của API.

### Phát triển frontend

Mở terminal thứ hai khi API đang chạy:

```powershell
powershell.exe -NoProfile -ExecutionPolicy Bypass -File .\scripts\Build-Frontend.ps1 -Watch
```

Vite build lại khi sửa JSX/CSS. Tải lại `https://localhost:7043/` để xem thay đổi; không cần server frontend riêng. Nếu đã cài Node 24.15+, có thể chạy `npm ci`, `npm run build`, `npm test` trong `frontend/`.

Khi sửa backend C# (ví dụ DTO hoặc quy tắc mật khẩu), dừng API đang chạy và chạy lại `Start-Development.ps1 -SkipCertificateTrust` để build và nạp code mới. Tải lại trang chỉ cập nhật frontend; API đang chạy vẫn dùng assembly đã nạp trước đó.

Nếu gặp `Failed to bind ... 7043: address already in use`, cổng HTTPS đang được một tiến trình khác sử dụng. `Start-Development.ps1` kiểm tra API trong cả `bin` và `artifacts/api-runtime`, kiểm tra cổng trước khi chuẩn bị môi trường, và chặn chạy trùng trong suốt phiên server. Dừng phiên cũ bằng `Ctrl+C` trong terminal đã chạy nó trước khi khởi động lại. Có thể xem PID giữ cổng bằng `Get-NetTCPConnection -State Listen -LocalPort 7043 | Select-Object LocalAddress, OwningProcess`; nếu API được chạy nền bằng `Restart-PlanoraApi.ps1`, dùng lại script đó để thay phiên API của checkout này.

Khi demo nhiều tài khoản bằng các browser khác nhau trên cùng máy, mỗi tài khoản có hạn mức API riêng (240 request/phút) để tải nền nhóm/chat/thông báo không dùng chung hạn mức theo IP. Request API chưa đăng nhập vẫn giới hạn 60/phút/IP; đăng nhập, OTP, ảnh và giọng nói có thêm hạn mức riêng. HTTP 429 trả thời gian chờ qua `Retry-After`; frontend tạm ngừng gửi lại cùng thao tác trong thời gian này và giữ phiên đăng nhập.

`dotnet publish src/SPWPOS.Api -c Release` tự build và đóng gói frontend. Bản triển khai chỉ cần ASP.NET Core runtime và cấu hình backend.

## Cấu hình

`appsettings.example.json` mô tả các nhóm cấu hình. Khóa JWT/OTP, connection string và credential SMTP/AI được lưu bằng **User Secrets** trong development hoặc environment variables/secret manager khi triển khai.

Script development tự tạo các secret cần thiết. Không đưa giá trị thật vào `appsettings.json`, `.env` được theo dõi bởi Git hoặc tài liệu. AI và admin bootstrap là tùy chọn; hướng dẫn cấu hình nằm trong [hướng dẫn hệ thống](docs/guides/SYSTEM_GUIDE.md).

## Cấu trúc dự án

```text
SPWPOS.sln
src/
  SPWPOS.Domain/          # Entities và enums
  SPWPOS.Application/     # DTOs, interfaces và thuật toán lập lịch
  SPWPOS.Infrastructure/  # Services, persistence, authentication và workers
  SPWPOS.Api/             # API, middleware và tài nguyên frontend đã build
frontend/                 # React JSX, Tailwind/CSS, Vite và component tests
tests/
  SPWPOS.UnitTests/
  SPWPOS.IntegrationTests/
  SPWPOS.TestSupport/
scripts/                  # Khởi động development và kiểm tra trước merge
docs/                     # Hướng dẫn, mô hình dữ liệu và tài liệu kỹ thuật
```

Backend tổ chức theo các lớp `Api → Infrastructure → Application → Domain`. Migrations nằm trong `src/SPWPOS.Infrastructure/Persistence/Migrations`.

## Kiểm thử

GitHub Actions chạy workflow [CI](.github/workflows/ci.yml) khi push, mở/cập nhật
pull request, hoặc chạy thủ công từ tab **Actions → CI → Run workflow**.
Ba job độc lập kiểm tra:

- Frontend: Vitest/React Testing Library, browser session-refresh tests, production build và npm audit.
- Backend: locked restore, Release build, unit/integration tests với SQL Server riêng cho mỗi run, migration snapshot và NuGet audit.
- Windows scripts: regression tests cho cấu hình cổng SQL Server và SMTP trên Windows PowerShell.

SQL Server dùng credential ngẫu nhiên, không cần cấu hình repository secrets.
Báo cáo JUnit/TRX, NuGet audit và log SQL Server nằm trong artifacts của run,
được giữ 14 ngày. Run mới trên cùng branch sẽ hủy run cũ đang chạy.

Dừng API trước khi build hoặc chạy bộ kiểm tra. Từ thư mục repository:

```powershell
Set-ExecutionPolicy -Scope Process -ExecutionPolicy Bypass
. .\scripts\Initialize-Dotnet.ps1
dotnet restore --locked-mode
dotnet build -c Release --no-restore
dotnet test -c Release --no-build
```

Các ca concurrency/migration SQL Server cần biến môi trường `SPWPOS_TEST_SQLSERVER` trỏ tới server test; chúng được bỏ qua nếu chưa cấu hình. Test factory tạo database riêng cho mỗi lần chạy.

Để chạy bộ kiểm tra đầy đủ, cấu hình connection test rồi chạy:

```powershell
powershell.exe -NoProfile -ExecutionPolicy Bypass -File .\scripts\Test-BeforeMerge.ps1 -RequireSqlServer
```

## Tài liệu

- [Mục lục tài liệu / Documentation index](docs/README.md)
- [Thư viện ảnh sơ đồ database — English / Tiếng Việt](docs/database/current/images/README.md)
- [Sơ đồ database hiện tại — sinh tự động từ SQL Server](docs/database/current/index.html) và [lệnh tạo lại](scripts/database-diagram/README.md)
- [Getting Started: nghiên cứu, thiết kế và luồng trước đăng nhập](docs/design/GETTING_STARTED_RESEARCH.md)
- [Hướng dẫn cho người dùng và tutorial 4 bước](docs/guides/USER_GUIDE.md)
- [Hướng dẫn sử dụng và cấu hình hệ thống](docs/guides/SYSTEM_GUIDE.md)
- [Tên đăng nhập và đăng nhập bằng email](docs/features/USERNAME_LOGIN.md)
- [Ghi chú và agent tự xếp lịch](docs/features/NOTES_AGENT_GUIDE.md)
- [Chat agent và tự điều chỉnh khi thay đổi thời gian](docs/features/PLANNING_AGENT_GUIDE.md)
- [Cấu hình Gemini trên Google Cloud và kiểm tra kết nối](docs/integrations/GOOGLE_CLOUD_AI.md)
- [Kế hoạch nâng cấp PLANORA qua bốn giai đoạn](docs/plans/PLANORA_UPGRADE_PLAN.md)
- [Cấu hình Gmail SMTP](docs/integrations/GMAIL_SMTP.md)
- [Use cases](docs/development/USE_CASE_COVERAGE.md) và [mô hình dữ liệu UML](docs/database/UML_ALIGNMENT.md)
- [Thiết kế bảo mật](docs/development/SECURITY.md)
- [Phân tích kiến trúc và xác minh .NET 8](docs/development/NET8_MIGRATION_2026-10-06.md)
- [Frontend React/Tailwind và quy trình build](docs/development/REACT_MIGRATION_2026-10-06.md)
