# Phân công phát triển PLANORA cho nhóm 4 người

## Cách chia

Nền tảng hiện tại đã có React/Tailwind, API .NET 8, SQL Server và các luồng nghiệp vụ chính. Bảng này giao quyền phụ trách, hoàn thiện và phát triển tiếp trên code hiện có; không yêu cầu viết lại chức năng đã hoạt động.

Mỗi thành viên chịu trách nhiệm cả UI, API, dữ liệu và kiểm thử trong module của mình. Tạm dùng TV1–TV4; thay bằng tên thật khi nhóm thống nhất. Phân bổ bao phủ đủ 72 use case trong `USE_CASE_COVERAGE.md`, không tính số UC như thước đo duy nhất của độ khó.

| Thành viên | Phạm vi chính | Use case | Trách nhiệm phối hợp |
|---|---|---|---|
| TV1 | Tài khoản, bảo mật, thông báo, quản trị | UC01–08, UC65–72 | Điều phối tích hợp, khung UI và migration chung |
| TV2 | Công việc, nhóm công việc, việc con, phụ thuộc, nhập/chia nhỏ bằng AI | UC09–26, UC55 | Hợp đồng dữ liệu task cho planner và thống kê |
| TV3 | Lịch rảnh, calendar, sự kiện, lập kế hoạch và điều chỉnh lịch | UC27–48 | Thuật toán và tính nhất quán của lịch |
| TV4 | Thực hiện phiên, dashboard, phân tích cá nhân, kịch bản | UC49–54, UC56–64 | CI, kiểm thử tích hợp và chuẩn bị bản demo cùng các chủ module |

## TV1 — Tài khoản, bảo mật và quản trị

| Task | Công việc | Tiêu chí hoàn thành |
|---|---|---|
| A01 | Hoàn thiện đăng ký, xác minh email, login/logout, quên/đổi mật khẩu | OTP sai/hết hạn được xử lý; form báo lỗi rõ; không đưa token vào localStorage |
| A02 | Hồ sơ, avatar, cropper, kiểm tra quyền dữ liệu tài khoản | Cập nhật tên/bio/ảnh đúng; không mất dữ liệu chưa lưu khi chỉnh ảnh; người dùng chỉ sửa hồ sơ của mình |
| A03 | Thông báo và tùy chọn nhắc việc | Nhắc deadline/phiên/workload đúng cấu hình; đánh dấu đã đọc; tránh thông báo trùng |
| A04 | Quản trị tài khoản và trạng thái hệ thống | Admin tìm, khóa/mở khóa, xem lịch sử; API chặn người không có quyền; thao tác được ghi nhận |
| A05 | Khung dùng chung và tích hợp | Duy trì App/router, shell, modal/toast, API client và design token; điều phối thay đổi contract/schema qua PR |

Code chính: `Authentication/`, `Security/`, `Email/`, `NotificationAdminService.cs`, `JobHistoryService.cs`; frontend `auth.jsx`, `auth-view.jsx`, các phần profile/notification/admin hiện nằm trong `pages.jsx`, cropper trong `interactive.jsx`.

Demo cá nhân: đăng ký → OTP → đăng nhập → cập nhật hồ sơ → thay đổi cài đặt thông báo; demo riêng tài khoản Admin khóa/mở khóa một tài khoản test.

## TV2 — Quản lý công việc

| Task | Công việc | Tiêu chí hoàn thành |
|---|---|---|
| B01 | Task CRUD, chi tiết, tìm kiếm và bộ lọc | Tên, deadline, ưu tiên, estimate được validate; xử lý danh sách rỗng, loading và lỗi API |
| B02 | Nhóm công việc | Tạo/sửa/xóa nhóm; màu hợp lệ; xóa nhóm không làm mất công việc |
| B03 | Subtask, dependency và tiến độ | Chặn vòng lặp/phụ thuộc không hợp lệ; status/progress nhất quán; tránh tính trùng workload của cha và con |
| B04 | Capture ngôn ngữ tự nhiên và gợi ý chia nhỏ | Có bước kiểm tra trước lưu; chỉ gọi AI khi đồng ý; fallback quy tắc khi AI chưa cấu hình/lỗi; retry không lưu trùng mục đã thành công |
| B05 | Kiểm thử task và bàn giao contract | Test validation, quyền sở hữu, xóa task/subtask, dependency và luồng UI; thống nhất DTO với TV3/TV4 |

