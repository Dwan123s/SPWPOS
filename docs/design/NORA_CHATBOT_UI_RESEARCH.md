# NORA — khảo sát UI và nâng cấp giao diện trợ lý

Ngày khảo sát: 15/10/2026, giờ Việt Nam.

Phạm vi: 10 hệ trợ lý đại diện từ nhiều nhà cung cấp, qua trang sản phẩm và tài liệu chính thức công khai. Không thể khảo sát toàn bộ chatbot trên thế giới; tài khoản đăng nhập, gói dịch vụ và thử nghiệm A/B có thể có UI khác. Bảng dưới là tổng hợp và suy luận thiết kế cho PLANORA, không khẳng định đã thử mọi chức năng hoặc giao diện riêng tư của các sản phẩm.

## Nguồn và lựa chọn

| Sản phẩm | Nguồn công khai | Điều tham khảo cho NORA |
|---|---|---|
| ChatGPT | [Trang chat](https://chatgpt.com/), [Canvas](https://openai.com/index/introducing-canvas/) | Nội dung hội thoại và ô nhập là trọng tâm; tìm/lịch sử ở cạnh; đầu ra lớn có vùng làm việc riêng. |
| Gemini | [Canvas](https://blog.google/products-and-platforms/products/gemini/gemini-collaboration-features/) | Lời chào dễ tiếp cận, gợi ý khởi đầu và kết quả cạnh cuộc trò chuyện. |
| Claude | [Artifacts](https://www.anthropic.com/news/artifacts), [Hướng dẫn artifacts](https://support.anthropic.com/en/articles/9487310-what-are-artifacts-and-how-do-i-use-them) | Kết quả độc lập có thể mở để xem chi tiết; nội dung chat được trình bày dễ đọc. |
| Copilot | [Conversation history](https://support.microsoft.com/en-us/microsoft-copilot/conversation-history-in-microsoft-copilot) | Lịch sử bên trái, tiếp tục hội thoại và mở/đóng thanh bên. |
| Perplexity | [Pro Search](https://www.perplexity.ai/help-center/en/articles/10352903-what-is-pro-search) | Minh bạch nguồn, câu trả lời có cấu trúc và tiếp tục hỏi trong ngữ cảnh. NORA dùng dữ liệu lịch thực; không thêm nguồn web giả. |
| Grok | [Thiết kế Grok Bot](https://x.ai/news/designing-grok-bot) | Dòng hội thoại có thể kết hợp thông điệp, trạng thái và kết quả công việc. NORA hiển thị giai đoạn thực và thẻ kế hoạch. |
| Mistral Le Chat / Vibe | [Files and Canvas](https://docs.mistral.ai/vibe/work/files-and-canvas) | Xem và làm việc với kết quả ngay cạnh chat. Tên sản phẩm có thể thay đổi; nguồn Vibe hiện hành được đọc cùng tài liệu Le Chat trước đó. |
| Poe | [FAQ](https://help.poe.com/hc/en-us/articles/19944206309524-Poe-FAQs) | Phân biệt hội thoại và khả năng của bot. NORA duy trì một trợ lý rõ ràng; các điều khiển có hành vi thực. |
| DeepSeek | [DeepSeek app/history screens](https://www.deepseek.com/en/download/) | Đối chiếu bổ sung về chat/lịch sử qua trang giới thiệu công khai. |
| Meta AI | [Voice app](https://about.fb.com/news/2025/04/introducing-meta-ai-app-new-way-access-ai-assistant/) | Bắt đầu voice bằng một nút, thuận tiện khi đang làm việc khác. |

Le Chat/Vibe được tính chung một dòng sản phẩm. Các quyết định cốt lõi được xây dựng từ ChatGPT, Gemini, Claude, Copilot và Perplexity; những nguồn còn lại bổ sung về trạng thái, voice và tổ chức hội thoại.

## Thiết kế đã thực hiện

Thanh điều hướng PLANORA thành dải biểu tượng 66px khi ở trang trợ lý. Thanh lịch sử riêng rộng 230px, thu gọn để tăng chỗ đọc chat. Màn hình nhỏ mở lịch sử thành bảng trượt. Header NORA thay toolbar lặp lại; menu ứng dụng trên điện thoại vẫn mở được và Escape trả focus đúng nút.

Trang chào giữ mascot cú xanh của PLANORA, với bốn gợi ý có hành vi thực: họp, deadline, giờ rảnh và xem lịch hôm nay. Bấm gợi ý chỉ điền ô nhập. Ô chat giữ thiết kế gọn, Enter gửi / Shift + Enter xuống dòng, voice nhận chữ trực tiếp và chế độ tự lưu hiện có.

Lịch sử được nhóm theo ngày của múi giờ tài khoản, có số lượt và tìm trong cả yêu cầu lẫn câu trả lời tiếp theo. Không tự đặt ngày cho dữ liệu thiếu timestamp. Chọn hội thoại đọc lại dữ liệu thực, không gọi AI.

Phản hồi hỗ trợ phần Markdown giới hạn: tiêu đề, đoạn văn, danh sách, chữ đậm, code và link an toàn. HTML thô luôn là chữ; không dùng `dangerouslySetInnerHTML`. Chỉ HTTP/HTTPS và các route PLANORA đã biết được chuyển thành link. Phần streaming vẫn lấy delta thật từ backend.

Kế hoạch có thẻ mở chi tiết trong tin nhắn. Bảng bên phải chia các phiên theo ngày, hiển thị giờ bắt đầu/kết thúc, tên task và số phút. Bấm kế hoạch của tin nhắn cũ mở đúng kế hoạch đó, không tự đổi thành kế hoạch mới nhất. Xem trước, áp dụng và hoàn tác giữ nguyên backend hiện có.

Thinking chỉ trình bày các giai đoạn công khai do backend phát ra, không có suy luận nội bộ hay phần trăm giả. Mascot tiếp tục đi bộ trên đường mảnh khi xử lý.

## Chuyển động

- Mở/thu gọn lịch sử và thay đổi chiều rộng vùng chat trong khoảng 300ms.
- Lịch sử trên điện thoại trượt ra/vào trong khoảng 280ms.
- Bảng kế hoạch vào nhẹ từ bên phải; tin nhắn mới hiện trong khoảng 350ms.
- Các gợi ý xuất hiện lần lượt, nâng nhẹ khi hover; nút có phản hồi khi nhấn.
- Mascot lời chào dịch chuyển nhẹ theo chu kỳ; mascot xử lý dùng 8 frame đi bộ thực.
- Thinking có ánh sáng nhẹ; voice có chỉ báo ghi và vòng nhấn nhẹ quanh micro.
- Nút tạm dừng và `prefers-reduced-motion` vẫn hoạt động. Hiệu ứng mở đầu đã hoàn tất không bị chạy lại chỉ vì resume.

Animation phục vụ nhận biết trạng thái và thay đổi bố cục. Cập nhật chữ streaming không tái chạy animation toàn bộ tin nhắn.

## Kiểm chứng và tài liệu liên quan

Build frontend production đạt. Kiểm tra NORA gồm định dạng an toàn, nhóm lịch sử, chọn kế hoạch cũ, gửi/dừng/retry, voice, preview/apply/undo, keyboard và pause/reduced motion. Browser kiểm tra ở 1440, 1024, 390, 320px với dữ liệu minh họa và SSE có độ trễ thực. Voice dùng thiết bị tổng hợp trên Edge ở 1440, 390, 320px. Không ghi dữ liệu lịch thật trong kiểm tra UI.

Ảnh: `artifacts/nora-professional-empty-*.png`, `artifacts/nora-professional-chat-*.png`, `artifacts/nora-professional-plan-*.png`. Demo chuyển động: `artifacts/nora-redesign-motion-demo.webm`. Metadata kiểm tra: `artifacts/nora-streaming-browser-results.json`, `artifacts/nora-voice-browser-results.json`.

Xem thêm [streaming](NORA_CHAT_RESEARCH.md), [voice](../features/NORA_VOICE.md), [asset/prompt mascot](NORA_MASCOT_FRAMES.md).
