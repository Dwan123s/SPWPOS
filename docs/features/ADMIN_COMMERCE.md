# Quản lý doanh thu và lịch sử mua hàng

Admin mở menu **Quản trị viên → Doanh thu & đơn hàng** (`#admin-revenue`). Trang này riêng với **Quản lý người dùng** (`#admin-users`) và **Trạng thái hệ thống** (`#admin-system`). Link cũ `#admin` chuyển về quản lý người dùng. Không cần cấu hình thêm và không cần migration; báo cáo đọc các đơn payOS đã được hệ thống ghi nhận.

- Bốn chỉ số: hôm nay, tuần này (từ thứ Hai), tháng này, toàn bộ thời gian; kèm số đơn thanh toán.
- Khoảng thời gian chọn nhanh hôm nay/tuần/tháng/năm hoặc nhập từ ngày–đến ngày, tối đa 366 ngày. Hai ngày đầu/cuối đều được tính theo **Asia/Ho_Chi_Minh (UTC+7)**.
- Biểu đồ doanh thu theo ngày, tuần, tháng. Các khoảng không có đơn nhận giá trị 0; khoảng tuần/tháng đầu và cuối chỉ tính phần nằm trong bộ lọc. Có thể trỏ chuột, focus/bấm cột để xem số tiền và số đơn; cuộn ngang nếu có nhiều cột.
- Tổng tiền, số đơn, số khách mua khác nhau và giá trị trung bình mỗi đơn trong khoảng chọn.
- Lịch sử có tìm theo email/tên người mua/mã đơn/mã giao dịch; lọc trạng thái; 20 đơn/trang; bấm **Xem** để mở chi tiết và mã giao dịch đối soát.
- Bấm **Làm mới doanh thu** để đọc số liệu mới. Bộ lọc ngày áp dụng đồng thời cho biểu đồ và lịch sử; nhóm biểu đồ không đổi bộ lọc đơn hàng.

## Quy tắc tính

Doanh thu chỉ cộng `SkinPurchase.Status == PAID` và có `PaidAt`. Thời điểm ghi nhận là `PaidAt` do luồng xác minh webhook/merchant lookup của payOS lưu, không phải ngày tạo đơn. Đơn chờ/hết hạn/hủy không cộng doanh thu. Đơn thanh toán sau ngày tạo xuất hiện tại ngày xác nhận tiền.

Lịch sử dùng `PaidAt` cho đơn đã thanh toán (fallback `CreatedAt` với dữ liệu cũ thiếu `PaidAt`); các trạng thái còn lại dùng `CreatedAt`. Đơn PENDING/CREATING qua `ExpiresAt` được hiển thị là EXPIRED, chỉ đọc và không sửa dữ liệu. Tổng tiền dùng số nguyên 64 bit, không tính phí cổng hay lợi nhuận; hệ thống hiện chưa có nghiệp vụ hoàn tiền.

Tổng hợp trên SQL Server tại database thành tối đa 366 tổng ngày trước khi nhóm tuần/tháng. Không gọi payOS theo từng dòng khi mở báo cáo; dữ liệu phụ thuộc các thanh toán đã được xác minh/lưu. Nếu webhook bị trễ, báo cáo cập nhật sau khi webhook hoặc luồng kiểm tra đơn hoàn tất đối soát.

## API và phân quyền

- `GET /api/admin/commerce/revenue?from=2026-11-01&to=2026-11-30&groupBy=day` (`day|week|month`). Mặc định đầu tháng–hôm nay.
- `GET /api/admin/commerce/purchases?from=2026-11-01&to=2026-11-30&status=PAID&search=...&page=1&pageSize=20`. Trạng thái bỏ trống hoặc PAID/PENDING/CREATING/EXPIRED/CANCELLED, tối đa 100 dòng/trang.
- Cả hai endpoint yêu cầu role **Admin**; chưa đăng nhập 401, tài khoản thường 403. Không trả accountNumber, checkoutUrl, khóa/token thanh toán hay dữ liệu lịch/Notes.
- Chỉ đọc báo cáo; admin không tự sửa trạng thái PAID hoặc cộng doanh thu bằng tay.

## Tách các trang quản trị (03/11/2026)

Menu **Quản trị viên** gồm ba trang độc lập: người dùng, doanh thu/đơn hàng và trạng thái hệ thống. Không còn mục Quản trị chung ghép các bảng vào một màn hình.

- Người dùng chỉ gọi API danh sách/tìm kiếm tài khoản; giữ khóa, mở khóa và lịch sử tài khoản.
- Doanh thu chỉ gọi các API báo cáo và mua hàng; không tải danh sách người dùng hay chỉ số vận hành.
- Hệ thống chỉ tải status/metrics/health/jobs; không kèm bảng tài khoản hoặc giao dịch.
- Link cũ `#admin` vẫn mở được qua chuyển hướng về `#admin-users`. Mọi trang mới và menu đều yêu cầu role Admin; tài khoản thường không gọi API quản trị khi nhập link trực tiếp.
- Tắt chu kỳ tải lại workspace trên cả ba trang để giữ bộ lọc và thao tác đang dùng.

Kiểm tra thành phần/tải trang: **13 passed**. Kiểm tra điều hướng toàn workspace, tách dữ liệu, link cũ và ba route với tài khoản thường: **6 passed**. Build frontend thành công, bundle đang được API local phục vụ có đủ ba route mới.

## Kiểm tra

`CommerceAdminTests`: mốc 00:00 Việt Nam, đầu tuần thứ Hai, đầu tháng, khoảng trống 0, lọc theo ngày nhận tiền, tổng tiền vượt Int32, trạng thái hết hạn, tìm kiếm, phân trang ổn định, không sửa đơn, validation.

`CommerceAdminApiTests`: 401/403/Admin, payload đối soát không lộ số tài khoản/checkout URL, filter lỗi 400; thêm tình huống SQL Server thật khi `SPWPOS_TEST_SQLSERVER` được cấu hình (database test riêng).

`frontend/tests/admin-commerce.test.jsx`: khoảng ngày đồng bộ, đổi nhóm biểu đồ, tìm/lọc/phân trang, popup, trạng thái 0/lỗi/thử lại, bỏ phản hồi cũ, preset múi giờ Việt Nam.

Kết quả kiểm tra 03/11/2026: **28 unit test** (báo cáo + thanh toán), **5 integration test** (gồm SQL Server thật và quyền Admin), **7 test giao diện**, **1 test điều hướng toàn bộ workspace/Admin** đều qua. Build frontend thành công. Browser bundle production ở 1440/390/320 px: đổi biểu đồ, popup, phân trang/lọc và reset trang hoạt động; không tràn ngang toàn trang, không lỗi JavaScript, không phát sinh request sửa thanh toán. Ảnh và log tại `artifacts/commerce-admin-*`.

Kiểm tra database local: đọc báo cáo/lịch sử thành công. Trước khi chạy bản mới, đã tạo COPY_ONLY backup và RESTORE VERIFYONLY tại `/var/opt/mssql/data/PLANORA_before_commerce_admin_20261102_193343.bak`, sau đó áp dụng migration bổ sung `TeamMembershipApproval` đã có trong source (không phải migration của báo cáo). Không còn migration chờ áp dụng. Báo cáo không sửa đơn thanh toán.

Bản local đã rebuild 0 warnings/0 errors và khởi động tại `https://localhost:7043`, runtime riêng `artifacts/api-runtime/20261102-193406-ef288dfc7ad04108ac47bd6cb1465076`. Cấu hình AI vẫn VertexAI / gemini-3.8-flash.
