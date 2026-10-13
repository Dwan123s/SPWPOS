# UI chung từ trang login

Dashboard và toàn bộ workspace dùng cùng màu xanh, ánh sáng, vật liệu kính và animation native như trang login. Cấu trúc ba cột của dashboard và thanh điều hướng gọn được giữ, các chức năng sử dụng dữ liệu thật như trước.

- `frontend/src/styles/workspace-login-theme.css` là lớp vật liệu chung sau CSS bố cục: nền gradient, viền sáng, shadow, blur, card, form, tab, lịch, bảng, nút và modal.
- `WorkspaceAtmosphere` tái sử dụng ảnh ribbon và bubble WebP/PNG từ login, trang trí phía sau nội dung. Ảnh có alt rỗng, nằm trong vùng aria-hidden và không nhận thao tác chuột.
- `WorkspaceMotionControl` dùng Web Animations API và IntersectionObserver để xuất hiện nhẹ theo từng panel; MutationObserver nhận các card tải sau từ API. Không chạy lại animation khi timer cập nhật.
- Bấm hoặc gõ sẽ kết thúc animation xuất hiện để form luôn dùng được. Nút tạm dừng lưu lựa chọn qua lần mở trang; khi tab ẩn hoặc hệ điều hành giảm chuyển động, hiệu ứng được dừng.
- Các khoảng ngày/giờ, cách tạo công việc, AI và API không thay đổi trong lần cập nhật UI này.
- Blur có mức nhẹ hơn trên điện thoại; không dùng zoom/scale toàn trang. In tài liệu bỏ nền trang trí, shadow và blur.

Khi viết cặp CSS tương thích, đặt `-webkit-backdrop-filter` trước `backdrop-filter`. Bộ tối ưu CSS có thể bỏ thuộc tính chuẩn nếu khai báo prefix đứng sau; cần kiểm tra `getComputedStyle(element).backdropFilter` trên bản production build, không chỉ nhìn source.

Kiểm tra bằng frontend tests và trình duyệt với dữ liệu giả: 10 trang chính, tab bảo mật, hộp thoại thêm công việc, nút tạm dừng và reduced motion. Màn hình desktop/tablet/mobile được kiểm tra ở 1440, 1024, 390 và 320px.

## Tutorial và quy trình sử dụng

`frontend/src/tutorial.jsx` bổ sung hướng dẫn 4 bước, thẻ bước tiếp theo trên Hôm nay và nút hiện/ẩn tên menu. CSS bổ sung nằm cuối chuỗi import trong `styles/tutorial.css`. Tutorial dùng dialog sẵn có và chuyển đến trang thật; ví dụ chỉ để đọc, không gọi API ghi dữ liệu.

Lời chào tự mở trên Hôm nay khi tài khoản chưa có công việc và chưa chọn bỏ qua/tiếp tục. Hướng dẫn tương tác trong `onboarding.js` lưu trạng thái và tiến độ theo ID tài khoản trên trình duyệt. Từng bước chỉ được đánh dấu khi API phân tích/lưu/bắt đầu thành công; lỗi hoặc chỉ nhập nội dung không mở bước tiếp theo. Ghi chú thực hành được mở lại khi chuyển về trang ghi chú. Không thay thế dialog đang mở. Menu mặc định hiện tên, lựa chọn thu gọn được nhớ trên thiết bị.

Khung lịch ngày mặc định dùng các thẻ phiên theo thứ tự giờ; có nút chuyển sang trục thời gian. `styles/daily-schedule.css` giữ thẻ đủ cao cho phiên ngắn, trạng thái rõ ràng và khoảng nghỉ tách riêng. Chế độ trục giờ giữ thời lượng thật, dùng nhãn truy cập/title cho thanh quá ngắn để chứa chữ.

Nhãn thao tác ghi chú phân biệt lưu nháp, xem trước công việc, tạo việc/xem lịch và tự động lưu lịch. Bản xem trước lịch có dòng xác nhận chưa áp dụng; kết quả đã lưu có liên kết mở lịch. Kết thúc phiên mở bước cập nhật tiến độ, không tự hoàn thành công việc.

Xem [hướng dẫn người dùng](USER_GUIDE.md) để biết trình tự đầy đủ.