Code chính: `TaskService.cs`, `TaskInterpreter.cs`, DTO/entity task; frontend `tasks.jsx`, phần nhóm công việc hiện nằm trong settings của `pages.jsx`.

Demo cá nhân: tạo task → thêm nhóm/subtask/dependency → lọc danh sách → cập nhật tiến độ → nhập nhanh và xác nhận trước lưu.

## TV3 — Thời gian và lập kế hoạch

| Task | Công việc | Tiêu chí hoàn thành |
|---|---|---|
| C01 | Giờ rảnh, ngoại lệ, sự kiện và tùy chọn | Chặn khoảng giờ không hợp lệ; xử lý overlap, múi giờ và ngày kết thúc đúng |
| C02 | Calendar tuần/danh sách và mobile | Hiển thị đúng ngày/giờ; sự kiện qua nửa đêm không lặp sai; dữ liệu rỗng có hướng dẫn |
| C03 | Generate → preview → apply/discard | Preview không đổi lịch; áp dụng nhất quán; thông báo rõ các việc chưa xếp được và lý do |
| C04 | Dời/đổi thời lượng/khóa/mở khóa/chia phiên, reschedule | Bảo vệ phiên khóa và lịch sử; kiểm tra xung đột; giữ các phiên không bị ảnh hưởng |
| C05 | Củng cố thuật toán và kiểm thử lịch | Test deadline cứng/mềm, phụ thuộc, giờ nghỉ, capacity và kế hoạch cũ; apply đồng thời chỉ thành công một lần |

Code chính: `PlanningEngine.cs`, `TimeService.cs`, `PlanService.cs`, các phương thức điều chỉnh lịch hiện trong `ExecutionService.cs`; frontend `schedule.jsx`, phần time/planning của `calendar.jsx`, lịch rảnh trong `interactive.jsx`.

Demo cá nhân: thiết lập giờ rảnh và sự kiện → tạo đề xuất → giải thích lý do → áp dụng → khóa phiên → lập lại phần lịch liên quan.

Đây là phần có độ phức tạp cao. TV4 phối hợp về capacity/bias và kiểm thử tích hợp; phạm vi C vẫn do TV3 chịu trách nhiệm chính.

## TV4 — Thực hiện phiên và phân tích

| Task | Công việc | Tiêu chí hoàn thành |
|---|---|---|
| D01 | Start/pause/resume/complete/skip, lịch sử và actual duration | Timer đúng sau tải lại; transition hợp lệ; một người không chạy hai phiên; sửa actual không làm mất timer/history |
| D02 | Dashboard và biểu đồ planned/actual | Dữ liệu khớp API; phân biệt dữ liệu rỗng với lỗi; hiển thị đúng múi giờ và mobile |
| D03 | Bias, capacity, workload và rủi ro deadline | Đúng ngưỡng mẫu và phạm vi phân tích; tránh tính trùng task cha/con; không hiển thị rủi ro thấp giả khi API lỗi |
| D04 | What-if scenario | So sánh before/after và lịch giả định; không lưu task, không đổi lịch, không cho apply/discard kế hoạch giả định |
| D05 | CI, kiểm thử tích hợp và bản demo | Pipeline build/test/publish chạy từ checkout sạch; phối hợp các chủ module sửa test hỏng; chuẩn bị dữ liệu demo không chứa secret thật |

Code chính: phần execution của `ExecutionService.cs`, `LearningService.cs`, scenario trong `PlanService.cs`; frontend `dashboard.jsx`, phần insights/scenario của `pages.jsx`, `SessionTimer` và controls của session. CI trong `.github/workflows/authentication.yml`.

Demo cá nhân: chạy/tạm dừng/hoàn thành phiên → chỉnh actual → xem planned/actual và workload → mô phỏng nhận thêm một công việc.

