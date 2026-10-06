# PLANORA — liquid glass toàn hệ thống

Giao diện dùng cùng phong cách xanh–trắng của trang login: nền chuyển sắc nhẹ, lớp kính trong mờ, viền bắt sáng, bóng mềm, chữ navy và nút xanh có phản sáng. Theme nằm trong `src/SPWPOS.Api/wwwroot/assets/glass.css`; `index.html` nạp sau các stylesheet hiện có và `shell.js` bật class `glass-workspace` cho phần ứng dụng.

## Phạm vi

- Sidebar dạng panel bo góc, thanh trên sticky, tìm kiếm, menu và tài khoản.
- Hôm nay: phiên tập trung, thống kê, danh sách ưu tiên và lịch trong ngày.
- Công việc: tab, bộ lọc, danh sách, tiến độ và popup thao tác.
- Lịch: toolbar, chế độ tuần/danh sách, trạng thái phiên và sự kiện cố định.
- Thời gian, phân tích, kịch bản, thông báo, hồ sơ/các tab tài khoản và quản trị.
- Đăng ký, OTP, lấy lại mật khẩu dùng cùng chất liệu kính như login.
- Dialog, form, trạng thái lỗi/cảnh báo và toast dùng màu/viền đồng bộ.

Theo yêu cầu, ẩn thanh cuộn của trang và sidebar; vẫn giữ overflow và thao tác cuộn. Thanh cuộn bên trong lịch tuần hoặc bảng dữ liệu vẫn được giữ để người dùng thấy nội dung có thể cuộn ngang.

## Đọc nội dung và responsive

Chỉ blur các panel lớn, không thêm blur cho từng dòng dữ liệu. Không dùng chuyển động liên tục hay hiệu ứng biến dạng văn bản. CSS có nền gần đặc khi trình duyệt không hỗ trợ backdrop-filter. Có focus ring, màu riêng cho cảnh báo/lỗi và reduced motion. Nút chính có tương phản chữ trắng tối thiểu 4,83:1 trên các màu trong gradient normal/hover được kiểm tra.

Desktop dùng sidebar bo góc nằm trong lề trang; dưới 800px dùng menu dạng drawer và thẻ gọn hơn. Khung lịch tuần/bảng dữ liệu tiếp tục cuộn riêng. Không thay đổi endpoint, nghiệp vụ, sự kiện thao tác hoặc dữ liệu.

## Kiểm chứng và giới hạn

- Cú pháp `shell.js` đạt.
- Localhost phục vụ `index.html`, `glass.css` và `shell.js` khớp hash với file vừa cập nhật.
- 9 kiểm tra frontend hiện có đạt.
- Dựng nội dung 18 trường hợp với dữ liệu mẫu nội bộ đạt: 9 trang chính, 4 tab tài khoản, 2 chế độ lịch, form công việc, bảng người dùng và thống kê quản trị.
- Kiểm tra cân bằng dấu ngoặc/comment/string của stylesheet mới đạt; đây không phải kiểm tra CSS trong trình duyệt.
- Kết quả smoke check nằm trong `artifacts/ui/glass-content-results.json`; script một lần trong `artifacts/ui/glass-content-check.mjs`.

Công cụ trình duyệt đã chặn thao tác truy cập tab trong cuộc trò chuyện này bằng chính sách URL. Chưa xác nhận hình ảnh, overflow thực tế hoặc toàn bộ tương tác UI bằng trình duyệt cho lần cập nhật này. Các lần kiểm tra Edge ở báo cáo UI cũ không đại diện cho theme liquid glass mới.
