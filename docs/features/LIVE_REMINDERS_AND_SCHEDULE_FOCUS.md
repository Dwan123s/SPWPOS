# Nhắc nhở và phiên tập trung theo lịch

Cập nhật 30/10/2026.

## Phiên tập trung tự động

- Dashboard nhận cả phiên công việc và sự kiện cố định, gồm thời khóa biểu nhập từ ảnh.
- Trước giờ bắt đầu: hiện lịch tiếp theo và đếm ngược đến giờ bắt đầu. Đến giờ: tự chuyển sang trạng thái đang diễn ra, hiển thị thời gian còn lại và tiến độ theo lịch. Hết giờ: chuyển sang lịch tiếp theo.
- Giờ được tính lại từ timestamp thực khi quay lại tab, cập nhật mỗi giây khi trang hiển thị. Lịch qua đêm và múi giờ tài khoản vẫn được hỗ trợ.
- Phiên Running đã hết giờ không chặn lịch hiện tại. Khi trùng giờ, việc khẩn được ưu tiên, sau đó mới đến phiên đang được ghi nhận.
- Danh sách hôm nay đếm cả sự kiện cố định và đổi nhãn/màu theo thời gian. Không còn hiện “0 phiên” khi vẫn có lịch học.
- Đây là đồng hồ theo lịch. Không tự tạo execution segment, tự đánh dấu hoàn thành công việc hay cộng XP chỉ vì đồng hồ đã chạy hết. Người dùng vẫn có thể ghi nhận kết quả thực tế trong chi tiết/catch-up.

## Thông báo trong ứng dụng

- Popup và số chưa đọc trên chuông cập nhật độc lập, kể cả khi đang nhập ghi chú, làm việc với NORA hoặc mở hộp thoại. Không tải lại cả màn hình để lấy thông báo.
- Poll mỗi 20 giây khi tab hiển thị; quét tạo lời nhắc tối đa mỗi phút, đồng thời lấy thông báo khi mở app/quay lại tab. `POST /api/notifications/refresh` chỉ xử lý tài khoản đang đăng nhập; `GET /api/notifications` đọc tối đa 100 thông báo hiển thị bằng query riêng.
- Thời khóa biểu là FixedEvent nên có nhắc `EVENT_REMINDER` theo cấu hình số phút nhắc trước phiên. Có cửa sổ bù hai phút sau lúc bắt đầu để xử lý tab vừa thức dậy. Không thêm luồng email cho loại thông báo này.
- Phiên đã qua chưa xác nhận tạo `SESSION_REVIEW`, dẫn về Hôm nay để ghi nhận kết quả. Dedupe key ngăn tạo lặp cùng lịch/phiên.
- Khi đăng nhập có thông báo chưa đọc, popup tóm tắt hộp thư một lần; thông báo mới hiển thị tiêu đề và nội dung riêng. Tối đa ba popup cùng lúc. Đóng popup không đánh dấu đã đọc; ID popup đã đóng được lưu riêng theo tài khoản, không lưu nội dung nhắc nhở vào localStorage.
- Trong trang Thông báo có nút **Thử popup**. Đây chỉ là xem thử giao diện, không tạo thông báo giả trong database/hộp thư. Khi đã tắt thông báo trong ứng dụng, nút thử bị vô hiệu và popup không xuất hiện.
- Popup hoạt động trong ứng dụng đang mở; không yêu cầu quyền thông báo Windows/trình duyệt và không dùng service worker/push nền.

## Phân tích khi chủ yếu dùng thời khóa biểu

- Biểu đồ bảy ngày tính cả lịch cố định và phiên công việc. Khoảng trùng nhau được hợp nhất để không cộng hai lần; lịch qua đêm được cắt theo ngày địa phương. Mỗi ngày hiện rõ thời lượng đã có lịch.
- Khi chưa có dữ liệu thực tế để so sánh ước lượng, thẻ đầu hiển thị **Lịch hôm nay**: tỷ lệ thời lượng lịch đã trôi, số lịch đã qua/đang diễn ra và thời gian còn lại. Chỉ số này không được coi là đã hoàn thành công việc hay là độ chính xác ước lượng. Dữ liệu ước lượng thực vẫn được giữ khi có.
- Khả năng xếp thêm dùng dữ liệu khả năng từ backend và giờ rảnh đã cấu hình. Chưa đặt giờ rảnh thì hiển thị hướng dẫn, không suy diễn toàn bộ ngày là rảnh hoặc tạo số khả năng giả.
- Khi không có deadline cứng, thẻ mốc thời gian hiển thị lịch sắp tới và giờ kết thúc của lịch đang diễn ra. Deadline công việc thật vẫn giữ loại và hành động riêng.
- Các thẻ cập nhật theo thời gian và ranh giới lịch. Bộ kiểm tra frontend cho thay đổi này có 84 ca đạt; countdown trong phiên tập trung tiếp tục đếm giây còn lại đến giờ kết thúc.

## Kiểm chứng và bản chạy local

- Thông báo: 54 frontend tests, 27 unit tests và 13 API tests liên quan đã đạt; kiểm tra lại popup riêng sau khi cập nhật số chưa đọc của bản tóm tắt.
- Phiên tập trung: 78 frontend tests liên quan đạt, gồm chuyển đúng ranh giới bắt đầu/kết thúc, fixed event, scheduled session, qua đêm, refocus, không sửa dữ liệu thực thi và điều chỉnh kết quả qua chi tiết phiên.
- Backend Release build thành công, không warning/error. Schema local đã khớp; không cần và không chạy migration mới. Đã cập nhật backend local cùng frontend wwwroot, xác nhận HTTP đang phục vụ endpoint refresh, popup và đồng hồ theo lịch mới.
- Kiểm tra trực tiếp qua điều khiển Cốc Cốc bị công cụ dừng vì chưa hỗ trợ kiểm soát URL cho trình duyệt đó. Kết quả xác minh gồm các suite tự động và HTTP của ứng dụng local, không tuyên bố đã bấm thử trực tiếp trên Cốc Cốc.
