# Lời nhắc cá nhân trong NORA

NORA có công cụ `manage_reminder` để tạo, liệt kê và hủy lời nhắc độc lập với
công việc, thời lượng thực hiện và lịch bận. Ví dụ: “1 phút nữa nhắc tôi trả lời
email khách hàng” hoặc “Ngày mai lúc 9 giờ nhắc tôi gọi khách”. Khi thiếu nội dung
hoặc thời gian, NORA hỏi lại; không tự tạo deadline hoặc phiên làm việc.

- Thời gian tương đối được neo vào lúc nhận yêu cầu, không tính lại từ lúc AI
  phản hồi hay lúc người dùng tải trang. Giới hạn là 1 giây đến 30 ngày.
- Dùng cùng RequestId không tạo trùng. Chế độ chỉ xem trước không lưu lời nhắc
  đến khi người dùng áp dụng. Tạo/hủy hỗ trợ hoàn tác khi lời nhắc chưa bị thay đổi.
- NORA có nút **Lời nhắc** để xem nội dung, giờ nhắc và hủy. Lời nhắc đã đến giờ
  chưa được xác nhận vẫn hiện khi quay lại web.
- Khi đến giờ, popup cố định nhấp nháy nhẹ và chuông lặp đến khi người dùng bấm
  **Tắt chuông**. Chuyển trang hoặc bấm ngoài không tự đóng. Hỗ trợ giảm chuyển
  động; tùy chọn tắt âm thanh của tài khoản vẫn được tôn trọng.
- Tắt chuông dừng âm thanh ngay, lưu xác nhận tại trình duyệt và đánh dấu thông
  báo đã đọc trên server. Nếu mạng lỗi, lần tải sau không phát lại chuông ở trình
  duyệt đó; xác nhận server được thử lại. Các tab cùng tài khoản nhận xác nhận
  qua storage event.
- Thông báo được lưu bền vững trong cơ sở dữ liệu; không phụ thuộc bộ đếm của tab.
  Đồng hồ ở App chỉ giúp làm mới đúng hạn. Nếu mạng tạm mất sau khi đã tải lời
  nhắc, đồng hồ vẫn có thể hiện popup từ giờ đã biết.

## Các kênh thông báo

- **Web:** popup NORA kèm chuông và trung tâm Thông báo, theo tùy chọn trong ứng
  dụng. Mỗi lời nhắc chỉ có một popup báo giờ, không tạo thêm toast trùng.
- **Email:** gửi một lần khi đến giờ nếu người dùng đã bật email. Không gửi sớm.
  Lỗi mạng gửi lại theo backoff, không chặn Telegram.
- **Telegram:** gửi một lần qua kết nối đã bật, gắn đúng binding để không gửi
  lời nhắc cũ sang một chat được liên kết lại. Có giờ nhắc theo múi giờ tài khoản.
- Worker nền kiểm tra các lời nhắc đến hạn mỗi khoảng 10 giây. Email/Telegram
  tiếp tục khi đóng tab web nếu server vẫn hoạt động và kênh còn được cấp quyền.
  Popup/chuông chỉ hoạt động khi web mở; trình duyệt có thể yêu cầu một tương tác
  để cho phép âm thanh. Không có Web Push/chuông hệ điều hành khi đã đóng browser.
- Hủy hoặc hoàn tác tạo lời nhắc trước hạn sẽ hủy hàng đợi ở các kênh. Email hoặc
  tin Telegram đã gửi không được thu hồi. Bấm Tắt chuông chỉ xác nhận lời nhắc
  trên web; các kênh đã bật vẫn nhận thông báo một lần.

## Inbox

Inbox chỉ lưu ý tưởng và việc tạm thời. Nội dung bắt đầu bằng “1 phút nữa…” không
tự tạo lời nhắc. Đã bỏ bộ đếm, nút nhắc lại và endpoint đặt nhắc cũ. Các thông báo
Inbox đã được tạo trước thay đổi được chuyển sang NORA, giữ ID, giờ và trạng thái
đã đọc; không yêu cầu viết lại ghi chú. Không có migration cấu trúc database.

API quản lý lời nhắc: `GET /api/agent/reminders`,
`DELETE /api/agent/reminders/{id}`. Tạo qua luồng chat NORA và công cụ có kiểm tra
quyền tài khoản; không có endpoint tạo thông báo tùy ý cho người khác.
