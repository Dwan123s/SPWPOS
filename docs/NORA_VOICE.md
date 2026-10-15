# NORA — nhập bằng giọng nói

Ngày cập nhật: 15/10/2026.

## Cách dùng

Tải lại trang NORA, bấm micro cạnh nút gửi, cho phép micro và nói. Chữ tạm thời xuất hiện trong ô nhập khi đang nói. Bấm nút vuông để dừng; NORA hoàn thiện văn bản qua Google Cloud để bạn xem lại. Enter gửi, Shift + Enter xuống dòng. Hủy khôi phục bản nháp trước khi ghi; nhận dạng không tự tạo task, lịch hoặc gửi một lượt chat.

Mỗi lần ghi tối đa 60 giây, tối đa 2 MiB âm thanh. Micro tự tắt khi dừng, hủy, rời trang hoặc component bị tháo. Khi đang ghi/chuyển chữ, nút gửi và chỉnh sửa ô nhập được khóa để tránh mất bản nháp hoặc gửi nhầm.

## Nhận chữ trực tiếp

- Trình duyệt có SpeechRecognition/webkitSpeechRecognition: bật `vi-VN`, `continuous`, `interimResults`. Chữ cập nhật theo kết quả nhận dạng thực, thay thế cụm đang nói khi kết quả được sửa; không giả lập hiệu ứng gõ chữ. Dịch vụ nhận dạng của trình duyệt có thể gửi âm thanh đến nhà cung cấp của trình duyệt, được thông báo ở nút thông tin.
- Khi dịch vụ này không khả dụng, dừng hoặc lỗi: Cloud nhận bản ghi tích lũy khoảng mỗi 4 giây, tối đa một request preview đồng thời. Tốc độ hiển thị phụ thuộc mạng/provider, cập nhật theo cụm lời nói. Với định dạng chưa giải mã được trước khi ghi hoàn tất, bước cuối vẫn xử lý bản ghi đầy đủ.
- Khi dừng, mọi nhận dạng/preview đang chạy được hủy. Kết quả Cloud cuối cùng kiểm tra và thay thế bản tạm. Kết quả đến muộn không được ghi đè bản cuối, bản nháp đã hủy hoặc hội thoại khác.

## Backend và quyền

`POST /api/agent/voice/transcribe` nhận âm thanh nhị phân WebM, Ogg, MP4 hoặc WAV. Endpoint yêu cầu đăng nhập; cookie request dùng cơ chế kiểm tra nguồn và `X-SPWPOS` hiện có. Kiểm tra MIME, chữ ký file và giới hạn 2 MiB, kể cả body không có Content-Length. Hạn riêng 24 request/phút/IP phục vụ preview và bước cuối; hạn chung hiện có vẫn áp dụng.

Gọi `generateContent` với inline audio qua Gemini/VertexAI đang cấu hình, dùng cùng Cloud key hoặc ADC ở server. Không gửi lịch, task, hồ sơ hoặc cookie đến model để chép lời. Không lưu audio vào database, file upload tạm hoặc log. Bản ghi chỉ được xử lý trong bộ nhớ; văn bản được đưa về ô nhập và chỉ trở thành nội dung chat khi người dùng gửi.

Model trả JSON `text`, không có hành động; phản hồi phải hoàn chỉnh, đúng schema, tối đa 2000 ký tự. Silence/không nghe rõ, lỗi quyền, quota và timeout được thông báo; không tự đổi key hoặc provider. Đây là tính năng dùng API hiện có, có thể tiêu thụ quota/credit, đặc biệt khi dùng preview Cloud.

Header microphone được đổi từ chặn hoàn toàn sang `microphone=(self)` để chỉ trang cùng nguồn có thể xin phép. CSP vẫn giữ nguyên, không mở kết nối AI trực tiếp từ browser hoặc cho phép iframe bên ngoài dùng micro.

## Kiểm chứng

- Release build: 0 warning, 0 error.
- 209 backend unit tests đạt; 106 integration tests gồm 96 đạt và 10 SQL Server tests bỏ qua do chưa có `SPWPOS_TEST_SQLSERVER`.
- Sau cập nhật live preview/hạn riêng, 25 kiểm tra frontend chat/voice và 2 API voice tests đều đạt. Bộ frontend trước bước live preview gồm 197 bài đều đạt.
- Trình duyệt Edge với thiết bị âm thanh tổng hợp kiểm tra 1440, 390, 320px: MediaRecorder thật, WebM hợp lệ, chữ xuất hiện trước khi dừng ở phương án Cloud dự phòng, giữ bản nháp, bản cuối để xem lại, hủy không gửi chat, không tràn màn hình và CSP.
- Cloud thật: WAV tổng hợp được chép thành 81 ký tự trong 2107ms; đoạn WebM tích lũy đang ghi được chép thành 53 ký tự trong 1834ms. Không dùng micro của người dùng hoặc ghi dữ liệu lịch trong các kiểm tra này. Độ chính xác trên micro và tiếng nói thật phụ thuộc âm thanh và cần được kiểm tra khi người dùng cấp quyền.
- Kết quả/ảnh: `artifacts/nora-voice-browser-results.json`, `artifacts/nora-voice-live-*.png`, `artifacts/nora-voice-review-*.png`.

Kiểm tra Cloud riêng bằng bản ghi minh họa, từ `src/SPWPOS.Api` với môi trường Development/User Secrets hiện có: `SPWPOS.Api --check-ai-voice --voice-file <absolute-path.wav>`. Chỉ in metadata, không in bản chép lời, credential hoặc provider body. Không chạy tự động khi khởi động server.

Nguồn: [Google Cloud audio understanding](https://docs.cloud.google.com/gemini-enterprise-agent-platform/models/capabilities/audio-understanding), [SpeechRecognition](https://developer.mozilla.org/en-US/docs/Web/API/SpeechRecognition), [MediaRecorder.start](https://developer.mozilla.org/en-US/docs/Web/API/MediaRecorder/start).
