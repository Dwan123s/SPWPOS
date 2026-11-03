# Hỏi kết quả khi hết giờ và bổ sung thời gian

Khi phiên hết giờ nhưng công việc chưa được xác nhận hoàn thành, hệ thống gửi một thông báo hỏi kết quả trong ứng dụng và xếp hàng Telegram nếu người dùng đã kết nối/bật kênh đó. Thông báo dẫn tới Hôm nay. Không tự hoàn thành task hoặc tự kéo dài deadline.

Trang Hôm nay có hai lựa chọn:

- **Đã xong công việc → Xác nhận hoàn thành**: kết thúc bộ đếm đang chạy, ghi nhận thời gian thực làm, giải phóng phần còn lại của phiên hiện tại và hủy các phiên Scheduled còn lại của cùng task. Không đổi lịch của task khác. Với task nhóm, việc cá nhân được giải phóng nhưng báo cáo vẫn chuyển PendingReview để leader duyệt; không tự thông báo hoàn thành chung.
- **Cần thêm thời gian**: chọn 15/30/60 phút hoặc nhập 5–480 phút, bấm NORA đề xuất thêm giờ, kiểm tra ngày/giờ rồi xác nhận. Có thể chia nhiều phiên nếu task cho phép và bị giới hạn độ dài phiên. NORA giữ các phiên đã có, lịch cố định, nghỉ/sinh hoạt, phụ thuộc, giới hạn mỗi ngày, khung thực hiện và deadline. Không đủ giờ thì báo chưa thể thêm; không lưu một phần hoặc dời deadline để làm cho phương án khả thi.

Nút hoàn thành sớm cũng có ở thẻ phiên đang diễn ra trên Hôm nay. Hoàn thành một phiên bằng timer không tự đồng nghĩa hoàn thành toàn bộ task.

## Tính thời gian và lưu dữ liệu

Worker xét mốc bắt đầu/kết thúc khoảng mỗi 10 giây; trình duyệt cập nhật phần phản hồi mỗi 10 giây và khi lấy lại focus. Không gửi liên tiếp cho cùng mốc kết thúc. Nếu timer còn Running ở lúc hết giờ, đoạn đo được chốt tại giờ kết thúc và chuyển Paused, không tự tính thời gian chờ phản hồi thành thời gian làm việc.

Đề xuất bổ sung được lưu riêng bằng `PrivatePlanningProposal.Kind=SESSION_EXTENSION`, tối đa 10 phút, chưa tạo WorkSession. Khi xác nhận, kiểm tra quyền sở hữu/tình trạng task nhóm, revision lịch, phiên bản task, thời gian đề xuất vẫn chưa qua và toàn bộ ràng buộc lịch. Retry cùng đề xuất đã áp dụng không thêm phiên hoặc XP lần nữa.

Ước lượng ban đầu `EstimatedMinutes` được giữ nguyên cho so sánh/học từ thời gian thực tế. Phần việc được xác nhận cần thêm được lưu trong proposal đã áp dụng cùng tiến độ và các phiên đã hoàn thành tại thời điểm đó. State tính quỹ còn lại từ bản xác nhận mới nhất, trừ thời gian thực làm của các phiên hoàn thành sau đó; không áp dụng thêm hệ số AI lên số phút người dùng vừa xác nhận. Phần này tiếp tục được tính khi xếp lại lịch/Eisenhower. Khi hoàn thành/hủy task, quỹ này đóng lại. Không thêm bảng/cột hoặc migration; `ConfirmedRemainingMinutes` là thuộc tính tính tại runtime, không ánh xạ EF.

Phản hồi đã xử lý được đánh dấu đọc, hủy các lời nhắc Telegram còn chờ cho phiên đó. Telegram hiện gửi câu hỏi/hướng dẫn; người dùng phản hồi và xác nhận trong PLANORA, chưa phải trả lời bằng tin nhắn trực tiếp cho bot.

## API

- `GET /api/sessions/time-review`: phiên đã hết giờ cần phản hồi của chính tài khoản.
- `POST /api/sessions/{id}/extra-time/preview`: `{ minutes }`, trả tính khả thi, phiên đề xuất, ID và revision.
- `POST /api/sessions/extra-time/{id}/confirm`: `{ revision }`, xác nhận phiên bổ sung.
- `POST /api/sessions/{id}/finish-task`: xác nhận hoàn thành toàn bộ công việc tương ứng và giải phóng các phiên còn lại.

Việc tìm slot/kiểm tra ràng buộc do planner thực hiện, không cần gọi model để thêm một vòng diễn giải. Cấu hình Gemini 3.8 Flash giữ nguyên cho các tác vụ AI khác. Không thay Notes.

## Kiểm chứng 02/11/2026

- Bộ unit test backend: **1.047 passed, 9 live/opt-in skipped**. Kiểm tra hết giờ chỉ pause timer tại đúng mốc, không hoàn thành task; dedupe thông báo/Telegram; thêm giờ cần xác nhận, retry không nhân phiên; giữ ước lượng gốc/deadline/phiên khác; stale/timeslot đã qua; task nhóm vẫn PendingReview; actual/XP/learning và giải phóng lịch sớm.
- API `WorkTimeApiTests`: luồng HTTP preview → confirm → retry → hoàn thành task → không còn phiên tương lai đã qua; chưa đăng nhập nhận 401.
- Frontend tập trung: **22 passed** ở các file work-time-review, scheduled-focus, notifications và notification-popups. Chọn phút không tự lưu, thay phút bỏ proposal cũ, thiếu giờ không có nút confirm, hoàn thành task cần xác nhận.
- Lượt frontend toàn bộ đã dừng trước khi hoàn tất, có hai lỗi workflows trong lượt chạy rộng. Chạy riêng lại hai tình huống Notes/mật khẩu: **2 passed**; không coi đây là kết quả toàn bộ frontend đã qua. Source Notes không sửa.
- Browser ở 1440, 390, 320 px: thông báo hỏi kết quả, preview 30 phút, xác nhận và refresh hoạt động; không tràn ngang/lỗi trang; chỉ có request ghi khi người dùng thao tác. Ảnh `artifacts/work-time-review-*.png`.
- API đã rebuild thành công (0 warnings/0 errors) vào runtime riêng, schema khớp và không thay đổi database. HTTP local `/app/` 200; Swagger có đủ bốn route time-review/extra-time preview/confirm/finish-task. Model vẫn VertexAI / gemini-3.8-flash.