## Công việc chung trước khi phát triển song song

Thực hiện trong 1–2 ngày đầu, giữ các route và hành vi API hiện tại:

1. TV1 điều phối tách `WorkspaceControllers.cs` và `Contracts.cs` thành file theo module. Các controller/DTO được chuyển cho chủ module tương ứng.
2. TV1 cùng cả nhóm tách dispatch trong `frontend/src/controller.jsx` thành handler theo feature. File gốc chỉ còn điều phối và đăng ký handler; không để 4 người cùng sửa switch lớn.
3. Tách `pages.jsx`: TV1 nhận profile/notifications/admin; TV2 nhận categories; TV4 nhận insights/scenario. TV3 và TV4 tách phần chỉnh lịch với phần thực hiện phiên trong `calendar.jsx`/`ExecutionService.cs`.
4. Tách `interactive.jsx`: TV1 nhận avatar/cropper; TV3 nhận availability editor. Thành phần dùng chung giữ trong `ui.jsx`/`react-ui.jsx` dưới điều phối TV1.
5. Chốt DTO, enum, error code, quy tắc ownership và style UI. Cả nhóm chạy được project và bộ kiểm thử trước khi bắt đầu feature branch.

Đây là kế hoạch refactor, chưa phải các thư mục/file mới đã được tạo.

## Phối hợp và Git

- Mỗi task là một issue và một branch riêng, ví dụ `feat/A01-auth-validation`, `feat/B03-task-dependencies`, `feat/C04-reschedule`, `feat/D03-workload-analysis`.
- PR nhỏ theo task; không push trực tiếp lên `main` trong quá trình nhóm phát triển. Merge sau review và CI pass.
- Chủ task tự viết test cho module của mình. TV4 điều phối test tích hợp, không nhận toàn bộ việc kiểm thử thay nhóm.
- Review chéo: TV1 ↔ TV2, TV3 ↔ TV4. Thay đổi ảnh hưởng module khác cần thêm review từ chủ module đó.
- TV1 điều phối thứ tự merge EF migrations và model snapshot; chủ feature viết migration của mình, cập nhật từ `main` trước khi tạo/merge.
- Không đổi schema/DTO/enum dùng chung mà chưa thông báo các module phụ thuộc. Không commit credential, User Secrets, `node_modules` hoặc bundle build.
- Họp ngắn mỗi ngày: việc đã xong, việc tiếp theo, dependency đang chặn. Demo luồng tích hợp cuối mỗi tuần.

## Lộ trình gợi ý 4 tuần

Ước lượng để nhóm điều chỉnh theo deadline và năng lực thực tế:

| Giai đoạn | Mục tiêu |
|---|---|
| Tuần 1 | Setup, tách điểm sửa chung; hoàn thiện auth/profile, task/category, availability/events, session transitions |
| Tuần 2 | Hoàn thiện dependency/capture, planning/reschedule, learning/workload và kết nối UI–API |
| Tuần 3 | Notifications/admin, scenario; kiểm tra mobile, xử lý lỗi và các race/edge case trên SQL Server |
| Tuần 4 | Regression và visual QA, sửa lỗi, publish, tài liệu và rehearsal demo |

Luồng bàn giao chính: TV2 cung cấp task/dependency → TV3 lập lịch → TV4 ghi nhận thực hiện và phân tích → TV1 cung cấp thông báo/quản trị. TV1 cung cấp authentication và shared API client từ đầu.

## Điều kiện hoàn thành một task

UI + API + dữ liệu của chức năng hoạt động cùng nhau; có validation, loading/error/empty state; test phù hợp pass; mobile được kiểm tra trực tiếp; PR ghi rõ thay đổi, cách kiểm tra và ảnh nếu đổi giao diện; tài liệu và migration được cập nhật khi cần.

Demo cuối nhóm: đăng ký → xác minh → tạo công việc → thiết lập giờ rảnh → preview/apply kế hoạch → thực hiện phiên → phân tích workload → thử kịch bản → nhận thông báo.
