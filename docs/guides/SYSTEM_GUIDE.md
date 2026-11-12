# SPWPOS — Hướng dẫn hệ thống và giao diện

## Chạy trên máy Windows

Yêu cầu .NET **SDK 8.0.100 trở lên trong nhánh 8.0** (bản ổn định), Docker Desktop engine đang chạy và HTTPS development certificate được tin cậy. Workspace hiện dùng SDK riêng trong `%LOCALAPPDATA%\SPWPOS\dotnet8`; script tự nhận diện SDK này. Solution dùng `SPWPOS.sln` để tương thích SDK 8.

```powershell
cd D:\SWP
.\scripts\Start-Development.ps1
```

Nếu PowerShell chặn script, chạy `powershell.exe -NoProfile -ExecutionPolicy Bypass -File .\scripts\Start-Development.ps1`. Xem [phân tích và kiểm chứng .NET 8](../development/NET8_MIGRATION_2026-10-06.md).

Nếu certificate đã được cấu hình:

```powershell
.\scripts\Start-Development.ps1 -SkipCertificateTrust
```

- Giao diện: **https://localhost:7043/**
- Swagger: https://localhost:7043/swagger
- Hộp thư OTP development: http://localhost:8025
- Gửi OTP tới Gmail thật: xem [cấu hình Gmail SMTP](../integrations/GMAIL_SMTP.md). Script khởi động giữ cấu hình SMTP đã lưu; mặc định Mailpit chỉ được tạo khi chưa cấu hình SMTP.
- SQL Server: `tcp:127.0.0.1,1433`, database `SPWPOS`.

Giữ terminal chạy API. Khi thay đổi C# hoặc chạy build/tests cùng configuration, dừng API bằng Ctrl+C trước để Windows không khóa DLL. Frontend React/Tailwind nằm trong `frontend/`, cần Node.js 24.15+ và npm để build. Script Windows tự chuẩn bị Node LTS portable nếu cần. Chạy `powershell.exe -NoProfile -ExecutionPolicy Bypass -File .\scripts\Build-Frontend.ps1 -Watch` trong terminal thứ hai để build lại khi sửa JSX/CSS, rồi tải lại trang. Tài nguyên build nằm trong `src/SPWPOS.Api/wwwroot/app`, được phục vụ cùng API; không cần server frontend riêng.

Script setup giữ password SQL, SMTP và khóa JWT/OTP hiện có, lưu secrets ngoài repository. Chỉ dùng `-RotateSecurityKeys` khi chủ động rotate khóa development; khi đó các phiên/OTP cũ cần tạo lại. Startup không chạy chồng nhau. Không xóa Docker volume để sửa lỗi khởi động.

## Sử dụng giao diện

1. **Đăng ký:** nhập email, mật khẩu dài ít nhất 10 ký tự, tên và họ. Mở email trong Mailpit khi chạy local, nhập OTP. Sau xác minh, đăng nhập.
2. **Thời gian & tùy chọn:** thêm giờ rảnh cho từng ngày, lưu; thêm ngoại lệ bận/rảnh và sự kiện cố định. Chọn múi giờ, phiên tối thiểu/tối đa và giờ nghỉ. Giờ kết thúc hỗ trợ `24:00`.
3. **Công việc:** tạo việc với deadline, thời lượng, ưu tiên và nhóm. Tùy chọn thêm cho phép cấu hình loại việc, chia phiên, mô tả và việc cha. Trang danh sách có tìm kiếm, lọc trạng thái, nhóm, ưu tiên và khoảng deadline.
4. **Chi tiết công việc:** sửa/xóa, cập nhật tiến độ, thêm việc con, thêm/bỏ phụ thuộc. Backend từ chối phụ thuộc vòng lặp hoặc dữ liệu thuộc người dùng khác. Chỉ việc độc lập và việc con được tính workload; việc cha đóng vai trò nhóm.
5. **Lập kế hoạch:** chọn thời điểm bắt đầu và 1–30 ngày. Xem trước các phiên, cảnh báo và lý do rồi áp dụng. Đề xuất có hiệu lực 30 phút, dùng một lần; nếu dữ liệu thay đổi phải tạo lại. Phiên mới bắt đầu sớm nhất sau 5 phút để có thời gian xem xét.
6. **Lịch tuần:** xem phiên/sự kiện, chuyển tuần; mở chi tiết để di chuyển, sửa thời lượng, khóa/mở khóa hoặc chia phiên. Các chỉnh sửa được kiểm tra giờ rảnh, deadline, phụ thuộc và chồng lịch. Xung đột do thay đổi lịch được cảnh báo; dùng “Lập lại phần lịch liên quan”.
7. **Thực hiện:** bắt đầu phiên sớm tối đa 15 phút; tạm dừng/tiếp tục; kết thúc hoặc bỏ qua. Chỉ một phiên chạy hoặc tạm dừng cùng lúc. Đồng hồ không tính thời gian tạm dừng. Kết thúc phiên không mặc định hoàn thành toàn bộ công việc: cập nhật tiến độ riêng.
8. **Thời gian thực tế:** mở chi tiết phiên đã hoàn thành để sửa số phút nếu quên bấm đồng hồ. Phân tích hiển thị planned/actual, bias, capacity và reason của từng rủi ro.
9. **Lập lại lịch:** trong chi tiết công việc chọn lập lại phần liên quan; planner giữ phiên đã khóa/đang chạy, phiên không bị ảnh hưởng và lịch sử thực hiện. Xem trước rồi áp dụng.
10. **Thử kịch bản:** nhập một công việc giả định để so sánh trước/sau, xem lịch giả định. Mô phỏng không tạo task, proposal hay session trong database. Có thể chuyển gợi ý thành form tạo việc và xác nhận riêng.
11. **Thông báo:** xem deadline trong 24 giờ, phiên sắp đến trong 10 phút (mặc định), workload và thay đổi lịch; đánh dấu đã đọc. Worker quét mỗi 60 giây; giao diện cũng cập nhật khi tải workspace.
12. **Tài khoản & nhóm:** sửa tên, quản lý nhóm, đổi mật khẩu bằng mật khẩu hiện tại + OTP + mật khẩu mới; đăng xuất mọi thiết bị. Đổi/reset mật khẩu thu hồi cả refresh session và quyền sử dụng access JWT cũ.

