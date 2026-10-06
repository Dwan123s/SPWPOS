<p align="center">
  <img src="src/SPWPOS.Api/wwwroot/assets/planora-brand.png" alt="PLANORA" width="420" />
</p>

# PLANORA — SPWPOS

**Smart Personal Workload Planning and Optimization System** — ứng dụng quản lý công việc và lập kế hoạch cá nhân dựa trên thời gian rảnh, deadline và năng lực thực tế.

PLANORA kết hợp quản lý công việc, đề xuất lịch, theo dõi phiên làm việc và phân tích khối lượng công việc trong một giao diện tiếng Việt. Thuật toán lập lịch hoạt động độc lập; AI là tùy chọn hỗ trợ nhập và chia nhỏ công việc.

## Tính năng

- **Quản lý công việc:** nhóm công việc, task/subtask, phụ thuộc, ưu tiên, deadline và tiến độ.
- **Lập kế hoạch:** thiết lập giờ rảnh, sự kiện cố định và thời gian nghỉ; xem trước, áp dụng hoặc lập lại lịch.
- **Theo dõi thực hiện:** bắt đầu, tạm dừng, tiếp tục và kết thúc phiên; ghi nhận thời gian thực tế.
- **Phân tích workload:** so sánh estimate/actual, đánh giá năng lực, cảnh báo rủi ro deadline và mô phỏng kịch bản.
- **Tài khoản và thông báo:** xác minh email bằng OTP, quản lý phiên đăng nhập, khôi phục mật khẩu và nhắc lịch.
- **Quản trị:** quản lý trạng thái tài khoản, thu hồi phiên và xem tình trạng hệ thống.

## Công nghệ

| Thành phần | Công nghệ |
|---|---|
| Backend | .NET 8, ASP.NET Core Web API, Entity Framework Core |
| Database | SQL Server |
| Frontend | React 19, Tailwind CSS 4, Vite 8; responsive, liquid glass |
| Authentication | ASP.NET Core Identity, JWT, cookie HttpOnly, Argon2id |
| Email | MailKit; Mailpit cho development |
| Kiểm thử | xUnit, HTTP integration tests, SQL Server concurrency tests, Vitest, React Testing Library |

Frontend viết bằng JSX trong `frontend/`, build thành tài nguyên tĩnh và được phục vụ cùng API. React gọi API bằng cookie HttpOnly trên cùng origin. Dependency được khóa bằng `package-lock.json` và `packages.lock.json`.

## Chạy trên Windows

### Yêu cầu

- .NET **SDK 8.0.425 trở lên trong nhánh 8.0**.
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

Nếu certificate đã được thiết lập, thêm `-SkipCertificateTrust` vào lệnh khởi động. Cấu hình SMTP hiện có được giữ khi restart; xem [hướng dẫn Gmail SMTP](docs/GMAIL_SMTP.md) để gửi email thật.

SQL Server development dùng `127.0.0.1,11433` trên Windows; container vẫn dùng cổng `1433`. Cổng host riêng tránh xung đột với SQL Server đã cài trên máy. Script cập nhật connection string và giữ nguyên credential cùng volume dữ liệu hiện có.

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

`dotnet publish src/SPWPOS.Api -c Release` tự build và đóng gói frontend. Bản triển khai chỉ cần ASP.NET Core runtime và cấu hình backend.

## Cấu hình

`appsettings.example.json` mô tả các nhóm cấu hình. Khóa JWT/OTP, connection string và credential SMTP/AI được lưu bằng **User Secrets** trong development hoặc environment variables/secret manager khi triển khai.

Script development tự tạo các secret cần thiết. Không đưa giá trị thật vào `appsettings.json`, `.env` được theo dõi bởi Git hoặc tài liệu. AI và admin bootstrap là tùy chọn; hướng dẫn cấu hình nằm trong [hướng dẫn hệ thống](docs/SYSTEM_GUIDE.md).

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

- [Hướng dẫn sử dụng và cấu hình hệ thống](docs/SYSTEM_GUIDE.md)
- [Cấu hình Gmail SMTP](docs/GMAIL_SMTP.md)
- [Use cases](docs/USE_CASE_COVERAGE.md) và [mô hình dữ liệu UML](docs/UML_ALIGNMENT.md)
- [Thiết kế bảo mật](docs/SECURITY.md)
- [Phân tích kiến trúc và xác minh .NET 8](docs/NET8_MIGRATION_2026-10-06.md)
- [Frontend React/Tailwind và quy trình build](docs/REACT_MIGRATION_2026-10-06.md)
