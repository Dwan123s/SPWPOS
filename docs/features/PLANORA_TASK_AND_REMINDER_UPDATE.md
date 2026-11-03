# Cập nhật công việc, giờ rảnh và nhắc nhở

- Danh sách công việc hiển thị 12 mục mỗi trang; bộ lọc đưa về trang đầu. Tên dài hiển thị tối đa hai dòng; mở chi tiết để đọc đầy đủ.
- Lưu nhiều khoảng rảnh trên cùng một ngày, ví dụ thứ 2 07:00–09:00 và 15:00–17:00. Bấm biểu tượng lịch cạnh công việc để tự xếp công việc đó vào những khoảng rảnh đã lưu. Khi tạo việc mới, tùy chọn **Tự xếp vào giờ rảnh sau khi tạo** mặc định bật nếu đã có giờ rảnh.
- Hệ thống giữ các phiên khác, kiểm tra sự kiện cố định, thời gian nghỉ, giới hạn làm việc, hệ số năng lực và phụ thuộc. Nếu không đủ thời gian hoặc có cảnh báo, lịch được mở để xem trước; chỉ áp dụng tự động khi không có cảnh báo. Nếu xếp lịch lỗi sau khi tạo việc, công việc vẫn được lưu.
- Quản trị có tên đăng nhập, ngày tạo, lần đăng nhập gần nhất, số phiên đăng nhập chưa hết hạn và số công việc hoàn thành / tổng số. Công việc đã xóa và phiên đăng nhập hết hạn không được đếm. Tìm kiếm hỗ trợ tên đăng nhập. Phần pet được để sau.
- Bật nhắc qua email trong Cài đặt → Thông báo. Xem [cấu hình Gmail](../integrations/GMAIL_SMTP.md); không nhập App Password vào chat hoặc Git.
- Web có âm báo ngắn khi nhận thông báo mới, mặc định bật. Trong **Thông báo** hoặc **Cài đặt → Thông báo**, dùng **Âm thanh thông báo** để bật/tắt và **Nghe thử âm báo** để kiểm tra. Lựa chọn lưu theo tài khoản trên trình duyệt này, có hiệu lực ngay. Cần bấm hoặc gõ trên trang trước để trình duyệt cho phép âm thanh. Thông báo cũ lúc mở trang, đọc lại hoặc polling không phát lặp; một lượt nhận nhiều thông báo chỉ phát một âm. Tắt thông báo trong ứng dụng cũng tắt âm báo. Âm thanh đi cùng cơ chế polling hiện tại khi tab đang hiển thị, không phải thông báo hệ điều hành khi đóng web.

## Áp dụng trên môi trường development

Dừng API, chạy script khởi động chuẩn từ repository để build frontend, áp dụng migration mới và khởi động lại:

```powershell
.\scripts\Start-Development.ps1 -SkipCertificateTrust
```

Nếu SMTP đang là Mailpit, cấu hình Gmail bằng script nhập credential an toàn:

```powershell
.\scripts\Configure-Gmail.ps1 -RestartApi
```

Sau đó bật email nhắc nhở trong tài khoản và kiểm tra một công việc đến hạn hoặc phiên sắp bắt đầu. Kiểm tra SMTP kết nối thành công chưa xác nhận email đã vào Inbox của Gmail.