## Ghi nhanh và gợi ý chia nhỏ

“Ghi nhanh” nhận câu tiếng Việt/Anh, ví dụ: `Viết báo cáo trước thứ Sáu, khoảng 3 tiếng`. Bộ quy tắc nhận giờ/phút, ngày mai/hôm nay, ngày trong tuần và ngày ISO; không nhận diện được thì mặc định 60 phút, deadline sau 7 ngày. Luôn hiển thị form để bạn kiểm tra và xác nhận.

Gợi ý chia nhỏ trả các bước theo loại việc; chọn những mục cần lưu. Các gợi ý chưa được xác nhận không thay đổi dữ liệu. Khi việc cha có phiên đã khóa/đang chạy phải bỏ khóa/kết thúc trước khi thêm việc con; phiên chưa thực hiện của việc cha được hủy để tránh tính trùng.

AI là tùy chọn: người dùng phải chọn đồng ý gửi nội dung task trong giao diện. Cấu hình trên server:

```powershell
. .\scripts\Initialize-Dotnet.ps1
& $SpwposDotnet user-secrets set 'AiSettings:Enabled' 'true' --project src/SPWPOS.Api
& $SpwposDotnet user-secrets set 'AiSettings:Model' '<model hỗ trợ structured outputs>' --project src/SPWPOS.Api
# Đặt AiSettings:ApiKey bằng User Secrets/secret manager của bạn; tránh lưu khóa trong script/history.
```

