# Eisenhower — hồ sơ triển khai và kiểm chứng local

Ngày bàn giao: **02/11/2026**, `Asia/Ho_Chi_Minh`.

Đã triển khai dữ liệu mức quan trọng, bộ tính độ gấp, API cá nhân, ma trận và gợi ý Hôm nay, tích hợp bản nháp NORA/file nhóm, thử chính sách xếp lịch có fallback và notification/Telegram theo chuyển trạng thái. Hướng dẫn: [EISENHOWER.md](../features/EISENHOWER.md). Checklist và các điều chỉnh thiết kế: [plan thực thi](../plans/PLANORA_EISENHOWER_IMPLEMENTATION.md).

## Kết quả kiểm thử

| Kiểm tra | Kết quả |
| --- | --- |
| Bộ unit backend đầy đủ tại mốc tích hợp | **984 đạt**, 6 kiểm thử dịch vụ thật/opt-in bỏ qua. |
| Kiểm tra backend sau các chỉnh sửa cuối: PlanningAgentTests, EisenhowerTests, PrivateTeamPlanningTests, UrgentPlanningTests | **219 đạt**. Có ca chat chỉ sửa Importance không replan và cảnh báo Telegram được đưa vào hàng đợi đúng binding. Đây là tập kiểm tra lại, không cộng vào số unit đầy đủ để đếm thành tổng mới. |
| Toàn bộ frontend | **486 đạt / 62 file**. |
| Frontend sau chỉnh sửa cuối về tìm kiếm/cache tài khoản | **41 đạt**; nằm trong tập kiểm tra lại. |
| API Eisenhower, API brief/recipient và SQL xác nhận batch đồng thời | **5 đạt**; SQL dùng database kiểm thử tạm. |
| Gemini 3.8 Flash thật, PDF giả lập, không lưu task | **1 đạt**; xác nhận schema mới được provider chấp nhận. |
| Build API Release | Thành công, không warning/error. |
| Build frontend | Thành công. |
| `git diff --check` | Đạt; Git chỉ nhắc quy đổi LF/CRLF. |

Các kiểm thử xác nhận quyền chủ sở hữu/leader, ngăn xem lịch member qua API nhóm, Importance chung và override riêng, task chỉ định chưa nhận, chống đếm đôi, stale/version, nghĩa vụ cạnh tranh, phụ thuộc, phiên khóa và deadline.

## Browser và tải giao diện

Đã dùng dữ liệu giả lập **100 task** ở **1440, 1024, 390, 320 px**. Kiểm tra:

- Tối đa bốn task/ô, tìm kiếm và phân trang; mobile dùng tab.
- Mở popup cá nhân và phần giải thích; từ ma trận mở đúng task nhóm.
- Task giao đích danh vẫn cần người được giao nhận/xác nhận lịch; không tự gọi preview/claim khi xem.
- Tối đa ba gợi ý Hôm nay.
- Không tràn ngang, không page error và không có request thay đổi dữ liệu từ thao tác chỉ xem.

Ảnh kiểm chứng nằm trong `artifacts/eisenhower-matrix-1440.png`, `artifacts/eisenhower-matrix-390.png` và `artifacts/eisenhower-focus-*.png`. Đây là ảnh kiểm thử giả lập, không đưa dữ liệu cá nhân vào repository.

## Đo phản hồi

API trên SQLite kiểm thử với 100 task, deadline trong bảy ngày, mười lượt cache ấm:

| Lần đo | Cold | Warm p95 |
| --- | ---: | ---: |
| Lần đầu | 60,5 ms | 11,5 ms |
| Lần kiểm tra cuối | 68,0 ms | 12,6 ms |

Máy Windows local, .NET 8.0.425. Số đo này không bao gồm AI hoặc độ trễ mạng ngoài máy và không phải cam kết production. Test chứng minh GET không tạo AgentTurn, phiên lịch hoặc trạng thái cảnh báo. Không thêm lần gọi AI để mở/tính lại ma trận.

## Database và ứng dụng local

