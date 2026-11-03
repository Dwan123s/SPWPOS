# Nhận diện đúng đối tượng dời lịch và giữ gói việc chờ deadline

## Hành vi sửa

- “Dời phiên làm việc 12:45–14:05 để kiểm tra lúc 14:00” chọn phiên theo giờ địa phương; giữ giờ kiểm tra, thời lượng và deadline. Các cách nói chuyển phiên/lùi buổi làm việc và giờ dạng `12h45 đến 14h05` được hỗ trợ. Khi lặp cùng khung giờ ở nhiều ngày, cần ngày rõ ràng hoặc sự kiện xung đột xác định duy nhất.
- Mục đích của hành động không trở thành đối tượng cần dời. Bản nháp đổi giờ hẹn trái giờ đã nói bị từ chối và trả phản hồi cho agent. Agent không được hỏi dời cuộc hẹn thay cho thử phương án dời phiên đã được cho phép.
- Quyền dời một phiên không mở rộng thành xếp lại toàn bộ lịch. Các phiên khác, kể cả phiên khác của cùng task, được giữ. Bản sao phục vụ planner bảo vệ các phiên này; không thay đổi thiết lập khóa thật.
- Runtime nhận đủ giờ sự kiện và quyền dời phiên có thể dùng `propose_changes` trực tiếp. `get_calendar` bổ sung tên và thời lượng/deadline của task tương ứng để tránh thêm lượt đọc chỉ để tra tên. Model vẫn là Gemini 3.8 Flash.
- Tài liệu SSG105 mẫu có ngày 04/11/2026 nhưng chưa chốt giờ nộp. NORA giữ các gói đầy đủ để xem, hỏi giờ chung một lần, rồi mới tạo bản nháp có thể giao việc. Không tự chọn 23:59 hoặc loại bỏ tất cả gói vì thiếu một phần deadline.

## Kiểm chứng

- Lượt kiểm thử backend cuối: **34 passed**, một live test opt-in được bỏ qua ở lượt thường. Kiểm tra quyền, phủ định riêng đối với lịch kiểm tra, phạm vi phiên (kể cả phiên khác cùng task), deadline và luồng runtime. Bộ kiểm tra rộng trước đó phát hiện việc model gửi thừa task ID; đã sửa để tham chiếu giờ thu hẹp đúng phạm vi và kiểm tra lại.
- Frontend nhóm/người nhận: 19 kiểm thử qua, gồm giữ gói chờ hạn, bổ sung hạn và chỉ hiện xác nhận giao việc sau khi dữ liệu đầy đủ.
- Hai thử nghiệm thật với Gemini 3.8 Flash qua trên SQLite bộ nhớ: giữ bài kiểm tra 14:00–15:00, giữ đủ 80 phút công việc và phiên khác; đọc file Word SSG105, giữ 2 gói chờ giờ rồi trả 2 gói hợp lệ sau khi xác nhận hạn chung 21:00 ngày 04/11/2026. Không ghi/giao task thật. Các giờ trong fixture chỉ là dữ liệu thử, không đặt lại deadline của người dùng.
- Browser bundle mới ở 1440, 390, 320 px: gói chờ hạn vẫn hiển thị đủ nội dung, chưa có nút giao việc; sau bổ sung có thể xác nhận; không lỗi trang hoặc tràn ngang. Ảnh `artifacts/team-brief-pending-*.png`.

Không cam kết hiểu chính xác mọi câu ngôn ngữ tự nhiên. Trường hợp thiếu thông tin hoặc nhiều đối tượng trùng nhau vẫn hỏi rõ; không đoán và thay lịch khác để tạo ra phương án có vẻ khả thi. Không đổi dữ liệu Notes, không cần migration mới.

API Release build thành công, 0 warnings/0 errors. Frontend production build đã cập nhật tại local. Chưa push các thay đổi đợt này lên Git.
