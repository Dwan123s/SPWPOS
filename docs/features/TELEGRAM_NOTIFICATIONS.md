# Thông báo Telegram

Telegram dùng cùng bộ nhắc lịch và thời gian nhắc của PLANORA: deadline, phiên làm việc, sự kiện cố định trong thời khóa biểu, phiên cần ghi nhận và cảnh báo lịch. Gửi tự động từ background worker, không phụ thuộc trình duyệt đang mở. Telegram được bật riêng, không phụ thuộc bật/tắt email hoặc thông báo trong ứng dụng.

## Cấu hình máy chủ

1. Dùng bot riêng được tạo bằng BotFather. PLANORA nhận lệnh bằng `getUpdates`, không cần public webhook hoặc tunnel cho môi trường local. Không dùng chung bot với một tiến trình polling khác hoặc webhook đang hoạt động. PLANORA không tự xóa webhook của bot.
2. Áp dụng migration `TelegramNotifications` bằng quy trình triển khai của dự án.
3. Với máy Windows local của dự án, chạy `powershell.exe -NoProfile -ExecutionPolicy Bypass -File .\scripts\Configure-Telegram.ps1` từ thư mục gốc. Script yêu cầu dán token ở ô nhập ẩn, kiểm tra định dạng trước khi lưu bằng User Secrets và tự khởi động lại bản Debug. Token trống/sai định dạng sẽ bị từ chối, không ghi đè cấu hình. Có thể dùng `-NoRestart` để chỉ lưu.

   Với hosting, lưu token trong secret store hoặc biến môi trường:

   ```text
   Telegram__Enabled=true
   Telegram__BotToken=<token lấy từ BotFather>
   Telegram__PollSeconds=3
   ```

   Tên khóa User Secrets tương ứng: `Telegram:Enabled`, `Telegram:BotToken`, `Telegram:PollSeconds`. Không ghi token vào repository, frontend, URL website hoặc log. Cấu hình ví dụ mặc định tắt Telegram để ứng dụng vẫn hoạt động trước khi có token.
4. Khởi động lại API sau khi cấu hình thủ công. Cần truy cập HTTPS outbound tới `api.telegram.org`. Nếu Telegram bật nhưng thiếu token/sai định dạng hoặc chu kỳ kiểm tra không hợp lệ, chỉ tích hợp Telegram tạm ngừng; website và các tính năng khác vẫn khởi động. Trang cài đặt hiển thị lỗi cấu hình và log chỉ ghi mã lỗi.

## Người dùng kết nối

Mở **Cài đặt → Thông báo** hoặc **Kết nối**, bấm **Kết nối Telegram**, bấm **Mở bot Telegram · Nhấn Start** rồi nhấn **Start** trong Telegram. Website tự cập nhật trạng thái. Không cần nhập token hay chat ID. Liên kết có hiệu lực 10 phút, dùng một lần; tạo liên kết mới sẽ vô hiệu liên kết cũ.

Mỗi Telegram cá nhân chỉ liên kết một tài khoản PLANORA. Chỉ nhận liên kết từ chat private và người gửi đúng chủ chat. Không nhận lệnh từ nhóm/kênh. Chỉ thông báo của chủ tài khoản được gửi tới chat đã liên kết. Có nút tạm dừng/bật lại, ngắt kết nối; lệnh `/stop` trong bot cũng ngắt kết nối. Đổi Telegram sẽ hủy thông báo còn chờ của liên kết cũ.

## Gửi và kiểm tra

Tin Telegram dùng HTML được Telegram hỗ trợ: tiêu đề/tên việc in đậm, khối ngày giờ, thời lượng, thời gian còn lại và chân tin PLANORA. Nhắc lịch/phiên/deadline lấy dữ liệu hiện tại theo đúng chủ tài khoản và múi giờ đã lưu; hiển thị giờ Việt Nam thay vì mã múi giờ kỹ thuật. Nội dung người dùng được escape trước khi ghép HTML. Tin đã gửi giữ nguyên mẫu cũ; mẫu mới dùng cho các lượt gửi tiếp theo.

- Có hai lượt nhắc độc lập cho mỗi sự kiện cố định/phiên làm việc: trước giờ bắt đầu theo cài đặt và lúc bắt đầu với trạng thái **Đang diễn ra**. Mỗi lượt có khóa chống trùng riêng gắn với bản ghi và giờ bắt đầu; đọc lượt trước không làm mất lượt đến giờ. Không tự chạy timer hoặc cập nhật tiến độ công việc.
- Worker quét nhanh các lịch vừa bắt đầu mỗi 10 giây, cho phép bắt kịp trong 2 phút và chỉ gửi khi lịch còn hiệu lực. Quét toàn bộ nhắc trước giờ, deadline, recovery và email vẫn mỗi 60 giây; worker Telegram kiểm tra lệnh kết nối mặc định mỗi 3 giây. Thời điểm nhận còn phụ thuộc xử lý và kết nối mạng, không bảo đảm chính xác từng giây. Máy chủ phải chạy để gửi nhắc.
- Thông báo chờ và kết quả gửi lưu trong DB, retry với backoff, không gửi lại thông báo đã ghi nhận thành công. Trước khi gửi kiểm tra lại task hoàn thành/xóa/đổi deadline, phiên đổi giờ và sự kiện đổi giờ/xóa. Telegram không hỗ trợ idempotency key cho `sendMessage`: nếu máy chủ mất kết nối sau khi Telegram đã nhận nhưng trước khi lưu kết quả, retry vẫn có thể tạo tin trùng.
- Không gửi hàng loạt thông báo đã có trước lúc kết nối. Người dùng đọc thông báo trong app không chặn nhắc Telegram.
- Bot bị chặn hoặc Telegram từ chối đích gửi: tạm dừng kênh, giao diện yêu cầu bỏ chặn/kết nối lại. Lỗi mạng/rate limit sẽ retry. Log chỉ chứa mã lỗi hoặc loại lỗi, không có token, chat ID hay nội dung cá nhân.
- Kết nối thật cần token bot hợp lệ và thao tác Start của người dùng. Kiểm thử tự động dùng gateway giả, không gửi thông báo vào Telegram thật.

API đăng nhập: `GET /api/integrations/telegram/status`, `POST .../connect`, `PUT .../preferences` với `{ "enabled": true }`, `DELETE /api/integrations/telegram`. Không có endpoint nhận chat ID do client tự khai.

Tài liệu Telegram: [Bot API](https://core.telegram.org/bots/api), [deep links](https://core.telegram.org/bots/features#deep-linking).
