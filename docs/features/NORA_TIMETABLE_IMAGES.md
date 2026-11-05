# Đọc thời khóa biểu từ ảnh trong NORA

## Cách dùng

1. Trong ô chat NORA, **dán ảnh đã sao chép bằng Ctrl + V** hoặc bấm biểu tượng ảnh để chọn tối đa **2 tệp PNG, JPG hoặc WebP**, tối đa 5 MB mỗi ảnh. Có thể chọn cả hai cùng lúc bằng Ctrl hoặc chọn/dán thêm ảnh thứ hai; từng ảnh có nút gỡ riêng. Ảnh dán xuất hiện trong bản xem trước như ảnh chọn từ máy; văn bản dán thông thường vẫn hoạt động.
2. Có thể ghi thêm ngày bắt đầu, số tuần học hoặc cách quy đổi số tiết sang giờ thực tế.
3. Bấm **Gửi**. Model thị giác Gemini/Google Cloud đang cấu hình nhận trực tiếp **cả hai ảnh và lời nhắn trong cùng một yêu cầu lập lịch**. NORA gộp các lịch bận, đối chiếu buổi trùng và hỏi lại dữ liệu mâu thuẫn/không đọc rõ trước khi lưu. Không có bước OCR riêng hoặc ghép văn bản nhận diện vào lời nhắn. Ảnh xuất hiện trong bong bóng tin nhắn và vẫn xem được sau khi tải lại trang; bấm ảnh để phóng to.
4. **Tự động lưu** bật: lịch hợp lệ được lưu ngay theo cơ chế chat hiện có. Tắt công tắc này nếu muốn xem phương án trước. Các trường hợp cần xác nhận dời lịch vẫn theo quy tắc hiện có.

Chọn hoặc dán ảnh chỉ tạo bản xem trước tại máy. Nội dung ảnh chỉ được gửi tới dịch vụ AI khi bấm Gửi. Có thể gỡ ảnh trước khi gửi hoặc bấm Dừng khi đang đọc. Gửi lại sau lỗi dùng cùng ảnh và mã yêu cầu để tránh tạo lịch hai lần. Khi NORA hỏi thêm ngày áp dụng hoặc giờ theo tiết, câu trả lời bổ sung được gửi cùng ảnh của yêu cầu đang chờ, không cần dán lại ảnh.

Nếu ảnh chưa rõ, thiếu ngày áp dụng, thiếu số tuần lặp hoặc chỉ ghi “tiết 1–3” mà không có bảng giờ theo tiết, NORA cần hỏi thêm. Không tự đoán một tiết dài bao nhiêu phút, ngày bắt đầu học kỳ hoặc lược bỏ buổi học để làm cho lịch vừa giới hạn. Ảnh cần được chụp đủ tiêu đề hàng/cột; giờ và môn học được giữ theo dữ liệu đọc được.

Khi một buổi học trùng với phiên công việc chưa khóa và chưa bắt đầu, NORA giữ giờ học và lập lại phần công việc bị ảnh hưởng trong các ràng buộc hiện có. Không tự dời lịch hẹn cố định, phiên đã khóa hoặc đang thực hiện. Nếu không thể hoàn thành việc còn lại đúng hạn, yêu cầu không được lưu một phần.

Nếu ngày bắt đầu thời khóa biểu đã qua, NORA chỉ nhập phần còn lại từ thời điểm hiện tại theo múi giờ tài khoản: bỏ các buổi đã kết thúc, cắt buổi đang diễn ra từ hiện tại đến giờ kết thúc gốc, giữ đúng giờ các buổi sau. Các lần lặp được tạo theo khoảng gốc rồi mới cắt, nên không dời buổi đã lỡ hoặc kéo dài số tuần để bù. Ví dụ “áp dụng từ tuần này trong 1 tuần” giữ phần còn lại của tuần thứ Hai–Chủ nhật hiện tại. Nếu toàn bộ lịch đã hết, NORA báo không còn buổi cần thêm, không tạo lịch mới.

Lời nhắn tối đa 2.000 ký tự; mỗi ảnh có giới hạn riêng 5 MB (tối đa 2 ảnh), không chiếm số ký tự lời nhắn. Tổng cộng trên cả hai ảnh tối đa 12 khung lớp khác nhau, mỗi khung có thể lặp tối đa 12 tuần; bảng dài hơn cần chia ảnh thành các phần. Lớp học là sự kiện `CLASS` chiếm giờ trong lịch, không tự biến thành giờ rảnh hoặc công việc linh hoạt.

Khi chưa có giờ làm/nghỉ đã xác nhận, NORA đề xuất lịch sinh hoạt và các khoảng rảnh để bạn xem và áp dụng. Sau khi xác nhận, các công việc có thời lượng và deadline được xếp trong giờ rảnh quanh cả hai thời khóa biểu; không coi mọi ô trống là giờ làm việc.

## Triển khai

- `GET /api/agent/capabilities` trả thêm `imageAvailable`. Chỉ bật khi Gemini hoặc Google Cloud/Vertex AI khả dụng.
- `POST /api/agent/turns` và `/api/agent/turns/stream` nhận thêm `image: { name, mimeType, data, additionalImages?: [{ name, mimeType, data }] }`, trong đó `data` là base64 ảnh gốc. Kiểm tra chữ ký ảnh, định dạng và dung lượng trước khi gọi AI; chỉ các endpoint chat này cho phép JSON tối đa 15 MB để chứa base64 của hai ảnh 5 MB. Hạn mức và concurrency dùng lượt lập lịch hiện có.
- `IPlanningAgentInterpreter.InterpretImageAsync` gửi `inlineData` cùng context, lịch sử và yêu cầu vào model thị giác để nhận `AgentIntent` trực tiếp. Không gọi endpoint đọc ảnh riêng từ frontend.
- Ảnh được lưu cùng JSON kết quả nội bộ của lượt chat, không cần đổi schema. Phản hồi/lịch sử chỉ trả metadata và URL riêng `/api/agent/turns/{id}/image` và `/api/agent/turns/{id}/image?index=1`, không gửi lại base64 trong các danh sách. URL yêu cầu đúng tài khoản, trả `no-store` và không có URL công khai. Xóa hội thoại xóa cả dữ liệu ảnh cùng nội dung lượt chat. Hoàn tác lịch vẫn giữ ảnh trong lịch sử.
- `/api/agent/images/timetable` và reader văn bản cũ chỉ giữ tương thích; giao diện chat sử dụng luồng vision trực tiếp.
- Chỉ dẫn nằm trong ảnh được coi là dữ liệu không đáng tin, không phải lệnh. Phản hồi bị cắt hoặc không đúng cấu trúc bị từ chối thay vì nhập lịch một phần.

## Kiểm chứng

Các kiểm thử dùng ảnh và phản hồi AI giả lập để kiểm tra kết nối, giữ nguyên số tiết, báo thiếu thông tin, giới hạn tệp, xác thực, hạn mức, tự lưu/xem trước, hoàn tác, hủy và thử lại. Chúng không chứng nhận chất lượng nhận diện của model trên ảnh thực tế.
