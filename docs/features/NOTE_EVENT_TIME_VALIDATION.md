# Kiểm tra giờ sự kiện từ Notes

Cập nhật 04/11/2026.

Sửa lỗi form đã có ngày giờ nhưng API vẫn báo “Bổ sung giờ bắt đầu và kết thúc”. Nguyên nhân là điều kiện thiếu trường và giờ bắt đầu đã qua dùng chung mã/thông báo. API giờ phân biệt:

- `NOTE_ACTIVITY_INCOMPLETE`: thiếu giờ bắt đầu hoặc kết thúc.
- `NOTE_ACTIVITY_TIME_INVALID`: giờ kết thúc không sau giờ bắt đầu.
- `EVENT_IN_PAST`: giờ bắt đầu đã qua hoặc trùng thời điểm hiện tại; yêu cầu chọn lại trước khi xem đề xuất.

Giao diện hiển thị lỗi dưới đúng ô và chặn xem đề xuất nếu giờ bắt đầu đã qua. Đánh giá lại mỗi 10 giây và khi quay lại cửa sổ, không tự đổi giờ người dùng đã nhập. Sửa nội dung review xóa thông báo lỗi cũ của lần preview/apply; vẫn kiểm tra điều kiện mới. Máy chủ tiếp tục kiểm tra tại lúc preview và xác nhận, không lưu sự kiện chỉ vì chỉnh form.

Các ô tên, loại và ngày giờ được định dạng nhất quán; số cột tự điều chỉnh theo độ rộng để không cắt chuỗi ngày giờ.

Kiểm chứng: 50 frontend tests (note-activities, notes, notes-autosave) và 33 backend tests (NoteActivities, NoteChecklist, CalendarPastEvent) đạt. Browser 1440/390px kiểm tra giờ đã qua, sửa giờ, xóa lỗi server cũ, preview lại, kiểu ô nhập, không tràn ngang hoặc lỗi JavaScript; dùng dữ liệu giả, không ghi lịch thật. Backend Release build 0 warnings/errors, schema khớp, local đã khởi động lại.

Log/ảnh: `artifacts/note-event-time-{frontend,backend,browser,build,restart}.log`, `artifacts/note-event-time-form-{1440,390}.png`.

## Giờ rảnh và danh sách SSG105

Bổ sung bản sửa 04/11/2026: Nhập việc nhận `availabilityPolicy.confirmed` làm nguồn giờ sinh hoạt đã xác nhận, ngoài weekly availability/exception. Không yêu cầu nhập lại giờ rảnh chỉ vì người dùng đang dùng chính sách sinh hoạt.

Hoạt động đủ thông tin được phép xem kiểm tra lịch ngay cả khi chưa có weekly availability. Sự kiện không yêu cầu giờ rảnh; công việc có giờ bắt đầu có thể xem phương án, nhưng ngoài giờ rảnh vẫn cần lựa chọn dùng giờ đã chọn cho lần này. API và planner tiếp tục chặn áp dụng phương án thiếu giờ/xung đột; xem preview chưa lưu giờ rảnh hoặc phiên.

Các mục thiếu thông tin hoặc đã qua ngày được liệt kê cạnh nút xem lịch. Bấm tên mục để mở và trỏ đến ô cần sửa. Người dùng có thể chọn **Chỉ giữ các mục hợp lệ đã chọn**: chỉ bỏ chọn mục lỗi, không đổi ngày, không tự chọn thêm mục trước đó đã bỏ, không xóa nội dung ghi chú. Xác nhận lưu vẫn là bước riêng.

Tình huống minh họa từ SSG105 được kiểm tra tại 07:00 ngày 04/11/2026: tiền quỹ thiếu thời lượng và đã qua ngày, chế biến 03/11 16:00–19:30 và đóng gói 19:30–20:00 đã qua; hai ca bán 04/11, mua quà 05/11 và sự kiện 06/11 có thể được giữ để kiểm tra riêng. Đây là fixture cấu trúc để kiểm tra UI, không phải kết quả gọi model thật. Không thay ngày của danh sách để làm cho lịch hợp lệ.

Kiểm chứng: unit backend NoteActivities (gồm confirmed routine không có weekly hours và one-off hours cần xác nhận), frontend ghi chú và browser 1440/390px. Các log `artifacts/note-availability-*` ghi kết quả từng lượt; ba test Notes ở lượt đầu có hai timeout và một lỗi appearance, chạy lại riêng đều qua. Không coi lượt đầu là toàn bộ frontend đã xanh.
