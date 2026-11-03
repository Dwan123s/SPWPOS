# NORA: phân biệt sự kiện cố định và phiên công việc

Trong ô chat NORA, **Loại lịch** có ba lựa chọn:

- **NORA tự nhận diện** (mặc định): người dùng chat bình thường; yêu cầu có thể gồm cả sự kiện và công việc.
- **Sự kiện cố định**: lịch học, họp, cuộc hẹn, có ngày và giờ bắt đầu/kết thúc. Lưu dưới dạng FixedEvent; không tạo task, không đưa vào Eisenhower. Không cần giờ rảnh làm việc để lưu một cuộc hẹn đầy đủ thông tin. Vẫn kiểm tra xung đột và dùng quy trình xác nhận dời lịch hiện có khi cần.
- **Phiên công việc**: tự học, làm báo cáo, hoàn thành bài tập. Tạo/cập nhật task rồi planner tìm WorkSession theo thời lượng, deadline và work window nếu có. Một khung giờ yêu cầu cho việc cá nhân không tự biến thành sự kiện để bỏ qua kiểm tra giờ rảnh. Phiên có thể cố định giờ theo yêu cầu; vẫn là phiên của task.

Người dùng không phải trả lời thêm mục tiêu cá nhân chỉ để phân loại mức quan trọng. Nếu chưa đủ căn cứ, mức quan trọng giữ Unknown; có thể cấu hình trong công việc/ma trận hiện có. Thiếu thời lượng vẫn dùng đề xuất ước lượng để xác nhận; thiếu thời điểm bắt buộc vẫn hỏi để lưu đúng. Loại lịch không tạo sẵn deadline hoặc thời lượng.

Ảnh thời khóa biểu chọn loại sự kiện để giữ lịch bận. Đổi loại không tự gọi AI, không đổi các mục lịch đã lưu; cần gửi yêu cầu mới. Lựa chọn reset khi chuyển cuộc trò chuyện. Kết quả có nhãn sự kiện/công việc/phiên; bảng lịch dùng nhãn Sự kiện cố định và Phiên công việc.

## Backend

`AgentTurnInput.CalendarKind`: `Auto` (default, tương thích client cũ), `Event`, `WorkSession`. Chọn loại nằm trong fingerprint idempotency, không được tái dùng request ID của loại khác. Giữ nguyên nội dung chat gốc trong AgentTurn; đưa lựa chọn thành context có kiểm soát cho model.

Luồng Workflow và Agent native đều kiểm tra lựa chọn: Event không chấp nhận lệnh task; WorkSession không chấp nhận lệnh event. Không chuyển đổi cấu trúc bằng suy đoán. Sai loại → không ghi lịch, yêu cầu làm rõ hoặc model sửa lại draft qua tool error. Các task hiện có có thể được planner dịch chuyển khi thêm sự kiện chỉ theo các quy tắc xung đột/ủy quyền hiện hữu. Auto tiếp tục cho phép yêu cầu hỗn hợp.

Không đổi model (`gemini-3.8-flash`), schema database, dữ liệu lịch cũ, Notes hay cấu hình thanh toán.

## Kiểm chứng 03/11/2026

- 194 unit tests liên quan PlanningAgent/AgentRuntime/AgentInterpreter qua, gồm sáu trường hợp mới về chọn loại, không ghi sai loại, idempotency, giữ chat gốc, sự kiện không cần availability, undo, chế độ Auto hỗn hợp và validation.
- 2 test bổ sung native tool runtime: chặn draft sai loại, báo CLARIFICATION_REQUIRED, không ghi event/task/session.
- 1 API integration test: bind CalendarKind, guard sai loại, lưu đúng Event, validation 400.
- 56 frontend tests về chat NORA, lịch/kết quả và sửa sự kiện qua.
- Browser 1440/390/320 px: chọn Event/WorkSession, gửi giá trị riêng cùng chat gốc, nhãn kết quả khác nhau, không tràn ngang hoặc lỗi JavaScript. `artifacts/calendar-kind-*.log/png`.
