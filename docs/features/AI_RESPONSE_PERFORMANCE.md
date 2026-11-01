# Tối ưu thời gian phản hồi NORA

Đã rà soát vòng Agent, gọi Vertex/Gemini, trích xuất Notes, đọc ảnh thời khóa biểu, đề xuất sinh hoạt và các bước kiểm tra lịch. Log ngày 01/11/2026 ghi nhận lượt ảnh mất 42.4 giây ở lần gọi model đầu chỉ để đọc preferences/calendar; công cụ thực thi mất 0–3 ms. Lần gọi model tiếp theo chạm timeout 45 giây. Một yêu cầu khác dùng bốn lượt model mất khoảng 55 giây, trong khi mô phỏng lịch chỉ khoảng 40 ms.

## Thay đổi

- Theo lựa chọn của người dùng, cấu hình mặc định tiếp tục dùng `gemini-3.8-flash` với `LOW` thinking và ngân sách output lớn để tránh cắt JSON. Giữ các đường xử lý gộp giảm số vòng gọi AI. Flash-Lite đã được thử nghiệm như phương án so sánh, không phải model đang chạy. Không tăng timeout để che độ trễ.
- Công cụ `propose_changes` gộp chuẩn bị, chuẩn hóa, đọc đủ lịch, mô phỏng và chọn phương án khả thi thành một vòng model. Dùng lại cùng validation và planner của luồng nhiều bước. Kết quả vẫn là đề xuất; quyền tự lưu/xác nhận theo cơ chế sẵn có.
- `answer_calendar` và `answer_free_time` đọc dữ liệu thật và trả câu trả lời do backend tính trong một vòng. Không để model viết lại giờ và khoảng trống đã tính.
- Các công cụ kết thúc/gộp phải được gọi riêng. Phương án không khả thi trả thiếu hụt cho model sửa, hỏi người dùng hoặc dừng; không bỏ qua deadline, thời lượng, giờ rảnh, lịch cố định, các phiên bảo vệ hay phạm vi tài khoản.
- Hướng dẫn model ưu tiên đường gộp, hỏi ngay khi thiếu thông tin thiết yếu, tránh đọc và đánh giá lặp. Schema giới hạn horizon 1–30 để giảm vòng sửa tham số.
- Model mặc định cũng áp dụng cho Notes, phân tích sinh hoạt, đọc ảnh, hỗ trợ task nhóm và các yêu cầu trích xuất hiện có. Notes giữ nguyên giao diện.

## Kết quả đo

Thử nghiệm so sánh dùng Vertex thật với **Gemini 3.1 Flash-Lite**, dữ liệu giả và database SQLite riêng trong bộ test: xem lịch 2.45 s/1 vòng; đề xuất task 4.22 s/2 vòng; ảnh 8 buổi 3.48 s/1 vòng; Notes 1.61 s; chỉnh giờ sinh hoạt 1.08 s. Lượt trước ảnh có thể mất 6.48 s khi model phải sửa một tham số. Các số này không đại diện tốc độ Gemini 3.8 Flash hiện đang được chọn. Đây là số đo mẫu, không phải bảo đảm SLA; ảnh nhỏ/mờ, yêu cầu nhiều điều kiện, tải provider, mạng và vòng xác nhận có thể tăng thời gian.

`AiLatencyLiveTests` kiểm tra đủ 8 buổi từ ảnh giả, thời lượng task, deadline, lịch cố định, đề xuất chưa tự ghi dữ liệu, Notes và sinh hoạt. Bật `PLANORA_LIVE_LATENCY_TESTS=1` và `PLANORA_REPOSITORY_ROOT=<repo>` để chạy; mặc định bỏ qua để không gọi dịch vụ tính phí. Có thể chọn model đo bằng `PLANORA_LATENCY_MODEL` mà không đổi cấu hình máy chủ. Ảnh mẫu ở `tests/SPWPOS.UnitTests/Fixtures/ai-latency-timetable.png`. Kết quả cục bộ ở `artifacts/ai-latency-live-measurements.json`.

Kiểm thử deterministic của Agent bao gồm đường một vòng, vẫn giữ bản xem trước, không tạo giờ rảnh giả, không thực hiện yêu cầu chỉ đọc và bảo vệ lịch hiện có. Các API, giới hạn lượt, kiểm soát phiên bản và xác nhận/hoàn tác giữ nguyên.

Tài liệu provider: [Thinking levels](https://ai.google.dev/gemini-api/docs/generate-content/thinking), [Gemini 3.1 Flash-Lite](https://docs.cloud.google.com/gemini-enterprise-agent-platform/models/gemini/3-1-flash-lite).
