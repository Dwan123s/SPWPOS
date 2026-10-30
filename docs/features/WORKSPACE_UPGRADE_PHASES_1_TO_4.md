# PLANORA: nâng cấp kiến trúc, lập lịch và ghi nhận công việc

Cập nhật 28/10/2026. Tài liệu này thay thế mô tả thuật toán/concurrency cũ trong các báo cáo lịch sử trước ngày này. Không thay đổi namespace hay đường dẫn của các API hiện có.

## Kiến trúc và ghi đồng thời

`WorkspaceEntities.cs` được tách thành từng entity; enum trạng thái nằm cùng `WorkTask.cs` và `WorkSession.cs`. Các controller chuyển vào `Controllers/Workspace`, giữ namespace, route và phân quyền.

Workspace mutation dùng transaction ReadCommitted và optimistic concurrency trên `WorkPreference.MutationVersion`, `Revision` cùng `WorkTask.Version` đã có. Một cập nhật có điều kiện vào version được thực hiện trước các dòng execution/repair phụ thuộc. Nếu phiên bản đã thay đổi, transaction rollback và API trả `409 CONCURRENCY_CONFLICT`. Client tải lại dữ liệu trước khi thử lại; không tự phát lại mù mọi thao tác.

`Revision` của lịch chỉ đổi khi cần làm mới đề xuất. `MutationVersion` còn bảo vệ những thao tác không làm cũ đề xuất, như đọc thông báo/chọn skin. Replay đã được ghi nhận không cộng XP hoặc tạo execution/repair lần nữa. Database vẫn có transaction và khóa ghi thông thường; OCC không có nghĩa là hoàn toàn không khóa database.

Không dùng `sp_getapplock` cho mutation workspace thông thường. Authentication/refresh/AI quota và thao tác khóa tài khoản vẫn giữ serialization riêng cần thiết để bảo vệ tài khoản. Không giữ transaction workspace trong lúc gọi model AI.

## Thuật toán có hàm mục tiêu

Sau thứ tự phụ thuộc/độ khẩn/deadline của task, planner so sánh các cửa sổ ứng viên:

`Penalty = 10000 * Lateness + 2 * Idle + 1000 * Fragmentation - 500 * Preferred`.

| Thành phần | Định nghĩa |
| --- | --- |
| Lateness | Số phút vượt hạn, cộng chi phí nghịch đảo độ dư trước hạn để tránh kết thúc sát hạn |
| Idle | Đuôi cửa sổ ngắn hơn phiên tối thiểu, cộng độ trễ bắt đầu tính theo ngày |
| Fragmentation | Tỷ lệ công việc còn lại sau phiên ứng viên |
| Preferred | 1 khi toàn phiên nằm trong giờ ưa thích theo múi giờ, ngược lại 0 |

Điểm nhỏ hơn được ưu tiên; đồng điểm chọn thời điểm sớm hơn. Hệ số là chính sách sản phẩm có thể hiệu chỉnh, không phải hệ số được suy ra từ nghiên cứu tâm lý học. Đây là heuristic nhiều mục tiêu có ràng buộc, chưa phải bằng chứng tối ưu toàn cục. Không cam kết mọi dữ liệu đều chạy dưới 100 ms.

Hard deadline, dependency, work window, availability, break, phiên khóa/đang thực hiện và giới hạn ngày vẫn được kiểm tra độc lập. Quy tắc khẩn cấp có ưu tiên cao hơn điểm mềm: việc khẩn chiếm khung cần thiết; việc bị ảnh hưởng gần hạn dời sát lịch cũ; việc còn hạn xa ưu tiên ngày kế tiếp trong giới hạn hai ngày. Điểm mềm không được làm lệch quy tắc này.

## EMA và cơ sở lý thuyết

Với các quan sát theo thứ tự thời gian, `r = ActualMinutes / EstimatedMinutes` và:

`bias_new = clamp(0.2 * r + 0.8 * bias_previous, 0.5, 3)`.

Khởi tạo bằng mẫu hợp lệ đầu tiên, giới hạn 0.5–3. Chưa đủ ba mẫu thì bias = 1; mỗi bước cập nhật giới hạn chính bias và kết quả cuối làm tròn ba chữ số. Dữ liệu workspace giới hạn 500 quan sát mới nhất để giới hạn tài nguyên; không còn trung bình thô của 50 mẫu. Hiệu chỉnh actual giữ nguyên ngày và estimate gốc của quan sát. Sai số ước lượng theo loại task cũng dùng EMA.

Capacity dùng EMA của tỷ lệ phút kế hoạch đã hoàn thành trên phút kế hoạch đã có kết quả, theo từng ngày địa phương. Chưa đủ năm phiên: 0.8; sau đó giới hạn 0.25–1. `Scheduled`/`NeedsReview` chưa xác nhận không bị coi là thất bại. Actual vượt giờ không làm tăng capacity. Snapshot tuần dùng cùng hàm capacity trên các phiên của tuần. Đây là thước đo lịch đã xác nhận ở cấp phiên, không chứng minh năng suất hay mức tập trung của người dùng.

Nền tảng tham khảo:

- Kahneman & Tversky, *Intuitive Prediction: Biases and Corrective Procedures*: [hồ sơ báo cáo gốc NTIS](https://ntrl.ntis.gov/NTRL/dashboard/searchResults/titleDetail/ADA047747.xhtml), [chương sách Cambridge](https://www.cambridge.org/core/books/abs/judgment-under-uncertainty/intuitive-prediction-biases-and-corrective-procedures/3A2F2ECDC508C662BE17C0FE5FD73C90). Cơ sở để đối chiếu ước lượng với kinh nghiệm thực tế và nhận biết planning fallacy.
- Flyvbjerg, *Quality control and due diligence in project management*: [bản tác giả](https://arxiv.org/abs/1302.2544). Tham khảo hướng outside view/reference class forecasting.

EMA là lựa chọn kỹ thuật để ưu tiên kinh nghiệm mới, không phải chính công thức của Kahneman/Tversky hay một mô hình Reference Class Forecasting đã được hiệu chuẩn. Alpha, ngưỡng mẫu, cold start và cận an toàn cần được đánh giá bằng dữ liệu thực tế sau này.

## XP và quyền riêng tư

- `GET /api/workspace/gamification/profile`: XP, level, ngưỡng cấp tiếp theo, skin đang chọn và inventory.
- `POST /api/workspace/gamification/skin` với `{ "code": "forest" }`: chỉ trang phục đã mở khóa.
- Hoàn thành phiên có actual dương, đúng thời điểm kết thúc phiên và deadline: `clamp(10 + floor(plannedMinutes / 15), 10, 50)` XP. Cờ `ExperienceAwarded` cùng transaction ngăn cộng lặp.
- Level = `1 + floor(XP / 100)`. Classic: 0 XP; forest: 100; sunset: 300; cosmic: 600. Skin là biến thể màu của mascot hiện có, không mua bằng tiền.
- Ghi nhận bù hoàn thành được hiểu là người dùng xác nhận đã làm phiên đúng lịch; XP dựa trên xác nhận này, không phải dữ liệu timer được quan sát tự động. Phiên chỉ hoàn thành một phần không được cộng XP hoàn thành.
- `POST /api/commerce/payments` trả `410 REAL_PAYMENTS_DISABLED`. Chưa có luồng tạo checkout PayOS thật trước thay đổi; helper kiểm chữ ký offline được giữ nhưng không nối gateway.

Admin chỉ xem tổng hợp (`GET /api/admin/metrics`, `/health`) và thông tin tài khoản cần thiết cho khóa/mở khóa/audit đăng nhập. Response danh sách user bỏ hoàn toàn `totalTasks`/`completedTasks`; job log không trả UserId. Không có endpoint admin đọc nội dung task, note hoặc calendar của người khác. Role Admin không bỏ qua bộ lọc sở hữu ở API workspace.

Metrics gồm tổng user, đăng nhập trong ngày UTC, phiên đã hoàn thành, token provider thực sự báo cáo, job lỗi và tài nguyên tiến trình. Token chưa có dữ liệu là `null`. Health kiểm tra database; AI latency và SMTP là bằng chứng thành công gần nhất, được ghi rõ không phải probe hiện tại. Không tự gửi email hay gọi model tính phí để mở trang Admin. RAM/CPU tiến trình không phải RAM/CPU của máy chủ SQL.

## Ghi nhận bù và tự vá lịch

`GET /api/sessions/needs-review` trả tối đa 20 phiên gần nhất đã qua, chưa có execution, thuộc task còn hoạt động. Query đọc không ghi. Worker chuyển phiên Scheduled đã qua sang `NeedsReview`, không tự kết luận Missed.

Dashboard hiển thị một card xác nhận. `POST /api/sessions/{id}/catch-up` nhận:

```json
{ "outcome": "COMPLETED" }
```

Hoặc `PARTIAL` với `progress` (tiến độ tuyệt đối của task, 1–99%, không giảm) và `actualMinutes` (1–10080); hoặc `MISSED`. Hoàn thành ghi actual bằng thời lượng kế hoạch; một phần ghi thời lượng tự báo cáo. Execution được đánh dấu manual, không tạo focus segment giả, không cần bật timer. Kết quả một phần đóng phiên, giữ task đang thực hiện và tìm phiên bù.

Missed/partial tìm khoảng rảnh sớm nhất từ hiện tại + 5 phút đến tối đa 48 giờ. Phiên bù dùng thời lượng còn thiếu của phiên, trừ phần đã có lịch tương lai; tuân thủ phiên tối thiểu/tối đa, break, giờ rảnh, sự kiện, work window, dependency, giới hạn ngày và deadline cứng. Không dời các cam kết khác hoặc gia hạn deadline. Khi không có phương án hợp lệ, trả thông báo rõ ràng; không tự phá ràng buộc để tạo lịch. Unique repair-source ngăn tạo hai phiên bù cho cùng một phiên gốc.

## Xuất lịch

`GET /api/workspace/calendar/export.ics` yêu cầu đăng nhập, trả `text/calendar; charset=utf-8`, chứa event và session của chính người dùng từ 30 ngày trước đến 366 ngày sau. Loại bỏ session hủy/bỏ qua và task đã xóa.

Tuân theo [RFC 5545](https://www.rfc-editor.org/rfc/rfc5545.html): UTC, UID ổn định, CRLF, escape TEXT và gấp dòng tối đa 75 octet UTF-8, không cắt rune. Đây là xuất một chiều để import; chưa đồng bộ hai chiều, chưa đăng ký push notification.

## Migration và triển khai

Migration `20261028034209_WorkspaceUpgradePhases` thêm XP/level/skin, MutationVersion, cờ XP và quan hệ repair-source. User hiện có bắt đầu level 1, classic, 0 XP. Không tự áp dụng migration lên database đang chạy trong đợt chỉnh sửa này. Backup và xem SQL migration trước khi cập nhật database thật; migration Down xóa dữ liệu XP/repair vừa thêm nên chỉ dùng khi đã đánh giá mất dữ liệu.

Xem [triển khai chung tên miền, build riêng](../development/INDEPENDENT_BUILD_SAME_ORIGIN.md). Theo lựa chọn của người dùng, không bật cookie credentials qua hai origin, không đổi CORS/CSRF/cookie hiện tại.

Kết quả build, unit/integration/frontend và giới hạn kiểm chứng được ghi trong [báo cáo kiểm chứng 28/10/2026](../development/WORKSPACE_UPGRADE_VERIFICATION_2026-10-28.md).