- Migration: **`20261101182927_EisenhowerPrioritization`**.
- Backup SQL `COPY_ONLY`, `CHECKSUM` và `RESTORE VERIFYONLY` thành công: `/var/opt/mssql/data/SPWPOS_before_eisenhower_20261101184657.bak`.
- Số bản ghi task, session, fixed event, Notes, hội thoại NORA, task/chat nhóm, membership và tài khoản giữ nguyên khi nâng cấp.
- Kiểm tra schema sau migration khớp bộ migration hiện tại.
- API Release đã khởi động tại `https://localhost:7043`, PID tại thời điểm bàn giao **13684**; startup xác nhận **VertexAI / gemini-3.8-flash** sẵn sàng.
- Sau restart đã kiểm tra HTTP: entry mới **`index-BjojrydQ.js`**, bundle chứa UI Eisenhower, `GET /api/eisenhower` không đăng nhập trả **401**.
- Cấu hình `Eisenhower.Enabled`, `SchedulingEnabled`, `NotificationsEnabled` đang bật; có thể tắt riêng hành vi mới khi cần rollback, giữ dữ liệu bổ sung.

Lần xác minh HTTP đầu bị cơ chế xét duyệt công cụ chặn do hạn mức xét duyệt, nên chưa được thực thi. Sau khi người dùng yêu cầu tiếp tục, chạy lại đúng bước qua cơ chế xét duyệt đã thành công; không còn bước xác minh này bị chặn.

## Giới hạn và điều không thay đổi

Task cũ chưa được AI/người dùng xác định ảnh hưởng vẫn là **Chưa xác định**; không gọi AI hàng loạt để backfill. Deadline ngoài horizon 30 ngày cần đánh giá sau. Tài liệu nguồn, ghi chú và báo cáo giữ quyền truy cập hiện có. Source Notes không bị sửa. Không tự xóa Q4, chuyển trách nhiệm giữa member hoặc đổi deadline. Chức năng đã có trên local; chưa push các thay đổi đợt này lên Git.

## Bổ sung: NORA đề xuất, người dùng xác nhận/chỉnh sửa

Mở popup task `Unknown` tự yêu cầu một đề xuất. Đề xuất AI có sẵn trong task chưa xác nhận được hiển thị để duyệt; task đã xác nhận chỉ gọi lại AI khi người dùng yêu cầu. Nút **Đồng ý đề xuất** hoặc form chỉnh mức/lý do mới lưu qua API riêng. Member không có quyền đề xuất/xác nhận đánh giá chung thay leader. Không thay đổi lịch.

- Backend liên quan Importance/Eisenhower: **21 passed**, một live test opt-in được bỏ qua ở lượt thường. Live Gemini **3.8 Flash** với task giả lập đã chạy thành công riêng, trả `Important` và trích dẫn có trong đầu vào; không lưu task người dùng.
- Kiểm thử service mới: đề xuất chưa lưu, confidence thấp vẫn chờ xác nhận, cache theo version, xác nhận giá trị đã chỉnh, quyền owner/leader, task/quyền thay đổi trong lúc AI chạy, từ chối trích dẫn bịa.
- API ImportanceProposal/Eisenhower: **3 passed**, xác nhận payload/version, cache, không ghi task trước xác nhận, stale sau xác nhận, HTTP 401 khi chưa đăng nhập.
- Frontend Eisenhower/task board: **23 passed**, gồm auto đề xuất, chấp nhận/chỉnh sửa, lỗi/retry, giữ đánh giá đã xác nhận, không gọi cho member, bỏ phản hồi task cũ, gộp StrictMode và giữ nội dung người dùng đang nhập.
- Browser bundle cuối: **1440, 390, 320 px**, tự đề xuất rồi chỉnh và lưu đúng version; không page error/tràn ngang/request đổi lịch. Ảnh giả lập `artifacts/importance-proposal-*.png` đã xem trực tiếp.
- API Debug build **0 warnings, 0 errors**; schema khớp, không cần migration mới. Local PID **13176**, startup xác nhận VertexAI / `gemini-3.8-flash` sẵn sàng.
- HTTP local: `/app/` và bundle mới **`index-Ymqe0beg.js`** đều 200; có UI “Đồng ý đề xuất”. POST proposal khi chưa đăng nhập trả **401**.

Các số kiểm thử ở đây là lượt kiểm tra tập trung cho thay đổi bổ sung; bộ kiểm thử đầy đủ phía trên thuộc lần bàn giao tính năng Eisenhower ban đầu. Các thay đổi đợt này chưa push Git.
