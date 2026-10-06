# PLANORA — UI research và thiết kế lại

Ngày: 05/10/2026. Hướng người dùng chọn: **nền sáng, hiện đại và gọn**.

Đã khảo sát các website chính thức của bảy sản phẩm tiêu biểu cùng tài liệu thiết kế liên quan. Đây là tập tham khảo có chọn lọc theo nghiệp vụ lập kế hoạch cá nhân, không phải tuyên bố đã khảo sát mọi website trên Internet. Nghiên cứu dùng các trang giới thiệu, tài liệu và hình minh họa công khai; không đăng nhập vào các sản phẩm tham khảo.

## Nguồn và quyết định áp dụng

| Nguồn chính thức | Điểm liên quan | Áp dụng trong PLANORA |
| --- | --- | --- |
| [Sunsama](https://www.sunsama.com/) | Lập kế hoạch ngày, bảo vệ thời gian tập trung, đối chiếu dự kiến và thực tế. | Dashboard ưu tiên phiên tiếp theo, tách workload tổng và khả năng 7 ngày, lịch hôm nay nằm cạnh công việc cần chú ý. |
| [Motion](https://www.usemotion.com/) và [Agenda view](https://help.usemotion.com/motions-agenda-view) | Công việc và sự kiện cùng xuất hiện trong agenda; lập lịch dựa trên thời lượng, deadline và phụ thuộc. | Chế độ danh sách lịch tuần; hành động lập kế hoạch dẫn tới xem trước và xác nhận theo cơ chế hiện có. |
| [Akiflow](https://akiflow.com/) | Task và calendar trong cùng sản phẩm, dành khoảng thời gian cụ thể cho công việc. | Điều hướng nhóm theo luồng làm việc; ghi nhanh công việc và tìm kiếm luôn dễ tiếp cận. |
| [Morgen](https://www.morgen.so/) | Task đặt lên lịch; người dùng xem xét kế hoạch và các rủi ro về khả năng thực hiện. | Giữ phân biệt lịch thực tế, đề xuất và kịch bản; các cảnh báo không bị che bởi giao diện. |
| [Todoist](https://www.todoist.com/) | Ghi nhanh bằng ngôn ngữ tự nhiên; các góc nhìn Today, Upcoming và bộ lọc. | Tab công việc Tất cả / Đang mở / Đang làm / Hoàn thành, bộ lọc và trạng thái không có kết quả. |
| [TickTick](https://ticktick.com/features) | Danh sách, lọc và nhiều góc nhìn lịch, gồm tuần và agenda. | Nút chuyển Tuần / Danh sách; điện thoại mặc định xem danh sách để dễ thao tác. |
| [Notion Calendar](https://www.notion.com/en-gb/product/calendar) | Deadline và sự kiện trong cùng góc nhìn; command menu và phím tắt. | Tìm kiếm Ctrl/Cmd+K; lịch thể hiện rõ múi giờ, phiên đã khóa và sự kiện cố định. |
| [Linear — design refresh, 12/03/2026](https://linear.app/now/behind-the-latest-design-refresh) | Phân cấp thông tin, vị trí thao tác nhất quán, giảm mức cạnh tranh thị giác của điều hướng. | Sidebar sáng, đường phân cách nhẹ, bảng màu bớt bão hòa; CTA chính luôn nằm cạnh tiêu đề trang. |

Các lựa chọn màu sắc và bố cục là tổng hợp thiết kế riêng cho PLANORA. Không sao chép logo, ảnh hoặc tài sản thương hiệu của các sản phẩm tham khảo. Giữ logo PLANORA đang có trong workspace.

## Các màn hình đã cập nhật

- Hôm nay: phiên tập trung tiếp theo/đang chạy, tiến độ phiên trong ngày, workload và khả năng thực hiện, danh sách deadline gần nhất, timeline hôm nay.
- Công việc: tab có số lượng, tìm kiếm và bộ lọc, bảng dễ quét, deadline mở rộng khi cần; kết quả tìm kiếm rỗng có thông báo riêng.
- Lịch: tuần bắt đầu thứ Hai, block phiên gọn, trạng thái bằng chữ bên cạnh màu; chuyển sang agenda, điều hướng tuần và chỉnh sửa sự kiện/phiên.
- Thời gian rảnh, phân tích, kịch bản, thông báo, tài khoản và quản trị: dùng cùng nền, kiểu chữ, card, trường nhập và nút.
- Đăng nhập, đăng ký, OTP và khôi phục mật khẩu: bố cục mới; minh họa lịch được ghi rõ là mẫu, không dùng số tăng hiệu suất bịa đặt.
- Modal và điện thoại: nhãn nút có nghĩa, focus rõ, menu không cho focus vào phần nền khi mở, Escape đóng menu và trả focus, giảm chuyển động theo tùy chọn hệ điều hành.

Thay đổi chỉ ở frontend và kiểm thử liên quan. Không đổi hợp đồng API, mô hình dữ liệu hoặc cơ chế lập lịch. Giữ trạng thái phân tích chưa xác định khi API không trả kết quả.

## Mã nguồn

Trong `src/SPWPOS.Api/wwwroot/assets`:

- `layout.css`: hệ thống bố cục mới và responsive; `app.css`: token nền và component cơ sở.
- `shell.js`: khung ứng dụng, điều hướng, menu điện thoại, tìm kiếm.
- `dashboard.js`, `schedule.js`, `auth-view.js`: các màn hình được thiết kế lại.
- `app.js`, `tasks.js`, `calendar.js`, `pages.js`, `auth.js`, `ui.js`: nối giao diện với các hành động và biểu mẫu hiện có.

Các dữ liệu mẫu và API giả **chỉ nằm trong `tests/browser/ui-preview-server.cjs`**, không được đưa vào `wwwroot` hay backend production. Server chỉ lắng nghe `127.0.0.1:5189` và không kết nối database/email/OpenAI.

## Kiểm chứng

- Release build: 0 warning, 0 error.
- 9 kiểm thử Node: session refresh nhiều tab, trạng thái phân tích thiếu, tuần qua ranh giới tháng/năm, sự kiện kết thúc đúng nửa đêm và tiêu đề lịch ở múi giờ UTC+14.
- Edge headless với hồ sơ riêng: 71 kiểm tra, không có exception JavaScript, vi phạm CSP hoặc toast lỗi.
- 9 trang làm việc được kiểm tra tại 1440×1000 và 390×844; kiểm tra tràn ngang của trang, cùng ảnh đăng nhập/đăng ký, tab cài đặt, form công việc, xem trước kế hoạch, lịch danh sách và menu.
- Luồng UI dùng API giả đã thử: tạo công việc, lọc hoàn thành, tìm kiếm không có kết quả, tìm kiếm toàn cục và mở chi tiết, xem trước/áp dụng kế hoạch, bắt đầu/tạm dừng/tiếp tục phiên, menu/Escape/trả focus, hiện/ẩn mật khẩu.

Ảnh: `artifacts/ui/screenshots/`. Kết quả máy đọc: `artifacts/ui/browser-results.json`. Bộ kiểm tra lịch mới đã được thêm vào script trước merge và CI.

Phạm vi kiểm chứng: Edge và dữ liệu mẫu, chưa chạy E2E lần này với SQL Server thật, gửi OTP/email hoặc gọi OpenAI thật. Kiểm tra responsive không thay thế kiểm tra trên thiết bị iOS/Android thực. Chưa thực hiện đánh giá accessibility đầy đủ bằng screen reader.

Chạy lại kiểm tra UI trên Windows có Edge và Node.js 20.18+, từ thư mục dự án trong hai terminal:

```powershell
node tests/browser/ui-preview-server.cjs
node tests/browser/ui-browser-check.cjs
```

Chạy kiểm thử module:

```powershell
node --experimental-default-type=module --test tests/browser/session-refresh.test.mjs tests/browser/analysis-state.test.mjs tests/browser/calendar-view.test.mjs
```

File server và driver là công cụ phát triển, không phải cách chạy ứng dụng production. Dữ liệu trên ảnh chụp là dữ liệu tổng hợp dùng để đánh giá giao diện.
