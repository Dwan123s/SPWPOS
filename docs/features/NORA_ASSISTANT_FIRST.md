# NORA hiểu mục đích trước khi dùng công cụ

Ngày cập nhật: 04/11/2026. Áp dụng cho NORA ở chế độ Agent; giữ nguyên model đang cấu hình, hiện là `gemini-3.8-flash`.

## Luồng xử lý

Trong chính phản hồi AI đầu tiên, NORA phải nêu một diễn giải ngắn về điều người dùng muốn, phân loại mục đích, xác định có cần dữ liệu cá nhân hay không và liệt kê thông tin thiết yếu còn thiếu. Không thêm một lượt gọi mô hình chỉ để phân loại. Đây là kết quả diễn giải công khai, không phải suy luận riêng tư và không thay thế quyền thực thi của máy chủ.

Agent dùng hướng dẫn riêng gọn hơn thay vì nối thêm giao thức JSON của Workflow: khoảng 10.500 ký tự so với 41.900 ký tự trước đó, giảm gần 75%. Lượt đầu chỉ gửi 9 công cụ cần thiết; công cụ sửa bản nháp/so sánh phương án chỉ được cung cấp ở lượt sau khi cần. Giữ cấu hình suy luận `LOW` của 3.8 Flash; model này không hỗ trợ `MINIMAL` và không thể tắt hoàn toàn thinking. [Tài liệu Google về thinking](https://firebase.google.com/docs/ai-logic/thinking).

| Mục đích | Cách xử lý |
| --- | --- |
| Chào hỏi, trao đổi, động viên | Trả lời trực tiếp; không gọi công cụ lịch |
| Hỏi cách dùng, tư vấn chung | Giải thích hoặc gợi ý; không tự biến thành task hay đọc lịch |
| Thiếu ngày, thời lượng, đối tượng hoặc chi tiết thiết yếu | Hỏi một câu tập trung trước khi đọc lịch/thử phương án; giữ các chi tiết đã có trong hội thoại |
| Xem lịch, giờ rảnh, danh sách task đơn giản | Đọc dữ liệu đã lưu và trả kết quả xác minh; không cần thêm lượt AI để lặp lại kết quả |
| Phân tích cá nhân cần dữ liệu | Chỉ cung cấp công cụ đọc; dùng dữ liệu thật để tư vấn |
| Yêu cầu tạo/sửa/xếp lịch rõ ràng | Chuẩn bị, kiểm tra và tạo phương án trong một thao tác khi khả thi; giữ nguyên quy tắc lưu/xác nhận/hoàn tác |

Không hỏi người dùng xác nhận lại một yêu cầu đã rõ. Tóm tắt mục đích được gửi qua tiến trình `INTENT` trước khi công cụ chạy và được giữ trong kết quả hội thoại. Giao diện hiển thị phản hồi trực tiếp hoặc yêu cầu đã được kiểm tra; chi tiết thao tác vẫn có thể mở xem.

## Giới hạn và bảo vệ

- Lượt đầu bắt buộc có `requestUnderstanding` trong cùng native function call. Bộ kết nối tách dữ liệu diễn giải ra khỏi tham số nghiệp vụ, giữ nguyên nội dung ký và `thoughtSignature` để tiếp nối với nhà cung cấp.
- Máy chủ kiểm tra cả nhóm thao tác trước khi chạy: mục đích tư vấn/xem dữ liệu không được dùng công cụ tạo/sửa lịch. Khi có thông tin thiết yếu còn thiếu, chỉ hỏi lại. Các lượt tiếp theo chỉ được thấy những công cụ phù hợp với mục đích đã xác định.
- `reply_to_user` chỉ là đầu ra hội thoại trực tiếp: không chạy công cụ nghiệp vụ, không tăng bộ đếm công cụ và không được dùng cho câu hỏi cần đọc lịch cá nhân.
- Các câu hỏi đơn giản chỉ được kết thúc sớm khi lượt mô hình chọn một thao tác; nhiều thao tác đọc độc lập vẫn phải được xử lý đầy đủ.
- Lịch đã đọc được dùng lại trong cùng lượt và cùng revision. Không cache dữ liệu lịch giữa tài khoản hoặc giữa các lượt có thể đã thay đổi. Thử phương án không đọc lại phạm vi đã có đầy đủ dữ liệu.
- Yêu cầu sửa có mã task hoặc tên đầy đủ được cung cấp sẵn tối đa 8 task khớp đã xác minh quyền sở hữu. Không cần gọi lại công cụ chỉ để tìm lại chính mã đã biết; nhiều kết quả khớp vẫn phải làm rõ.
- Một thao tác đọc lặp y hệt được dùng lại kết quả; nếu mô hình tiếp tục lặp thay vì tiến tới câu trả lời, lượt mới có diễn giải mục đích sẽ dừng sớm, không đốt hết số lượt gọi AI.
- Không tự nới deadline, thay thời lượng, ghi lịch từ câu hỏi tư vấn hoặc bỏ kiểm tra sở hữu, revision, dữ liệu nguồn, phiên khóa/đang làm, giờ nghỉ và xác nhận dời lịch.

Các bộ tích hợp cũ chưa hỗ trợ diễn giải có thể giữ giao thức cũ. Nhánh Workflow không dùng giao thức Agent mới. Không cần migration database. Thời gian thực tế vẫn phụ thuộc nhà cung cấp AI, kích thước ảnh, độ dài câu trả lời và độ phức tạp của yêu cầu; giảm số lượt không bảo đảm một số giây cố định.

Lần đo sau khi rút gọn, với 3.8 Flash và dữ liệu giả: chào hỏi 4,9 giây; tư vấn 13,1 giây; giải thích 6,1 giây; hỏi làm rõ 5,7 giây; xem lịch 4,7 giây; tạo task xem trước 5,0 giây. Cả 6 tình huống đều hoàn tất với 1 lượt mô hình; ba tình huống hội thoại/tư vấn không dùng công cụ nghiệp vụ. Đây là một lần đo minh họa, không phải cam kết tốc độ hoặc phép so sánh hiệu năng trước/sau.

## Xác minh

Kiểm thử gồm hội thoại/tư vấn không đổi lịch, hỏi trước khi đọc dữ liệu nếu thiếu chi tiết, truy vấn lịch/task một lượt, tạo phương án một lượt, chặn thao tác sai mục đích, không bịa câu trả lời lịch, giới hạn lặp và bảo toàn nội dung ký của provider. Kiểm tra thực tế tùy chọn `NoraAssistantLiveTests` dùng 3.8 Flash cùng dữ liệu giả trong bộ nhớ; số lượt và thời gian được ghi vào `artifacts/nora-assistant-live-measurements.json`, không dùng lịch thật.
