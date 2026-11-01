# Đề xuất theo tuần thời khóa biểu

Sau khi thời khóa biểu được NORA phân tích và lưu/xác nhận, thẻ đề xuất tự mở trong hội thoại. Thẻ gắn với mã lượt nhập thời khóa biểu của chính tài khoản, tự làm mới khi có lần nhập mới. Người dùng xem đề xuất rồi xác nhận áp dụng.

Phạm vi là thứ Hai đến Chủ nhật của tuần có ngày đầu tiên trong thời khóa biểu vừa nhập, theo múi giờ tài khoản. Thời khóa biểu tuần sau tạo đề xuất tuần sau. Khi nhập lịch có nhiều tuần, thẻ bắt đầu ở tuần đầu tiên của lịch đó. Ngày đã qua vẫn có trong bảng tuần nhưng không trở thành giờ rảnh có thể xếp việc.

Backend trả `weekStart`, `weekEnd` và đúng 7 ngày. Mỗi ngày trừ lịch cố định, di chuyển, ngủ, ăn, nghỉ và các hoạt động riêng đã có. Phương án xếp phiên chỉ nằm trong phạm vi tuần; các phiên ngoài tuần được giữ. Các ngày sinh hoạt đã xác nhận ở tuần khác được giữ lại; không tự mở giờ rảnh sang tuần chưa xác nhận.

`POST /api/time/policy/preview?timetableTurnId=<id>` và endpoint `draft` dùng cùng mã lượt nhập. Backend xác minh chủ tài khoản và trạng thái đã áp dụng, đọc ngày từ các sự kiện thực tế còn tồn tại. Không nhận ngày tự khai từ client. Nếu không có mã lượt nhập, dùng lần nhập thời khóa biểu gần nhất; dữ liệu cũ không có lượt nhập dùng sự kiện sắp tới gần nhất. Tuần đã hết hạn yêu cầu gửi thời khóa biểu của tuần cần sắp xếp.

Không cần migration: phạm vi ngày lưu trong JSON ngày sinh hoạt và payload đề xuất hiện có. Chính sách cũ không có ngày cụ thể giữ cách hoạt động cũ.