Environment tương ứng: `AiSettings__Enabled`, `AiSettings__Model`, `AiSettings__ApiKey`. Adapter dùng OpenAI Responses API với JSON schema, `store=false`, giới hạn phản hồi và timeout. Chỉ gửi nội dung bạn chọn; không gửi email, mật khẩu hay lịch sử tài khoản. Khi chưa cấu hình hoặc provider lỗi, dùng bộ quy tắc. Chưa kiểm thử gọi provider thật với khóa của bạn. Tham khảo [Structured outputs](https://developers.openai.com/api/docs/guides/structured-outputs).

## Quản trị viên development

Tạo và xác minh tài khoản của chính bạn trước. Dừng API, cấu hình email đã xác minh:

```powershell
. .\scripts\Initialize-Dotnet.ps1
& $SpwposDotnet user-secrets set 'AdminBootstrap:Email' '<email của bạn>' --project src/SPWPOS.Api
& $SpwposDotnet run --project src/SPWPOS.Api --launch-profile https
```

Đăng nhập lại để JWT nhận role `Admin`; mục **Quản trị** sẽ xuất hiện. Có tìm người dùng, khóa/mở khóa và xem database/session/tác vụ thông báo. Không được tự khóa tài khoản admin đang dùng. Khóa tài khoản thu hồi toàn bộ session của người bị khóa; mở lại không khôi phục token cũ.

Không có admin/mật khẩu mặc định. `AdminBootstrap` chỉ cho Development, từ chối Production. Production cần quy trình cấp role riêng do người vận hành kiểm soát, không public API nâng quyền. Có thể bỏ cấu hình bootstrap sau lần cấp role; role vẫn được giữ trong Identity.

## Mô hình dữ liệu và thuật toán

Identity/security tables được giữ nguyên; migration `AddWorkloadSystem` bổ sung:

| Table | Trách nhiệm |
|---|---|
| WorkTasks | Task, tiến độ, thời lượng, deadline, category, parent; soft delete |
| TaskCategories | Nhóm duy nhất theo user và tên |
| TaskDependencies | Cạnh tiền nhiệm, duy nhất theo user/task/predecessor |
| WeeklyAvailability | Các cửa sổ rảnh theo ngày trong tuần |
| AvailabilityExceptions | Bận hoặc rảnh bổ sung theo ngày |
| FixedEvents | Sự kiện cố định theo UTC instant |
| WorkPreferences | Múi giờ, phiên/break, preferred hours và revision |
| PlanProposals | Đề xuất JSON, revision, expiry, trạng thái đã áp dụng |
| WorkSessions | Lịch phiên, khóa, trạng thái, actual seconds, timer segment |
| InAppNotifications | Thông báo theo user với dedupe key |

FK composite `(UserId, Id)` ở task/category/parent/dependency/session ngăn tham chiếu chéo tài khoản ngay tại SQL. FK dùng Restrict. Unique filtered index bảo đảm một phiên Running/Paused mỗi user. Mutation dùng transaction và cùng SQL Server `sp_getapplock` theo normalized email như AuthService; hoạt động trên nhiều API instance. Proposal áp dụng trong transaction, revision chống ghi đè lịch mới.

Planner xác định, chạy không cần AI: topological dependencies → deadline → priority → khoảng phù hợp trong availability, trừ events/exceptions và phiên được giữ. Phiên không chia nhỏ cần khoảng liền mạch và không vượt max session. Ưu tiên cả giờ bắt đầu và kết thúc của preferred hours, rồi dùng giờ rảnh khác khi cần đáp ứng deadline. Buffer nghỉ giữa các phiên mới; cảnh báo nếu không đủ chỗ trước hard deadline. Bài toán dùng heuristic khả thi, không cam kết nghiệm tối ưu toàn cục.

Bias cần 3 observation có actual > 0; lấy tối đa 50 mẫu gần nhất, giữ lịch sử sau xóa mềm và tính tổng actual / tổng estimate, giới hạn 0.5–3, ưu tiên theo loại rồi global. Capacity cần 5 phiên đã đến hạn, lấy completed planned / tổng planned (completed/skipped/missed), giới hạn 25–100%; cold start 80%. Risk dựa deadline, capacity deficit, phụ thuộc và khoảng liên tục, luôn có reason code. Ngày có DST: giờ không tồn tại dời đến phút hợp lệ kế tiếp; ngày thường theo múi giờ đã chọn. Giới hạn 500 task hoạt động, horizon 30 ngày và một cấp subtask được kiểm tra phía server.

## Migration và kiểm thử

```powershell
. .\scripts\Initialize-Dotnet.ps1
& $SpwposDotnet tool restore
& $SpwposDotnet restore --locked-mode
# ConnectionStrings__DefaultConnection được setup script đặt trong cùng terminal.
& $SpwposDotnet ef database update --project src/SPWPOS.Infrastructure
& $SpwposDotnet test
```

Concurrency suite cần SQL Server thật, **không dùng SQLite để kết luận concurrency**. Đặt `SPWPOS_TEST_SQLSERVER` bằng connection từ secrets (không in ra), rồi chạy `scripts/Test-BeforeMerge.ps1 -RequireSqlServer`. Test factory tạo database `SPWPOS_Tests_<Guid>` riêng và chỉ xóa đúng namespace đó; không xóa database SPWPOS.

Các ca cần pass: Auth/OTP/password/refresh, cross-owner CRUD, dependency cycle, invalid timezone/windows, proposal stale/single-use/delay approval, locked conflict, start/pause/resume actual, scenario không persist, bounded NLP, Admin lock/revoke, CSRF + token-free browser response, concurrent proposal apply và session start trên SQL Server.

## Bảo mật UI/API

- Không dùng localStorage/sessionStorage cho JWT hoặc refresh token. Cookie Secure, HttpOnly, SameSite Strict; refresh cookie giới hạn path.
- Mutation cookie yêu cầu `X-SPWPOS: 1`, kiểm tra Origin; CORS chỉ origin cụ thể. Bearer API vẫn hỗ trợ Swagger/Postman.
- Nội dung user được escape trước khi ghép HTML; không render Markdown/HTML do AI trả về. CSP chỉ script/style cùng origin.
- Không log password, OTP, JWT, refresh token, AI key hay provider response.
- HTTPS, security headers, DTO validation, body limit, rate limit và kiểm tra owner/role trên server.
- Các ảnh/chỉ số demo trong buổi kiểm tra dùng dữ liệu local riêng; không được tự động seed trên Production.


Bản UML v2 đã được đối chiếu và bổ sung persistence. Xem UML_ALIGNMENT.md cho ánh xạ toàn bộ bảng, deadline cứng/mềm, cài đặt thông báo, lịch sử timer và job quản trị.
