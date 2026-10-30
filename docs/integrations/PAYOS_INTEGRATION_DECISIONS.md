# payOS — quyết định tích hợp

**Cập nhật 28/10/2026:** đóng băng thanh toán tiền thật; skin NORA mở bằng XP nội bộ. Endpoint tạo thanh toán trả `410 REAL_PAYMENTS_DISABLED`. Phần dưới lưu lại thiết kế nghiên cứu lịch sử, không phải luồng đang hoạt động. Xem [XP và inventory hiện tại](../features/WORKSPACE_UPGRADE_PHASES_1_TO_4.md).

Người dùng chọn payOS ngày 15/10/2026 và chưa có merchant. Trạng thái: nghiên cứu/chuẩn bị, chưa tạo checkout hay xác minh thanh toán thật.

Tài liệu chính thức đã kiểm tra:

- [API](https://payos.vn/docs/api/)
- [Chữ ký payment-requests](https://payos.vn/docs/tich-hop-webhook/kiem-tra-du-lieu-voi-signature/)
- [.NET SDK](https://payos.vn/docs/sdks/back-end/net/)

Tài liệu API hiện liệt kê endpoint production `api-merchant.payos.vn`; chưa xác minh được sandbox công khai. Không tự bịa hostname sandbox. Cần merchant/test channel được payOS hỗ trợ trước khi kiểm thử bên ngoài.

Tạo link dùng Client ID, API Key, Checksum Key. Chuỗi ký gồm amount/cancelUrl/description/orderCode/returnUrl theo thứ tự alphabet, HMAC SHA-256. Webhook ký phần data; không dùng thuật toán payouts cho payment-requests. Hủy link không đồng nghĩa hoàn tiền. Đường dẫn return/cancel chỉ phục vụ giao diện; quyền sở hữu skin phải chờ xác minh server.

## Ranh giới triển khai

1. Giá và currency VND lấy từ catalog server. Snapshot order/item bất biến; client chỉ gửi product ID và request ID. Skin chỉ thay hình thức, không tăng XP.
2. Reserve order/attempt trong transaction trước lời gọi provider, giữ nguyên orderCode khi kết quả mạng chưa rõ. Timeout chuyển UNKNOWN/PENDING_RECONCILIATION; không tạo đơn mới để thử lại.
3. Sau checkout, lưu paymentLinkId và chỉ dẫn checkout đã xác thực. Không đặt user-provided return URL hoặc tự mở thanh toán.
4. Webhook kiểm HMAC, orderCode, paymentLinkId, amount, VND và thông tin merchant/channel đã cấu hình. Signature hợp lệ chưa đủ để cấp skin nếu dữ liệu không khớp order.
5. Inbox có unique provider reference; xử lý payment và grant inventory trong transaction. Unique entitlement/order-item và user/skin ngăn cấp trùng, kể cả callback trùng hoặc mất phản hồi sau commit.
6. Webhook mẫu kiểm tra kết nối được nhận diện và không cấp skin. Callback unknown order cần đối soát, không tạo order từ payload.
7. Reconciliation đọc trạng thái provider khi pending/unknown, dùng cùng transition và grant path với webhook. Không tự hạ trạng thái PAID khi callback cũ tới muộn.
8. Refund cần quy trình riêng được xác minh với merchant; không dùng payouts như refund tự động. Dashboard tách confirmed payment/refund/settlement/fees và số chưa biết.

## Điều kiện trước khi kết nối ngoài

- Merchant/kênh thanh toán và quy trình test do payOS hỗ trợ.
- Ba credential lưu server qua User Secrets/secret manager, không vào chat hay frontend.
- HTTPS public webhook và origin return/cancel đã được duyệt.
- Catalog, XP/inventory, order/attempt/inbox/entitlement, audit và phân quyền finance hoàn chỉnh.
- Test offline và SQL concurrency đạt; sau đó mới kiểm thử với kênh test thực. Không xem test double là sandbox/live.

## Acceptance còn phải thực hiện

Tamper giá, sai signature/amount/currency/channel, order lạ, replay, callback đảo thứ tự, timeout tạo link, restart giữa commit và ack, reconciliation hai worker, equip chưa sở hữu, refund/entitlement revoke và quyền finance/support. Chưa có kết quả cho các acceptance này.
