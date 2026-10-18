# NORA: chat, streaming và tiến trình xử lý

Ngày cập nhật: 15/10/2026.

Bản nâng cấp bố cục và animation mới nhất dựa trên 10 trợ lý đại diện: [khảo sát UI](NORA_CHATBOT_UI_RESEARCH.md).

## Tham khảo

Đây là các nguồn đại diện, không phải khảo sát mọi website. Các lựa chọn dưới đây là quyết định thiết kế cho PLANORA, không khẳng định NORA có toàn bộ tính năng của sản phẩm tham khảo.

- [ChatGPT](https://chatgpt.com/): ưu tiên hội thoại, lời chào đơn giản, ô nhập nổi bật; lịch sử và tìm hội thoại nằm bên cạnh.
- [Gemini Canvas](https://blog.google/products-and-platforms/products/gemini/gemini-collaboration-features/): kết quả có thể mở trong vùng làm việc cạnh chat. Với NORA, vùng này là kế hoạch thực tế vừa tạo, có thể đóng/mở bằng nút Kế hoạch.
- [Claude Artifacts](https://support.claude.com/en/articles/17153992-what-are-artifacts-and-how-do-i-use-them): tham khảo cách đặt kết quả cạnh hội thoại.
- [Conversation](https://elements.ai-sdk.dev/components/conversation), [Task](https://elements.ai-sdk.dev/components/task), [Prompt Input](https://elements.ai-sdk.dev/components/prompt-input): tham khảo hành vi cuộn, tiến trình có thể thu gọn và ô nhập. Không thêm thư viện AI Elements vào dự án.

## Những lựa chọn đã triển khai

Nền sáng, chữ hội thoại dễ đọc, chiều rộng nội dung tối đa 760px. Lịch sử có tìm kiếm; màn hình nhỏ mở thành bảng trượt và đóng bằng Escape. Ô nhập ở đáy vùng chat; Enter gửi, Shift + Enter xuống dòng; không gửi khi đang nhập IME. Gợi ý chỉ điền nội dung, không tự gọi AI.

Ô nhập mặc định một dòng, tự giãn khi nội dung dài. Hàng dưới chỉ có tự lưu, nút thông tin và gửi/dừng. Hướng dẫn, bộ đếm và mô tả dữ liệu nằm trong nút thông tin, đóng bằng Escape.

Mascot NORA nhỏ trong lời chào và cạnh phản hồi. Khi xử lý, mascot chạy chu kỳ đi bộ 8 frame trên đường mảnh; có tạm dừng và hỗ trợ reduced motion. Bảng mascot lớn và thống kê lịch được bỏ khỏi vùng chat để dành không gian cho cuộc trò chuyện.

Thinking là trạng thái xử lý công khai: đọc lịch, hiểu yêu cầu, kiểm tra kế hoạch, lưu lịch. Thẻ nhỏ có ánh sáng nhẹ trên nhãn, thời gian đã trôi qua, số bước hoàn tất và nút mở chi tiết. Mặc định thu gọn. Không hiển thị suy luận nội bộ của model, không giả lập phần trăm tiến độ hoặc tốc độ gõ.

## Streaming thật

`POST /api/agent/turns/stream` trả SSE: `phase`, `delta`, `result`, `error`. VertexAI/Gemini dùng `streamGenerateContent` và `alt=sse`; key chỉ sử dụng ở backend. Chỉ phần `reply` công khai được chuyển dần đến trình duyệt. Phần JSON hành động, thought/signature của provider và credential không gửi ra chat.

Planner chỉ nhận intent sau khi toàn bộ phản hồi hoàn tất và schema hợp lệ. Các nút áp dụng/hoàn tác xuất hiện trên kết quả cuối cùng. Nút Dừng hủy kết nối và giữ mã yêu cầu khi gửi lại để chống lưu trùng; bước đang lưu sẽ khóa nút Dừng. Khi kết nối bị ngắt, cần kiểm tra lịch sử vì yêu cầu có thể đã được lưu. Không tự động chuyển provider hoặc tự gửi lại một yêu cầu mới.

Cuộn tự động khi người dùng đang ở cuối chat. Khi đang đọc tin cũ, giao diện giữ vị trí và hiện nút xuống cuối. Có sao chép phản hồi và chỉnh sửa yêu cầu để gửi lại. Quyền sở hữu dữ liệu, revision, hạn preview và idempotency của backend được giữ nguyên.

## Kiểm chứng

- Release backend build thành công; 204 unit tests đã đạt.
- 104 integration tests: 94 đạt, 10 bài SQL Server được bỏ qua vì chưa có `SPWPOS_TEST_SQLSERVER`.
- Kiểm tra VertexAI thật với yêu cầu mẫu không ghi lịch: 3 reply chunks, chunk đầu 1231ms, hoàn tất 1790ms; schema và thời gian cuộc họp được xác nhận.
- Trình duyệt kiểm tra ở 1440, 1024, 390, 320px với server SSE có độ trễ thực và dữ liệu minh họa: nội dung đến trước kết quả, hoạt ảnh đổi frame, xem trước/áp dụng/hoàn tác, bảng kế hoạch, lịch sử, dừng/gửi lại, pause/reduced motion và CSP. Không gọi AI hoặc ghi dữ liệu thật trong bước kiểm tra giao diện.
- Kết quả và ảnh kiểm tra: `artifacts/nora-streaming-browser-results.json`, `artifacts/nora-professional-*.png`.

Asset và prompt gốc: [NORA_MASCOT_FRAMES.md](NORA_MASCOT_FRAMES.md).

Nhập bằng micro, chữ trực tiếp khi nói và kiểm chứng Google Cloud: [NORA_VOICE.md](../features/NORA_VOICE.md).
