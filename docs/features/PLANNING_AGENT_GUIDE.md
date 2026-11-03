# NORA — Trợ lý AI lập lịch

Từ bản nâng cấp 31/10/2026, chat NORA trên Gemini/Vertex AI có vòng gọi công cụ do model điều phối. Xem [runtime Agent, cấu hình, giới hạn và kịch bản kiểm thử](NORA_AGENT_RUNTIME.md). Chế độ `Workflow` vẫn có thể được chọn trong cấu hình.

Từ 04/11/2026, Agent [xác định mục đích trước khi dùng công cụ](NORA_ASSISTANT_FIRST.md): trò chuyện/tư vấn được trả lời trực tiếp, thiếu chi tiết thiết yếu thì hỏi trước, truy vấn lịch đơn giản kết thúc sau dữ liệu đã xác minh. Không thêm lượt gọi mô hình chỉ để phân loại, không buộc người dùng xác nhận lại yêu cầu đã rõ. “Chỉ xem trước” tạo phương án để xem, khác với yêu cầu chỉ đọc.

## Tiếng Việt gõ Telex chưa chuyển dấu

NORA hỗ trợ đọc các từ Telex thông dụng trong yêu cầu lập lịch, kể cả khi trộn với chữ đã có dấu hoặc gõ lặp phím dấu:

- `lamf`, `vieejc` → làm, việc.
- `vieets`, `vieetss` → viết.
- `45 phuts, xong truowsc 21h ngayf mai` → 45 phút, xong trước 21h ngày mai.
- `ddungwf tuwj ddoongj luuw` → đừng tự động lưu; yêu cầu xem trước vẫn không tự áp dụng.

Tin nhắn trong hội thoại giữ nguyên nội dung đã gửi. Backend cung cấp cách đọc Telex cho AI và dùng cùng cách đọc khi kiểm tra thời lượng, deadline, giờ trong ngày và tên task. Số liệu, ID, địa chỉ email, URL và đoạn mã trong backtick được giữ nguyên.

Đây là hỗ trợ diễn giải, không phải bộ sửa chính tả cho mọi từ hoặc bộ gõ đầy đủ. Từ không nhận diện được vẫn được gửi nguyên bản cho AI; nếu ngày giờ, thời lượng hoặc ý định còn mơ hồ, NORA cần hỏi lại. Thay đổi backend cần build và khởi động lại API để có hiệu lực.

## Giao diện NORA — 15/10/2026

Menu **NORA · Trợ lý AI** dùng cùng phong cách kính xanh, gradient, ánh sáng và Web Animations API của login/getting started. Linh vật cú xanh hiện có là NORA; dùng chung MascotEyes để nhìn theo con trỏ. NORA có trạng thái sẵn sàng/đang xử lý, nút tạm dừng chuyển động lưu trên máy, hỗ trợ prefers-reduced-motion và kế thừa thiết lập tạm dừng của workspace. Bản mobile giữ mascot ở đầu cuộc trò chuyện.

Ba gợi ý mẫu chỉ điền và focus ô nhập; không gọi AI hoặc lưu lịch cho đến khi gửi. Ctrl/Command + Enter gửi yêu cầu, Enter xuống dòng; không gửi khi bộ gõ đang composition. Giữ nguyên lịch sử, chế độ tự lưu/xem trước, câu hỏi làm rõ, áp dụng, hoàn tác và request ID khi thử lại. API và quy tắc xếp lịch không thay đổi.

Đã build frontend và xác nhận backend đang phục vụ entry mới HTTP 200. 182 kiểm thử frontend và 4 kiểm thử phiên đăng nhập đạt; sau thay đổi cuối, 12 kiểm thử agent/getting started được chạy lại và đạt. Kiểm tra trình duyệt bằng dữ liệu giả ở 1440/1024/390/320px: không tràn ngang, gợi ý không tự gửi, preview/apply/undo, CSP, mắt mascot, tạm dừng và giảm chuyển động đều đạt. Ảnh tham khảo nằm ở artifacts/nora-empty-1440.png và artifacts/nora-empty-390.png; các ảnh dùng dữ liệu minh họa, không phải lịch thật.

## Tính năng đã triển khai

- Trang **Trợ lý AI**: hội thoại tiếng Việt có lịch sử theo tài khoản, tiếp tục hội thoại và tạo hội thoại mới.
- Nhập/sửa lịch rảnh tuần, thêm/sửa khoảng rảnh hoặc bận theo ngày, thêm/sửa một sự kiện cố định, tạo/cập nhật task và xếp lịch.
- Sự kiện lặp hàng tuần có số lần cụ thể, tối đa 12; từng lần có thể sửa/xóa riêng bằng form. Chat cần số lần lặp rõ ràng.
- **Tự lưu khi lịch khả thi** mặc định bật. Tắt để xem trước: chưa tạo task, sự kiện, ngoại lệ hoặc phiên thật. Bấm **Áp dụng yêu cầu này** để lưu cùng nhau.
- Kiểm tra giờ rảnh, khoảng bận, sự kiện, thời gian nghỉ, deadline cứng, phụ thuộc, phiên khóa/đang thực hiện và giới hạn phút mỗi ngày.
- **Hoàn tác yêu cầu** khôi phục thay đổi gần nhất khi revision chưa đổi và các phiên/sự kiện chưa bắt đầu. Lưu lịch sử phiên bị thay thế/hoàn tác bằng trạng thái Cancelled.
- Thử lại cùng request ID trả lại kết quả hoặc tiếp tục áp dụng bản xem trước; không tạo công việc, lịch hay sự kiện trùng.

## Cách sử dụng

### Ưu tiên hoàn thành trong cùng ngày — 03/11/2026

Bộ lập lịch xét ngày sớm còn giờ khả dụng trước, sau đó chấm điểm các khoảng giờ trong ngày. Giờ làm việc ưu tiên là sở thích mềm: không chọn buổi sáng ngày sau chỉ vì phần còn lại hôm nay nằm ngoài khung ưu tiên. Một task có thể gồm nhiều phiên trong cùng ngày, vẫn giữ thời gian nghỉ, giờ ăn/ngủ, lịch cố định, phiên được bảo vệ, phụ thuộc, deadline, khung thực hiện riêng và giới hạn phút mỗi ngày. Khi ngày đó không đủ giờ hợp lệ, phần còn lại mới chuyển sang ngày sau.

Ví dụ task 120 phút, bắt đầu xét lúc 16:12, rảnh tới 18:00 và 18:30–22:00, phiên tối đa 90 phút, nghỉ 10 phút: có thể xếp 16:12–17:42 và 18:30–19:00 trong cùng ngày. Quy tắc áp dụng khi tạo/xem lại phương án; không tự sửa các lịch đã lưu. Yêu cầu rõ ngày mai hoặc một khung giờ riêng vẫn được giữ nguyên.

### Nhường giờ cho việc quan trọng gần deadline — 03/11/2026

Khi tạo/cập nhật một công việc quan trọng còn hạn trong 48 giờ, NORA thử giữ nguyên các phiên khác trước. Nếu thiếu thời gian, bộ lập lịch tự tìm các phiên linh hoạt từ thời điểm lập lịch đến deadline thuộc việc có deadline muộn hơn. Không cần gán mức ưu tiên 4 để kích hoạt bước này. AI đọc được mức quan trọng đã lưu; không tự suy ra rằng mọi việc sát hạn đều quan trọng.

Chỉ thử dời phiên chưa bắt đầu, chưa ghi nhận thời gian làm và không khóa. Giữ nguyên lịch cố định, giờ ăn/ngủ/nghỉ, phiên đang làm/tạm dừng, tiền nhiệm, khung thực hiện, giới hạn phiên/ngày và deadline của mọi công việc. Phiên của cùng task nằm ngoài khoảng cần nhường vẫn được giữ. Chỉ nhận phương án cải thiện thiếu hụt mà không làm việc khác thiếu thời gian hơn hoặc trễ hạn hơn; không gia hạn deadline để làm lịch có vẻ khả thi. Mỗi lần kiểm tra tối đa 8 phương án nhường giờ, trong phạm vi lập lịch hiện có.

Ví dụ report SWP tăng từ 120 lên 240 phút, hạn trưa mai; report SSG105 chiếm hai giờ tối nay nhưng hạn thứ Bảy: NORA có thể đề xuất chuyển các phiên SSG105 sang khoảng hợp lệ sau đó để dành chỗ cho SWP. Phương án hiển thị giờ cũ/mới và luôn cần người dùng xác nhận, kể cả bật tự lưu; hỗ trợ hoàn tác. Không tự sửa task chung của nhóm hoặc lịch tài khoản khác. Tắt tự xếp lại hoặc nói “không dời các task khác” thì không tự thử bước nhường giờ.

Thông báo AI đạt hạn mức là lỗi dịch vụ/quota, không phải kết quả kiểm tra thiếu giờ của bộ lập lịch. Bước nhường giờ chạy bằng bộ tính lịch của máy chủ, không cần thêm lượt gọi mô hình. Model hiện có được giữ nguyên.

### Nhập thời khóa biểu tự động — 01/11/2026

Gửi ảnh thời khóa biểu trong NORA hoặc nhập thời gian biểu bằng lời. Khi ngày áp dụng, giờ học và số tuần lặp đã rõ, NORA nhập các buổi học, tự tính các khoảng rảnh và trả báo cáo 7 ngày tính từ buổi học đầu tiên còn hiệu lực. Báo cáo liệt kê lịch bận, từng khoảng rảnh kèm số phút, tổng phút theo ngày và khoảng trống dài nhất. Các phiên linh hoạt bị lịch học đè lên được kiểm tra/xếp lại trước khi lưu.

Thời khóa biểu người dùng cung cấp là dữ liệu gốc. Hệ thống không tự tạo giờ làm việc, giờ ngủ/nghỉ hoặc coi ô trống là giờ rảnh. Khi chưa có giờ có thể làm việc, NORA vẫn nhập lịch cố định đầy đủ, phân tích các giờ bận và hỏi khung sinh hoạt còn thiếu bằng hội thoại. Người dùng trả lời bằng lời để NORA cấu hình giờ rảnh quanh lịch cố định; không cần điền từng khung trong form. Giờ rảnh đã xác nhận được giữ nguyên. Giới hạn phút mỗi ngày, độ dài phiên và thời gian nghỉ vẫn được áp dụng riêng khi xếp công việc.

Chế độ tự lưu mặc định lưu toàn bộ phương án hợp lệ. **Hoàn tác** khôi phục cả lịch học và các phiên bị dời, theo điều kiện hoàn tác hiện hành. Tắt tự lưu hoặc yêu cầu chỉ xem trước thì chưa lưu gì. Nếu thiếu ngày/giờ học, ảnh không rõ hoặc có xung đột không thể giải quyết, hệ thống không lưu từng phần. Không tự đoán giờ theo tiết hoặc thời hạn học kỳ. Với công việc khẩn, NORA dùng thời lượng, deadline và các khoảng rảnh đã xác nhận để tìm chỗ; lịch cố định vẫn được bảo vệ.

Luồng này có trong cả chế độ `Agent` và `Workflow`; không cần migration mới. Cần build frontend và khởi động lại API để dùng thay đổi.

### Giao việc qua hội thoại

Workspace mới công bố capability `secretary-v1`: các màn hình công việc, lịch cố định, giờ rảnh và mô phỏng chuyển thao tác nhập/sửa sang NORA. Trang giờ rảnh trở thành bản xem thời gian biểu và các giờ đã xác nhận. **Notes giữ nguyên giao diện và toàn bộ luồng hiện có**: soạn/lưu bản nháp, phân tích AI, chỉnh kết quả và xem trước/tự lưu lịch. Notes vẫn truy cập được khi workspace chưa có thời gian biểu. Các màn hình lập lịch khác không yêu cầu điền form task, số phút, mức ưu tiên, các khung rảnh, phiên/ngày lập kế hoạch hay sửa thời gian phiên. Theo dõi timer và đánh dấu hoàn thành vẫn dùng nút trực tiếp. Form tài khoản, bảo mật và kết nối không thuộc luồng lập lịch này.

Khi người dùng chỉ cung cấp tên/mô tả công việc, AI có thể **đề xuất** một thời lượng ước lượng. Đề xuất được lưu trong ngữ cảnh làm rõ, chưa tạo task hoặc phiên làm việc. Chỉ sau câu trả lời chấp nhận rõ ràng (ví dụ “Đồng ý”) mới dùng đúng số phút đó cho đúng công việc của yêu cầu đang chờ. Từ chối, câu trả lời không liên quan, đổi tên công việc hoặc số phút khác không tự chấp nhận ước lượng. Deadline và giờ sinh hoạt vẫn cần dữ kiện từ người dùng; không được tự bịa.

### Quy tắc và guardrail — 19/10/2026

- Danh sách loại công việc đang hoạt động được đọc từ database vào ngữ cảnh AI và enum của schema, thay cho danh sách viết cố định. Backend vẫn kiểm tra loại công việc khi tạo bản xem trước và kiểm tra lại khi áp dụng.
- Khi AI hỏi làm rõ, backend loại bỏ toàn bộ lệnh thay đổi, cờ thay lịch tuần và cờ xếp lại trước khi lưu lượt ở trạng thái **Cần làm rõ**. Không áp dụng phần khác của yêu cầu trong lúc chờ câu trả lời. Công việc mới thiếu deadline hoặc thời lượng cũng được chuyển thành câu hỏi thay vì lưu một phần.
- Lượt thất bại được đưa vào tối đa 8 lượt ngữ cảnh gần nhất bằng thông báo cố định rằng yêu cầu chưa lưu; không gửi lại chi tiết lỗi thô. AI phải đối chiếu công việc và phiên thực tế trước khi xác nhận deadline.
- Câu trả lời không có lệnh thay đổi không chạy kiểm tra/xếp lại các xung đột đã có. Sửa lịch tuần, ngoại lệ, sự kiện hoặc công việc với giá trị không đổi không tạo bản xem trước, thay revision hay cung cấp hoàn tác. Các lệnh sửa lặp cùng ID vẫn bị từ chối.
- Hướng dẫn AI yêu cầu giữ các điều kiện của lượt trước, hỏi lại giờ sáng/chiều chưa rõ, không tự đặt thời lượng/deadline và không tự nới giờ rảnh hoặc chia yêu cầu liên tục. Các quy tắc diễn giải này vẫn phụ thuộc vào khả năng tuân thủ của model.
- Công việc hỗ trợ **khung thực hiện riêng**, ví dụ “chỉ học tối thứ Năm”. Khung thực hiện được lưu riêng với deadline và được kiểm tra khi lập lịch, sửa phiên hoặc bắt đầu làm việc. Lượt làm rõ phải giữ nguyên ngày/khung giờ đã xác nhận. Khi chỉ có khung thực hiện và chưa có deadline, điểm kết thúc khung chỉ là giới hạn lập lịch, không được coi là deadline cứng do người dùng đặt.
- Bằng chứng thời lượng/deadline được đối chiếu với nội dung người dùng. Backend có thể lấy lại thời lượng ghi ngay sau đúng tên công việc và nhận ra nhãn deadline đứng ngay trước đoạn giờ được trích; không lấy giờ rảnh làm thời lượng và không lấy ngày thực hiện làm deadline.

Kiểm thử hồi quy dùng phản hồi AI được kiểm soát và database SQLite. Sandbox có chế độ offline và chế độ gọi AI thật với dữ liệu giả; không thay đổi lịch người dùng. [Kiểm chứng bản tích hợp](../reviews/NORA_GUARDRAILS_INTEGRATION_2026-10-19.md) ghi kết quả riêng với [báo cáo gốc ngày 18/10](../reviews/NORA_REQUEST_RESPONSE_QUALITY_REPORT_2026-10-18.md).

### Các bước

1. Có thể gửi thời khóa biểu trực tiếp để hệ thống tự phân tích. Phần **Giờ rảnh** dùng khi muốn cá nhân hóa các khoảng làm việc, múi giờ, phiên dài nhất và tổng phút mỗi ngày.
2. Vào **Trợ lý AI**, nhập yêu cầu có ngày giờ cụ thể, ví dụ:

   - “Ngày mai tôi có cuộc họp từ 14h đến 16h, xếp lại lịch giúp tôi.”
   - “Báo cáo SWP cần làm liên tục 2 tiếng, deadline 18h ngày mai. Xếp vào giờ rảnh cho tôi.”
   - “Tôi rảnh thứ Hai đến thứ Sáu từ 19h đến 22h, riêng ngày mai bận từ 20h đến 21h.”
   - “Chuyển cuộc họp vừa thêm sang 15h đến 17h ngày mai.”

3. Đọc các thay đổi trước/sau và thời gian từng phiên. Nếu thiếu giờ rảnh, chat báo phần thiếu; lịch không được áp dụng một phần.
4. Trả lời trong cùng hội thoại khi cần làm rõ “6h sáng hay 18h”, tên task trùng hoặc số tuần lặp.

## Quy tắc thời gian

- Nội dung “chỉ xem thử”, “chưa đồng ý lưu” hoặc yêu cầu không lưu sẽ giữ bản xem trước dù đang bật tự áp dụng. Gửi lại cùng mã yêu cầu không tự lưu; người dùng vẫn có thể bấm áp dụng bản xem trước.
- Backend kiểm tra cặp thứ/ngày rõ ràng như “thứ Năm ngày 28/10/2026”; nếu không khớp sẽ hỏi lại và không lưu. Ngày tương đối của một sự kiện/ngoại lệ được đối chiếu với ngày tại múi giờ tài khoản.
- Dùng múi giờ tài khoản để hiểu ngày tương đối và hiển thị lịch. Timestamp AI bắt buộc có offset; backend không suy ra theo múi giờ máy chủ.
- Yêu cầu liên tục 120 phút cần một khoảng liên tục và giới hạn phiên ít nhất 120 phút. Chat không tự nâng giới hạn hoặc chia nhỏ yêu cầu liên tục.
- Với chat, bộ lập lịch dùng thời lượng đã xác định trong task và không tự nhân thêm hệ số bias hoặc hệ số năng lực học được. Các giới hạn phiên/ngày/nghỉ vẫn bắt buộc. Những luồng lập lịch thông thường tiếp tục cá nhân hóa estimate.
- Giới hạn mỗi ngày áp dụng tổng phút của phiên đã lên lịch và phiên đã hoàn thành. Đây là giới hạn lập kế hoạch, không tự dừng đồng hồ khi người dùng làm lâu hơn dự kiến.
- Preview hết hạn sau tối đa 30 phút hoặc sớm hơn khi giờ bắt đầu đã qua. Revision thay đổi thì phải gửi lại yêu cầu.
- Chat chừa 5 phút để kiểm tra/lưu lịch; thay đổi đè lên phiên sắp bắt đầu cần xử lý riêng. Form tự dời lịch chừa 1 phút.
- Sự kiện/ngoại lệ mới qua chat bắt đầu trong 30 ngày tới; chuỗi sự kiện có thể kéo dài hơn. Phiên xung đột ngoài phạm vi yêu cầu phải được xử lý riêng hoặc mở rộng phạm vi.

## Tự điều chỉnh khi dùng form

Trong **Giờ rảnh → Khi thời gian thay đổi**, chọn:

- **Tự xếp lại phiên bị ảnh hưởng**: khi đổi lịch rảnh, khoảng bận, sự kiện hoặc giới hạn phiên/ngày, backend xếp lại các task có phiên tương lai linh hoạt bị xung đột. Các phiên ngoài phạm vi 30 ngày không được âm thầm thay đổi.
- **Thông báo để tôi tự xem lại lịch**: giữ lịch làm việc và gửi thông báo nếu phát sinh xung đột; dùng xem trước kế hoạch để xử lý.

Form cho phép lưu thời gian bận ngay cả khi không xếp đủ phần việc còn lại: phiên xung đột được thay thế/hủy, phần chưa xếp đủ có cảnh báo trong kế hoạch và thông báo. Chat yêu cầu toàn bộ phương án khả thi trước khi lưu.

## Cấu hình và cập nhật

Chat dùng chung `AiSettings` và TaskInterpreter với ghi chú (VertexAI/Gemini/OpenAI). Khi chuyển từ AI Studio sang Cloud cần xác thực Cloud riêng. Xem [GOOGLE_CLOUD_AI.md](../integrations/GOOGLE_CLOUD_AI.md).

Nếu AI chưa cấu hình, lịch sử vẫn đọc được và form vẫn dùng được; nút gửi yêu cầu bị khóa. Lỗi provider không chuyển sang phân tích quy tắc và không thay đổi lịch. Các yêu cầu AI được ghi vào job history; hội thoại có giới hạn 2000 yêu cầu/tài khoản, giao diện tải 100 yêu cầu mới nhất và AI dùng tối đa 8 lượt gần nhất của hội thoại hiện tại.

Migration mới: `20261014132803_AddPlanningAgent`, thêm AgentTurns, MaximumDailyMinutes (480) và AutoReschedule (true). Khởi động lại bằng quy trình development hiện có để build và cập nhật database. Nếu chỉ build source mà chưa chạy migration, API chat chưa dùng được với database cũ.

Khung thực hiện riêng và giờ rảnh duy nhất theo ngày dùng migration `20261018172132_NoraSchedulingConstraints` đã có trên main. Bản tích hợp guardrail không thêm migration mới.

Nội dung yêu cầu, lịch và task cần thiết được gửi đến provider AI. Không gửi email, hồ sơ đăng nhập, token hay credential. Khóa AI chỉ ở server. Kiểm thử dùng provider giả, không gọi AI thật.

### Kiểm tra AI thật theo yêu cầu

Từ thư mục dự án, chạy:

```powershell
powershell.exe -NoProfile -ExecutionPolicy Bypass -File .\scripts\Test-LiveAi.ps1
```

Đây là kiểm tra chủ động với hai yêu cầu mẫu, có thể tiêu thụ hạn mức API. Script dùng cấu hình User Secrets hiện có, không in khóa và không tạo dữ liệu trong lịch. Nó kiểm tra cuộc họp 14h–16h, báo cáo SWP liên tục 120 phút trước 18h và tính khả thi qua PlanningEngine. Có thể dùng `-SkipBuild` khi bản Release đã cập nhật.

Ngày 14/10/2026 đã kiểm tra thành công với Gemini thật `gemini-3.5-flash-lite`, áp dụng migration AddPlanningAgent vào database SPWPOS development và nạp lại server Release trên https://localhost:7043. Trang trả HTTP 200, API agent được bảo vệ bởi đăng nhập (HTTP 401 khi chưa đăng nhập).

### Đổi khóa và credit Google Cloud

- Đổi Gemini Developer API key: dùng `scripts/Configure-Gemini.ps1`, nhập khóa trong ô ẩn và nạp lại API. Không đưa khóa vào Git hoặc gửi trong chat.
- Credit Google Cloud Free Trial không phải API key. Theo [tài liệu billing Gemini](https://ai.google.dev/gemini-api/docs/billing), Welcome/Free Trial credit mới không thanh toán cho Gemini Developer API/AI Studio từ tháng 3/2026.
- Google Cloud đã được hỗ trợ qua provider `VertexAI`, với Cloud API key hoặc ADC. Dùng [hướng dẫn cấu hình](../integrations/GOOGLE_CLOUD_AI.md); `VertexApiKey` độc lập với `GeminiApiKey`. Script kiểm tra Cloud thành công trước khi lưu cấu hình và nạp lại server.
- Phạm vi áp dụng credit phải được xác nhận trong Cloud Billing. [Tài liệu Free Trial](https://docs.cloud.google.com/free/docs/free-cloud-features) nêu rõ credit chỉ áp dụng dịch vụ đủ điều kiện; khi nâng cấp paid account, khoản ngoài credit/free tier được tính vào phương thức thanh toán.

## Các phần chưa triển khai trong giai đoạn này

Giao diện NORA mới có streaming thật từ VertexAI/Gemini, tiến trình thu gọn, mascot đi bộ và bảng kế hoạch chỉ mở sau kết quả. Chi tiết thiết kế, transport và kiểm chứng: [NORA_CHAT_RESEARCH.md](../design/NORA_CHAT_RESEARCH.md).

Thống kê lịch sử 3/7/30 ngày, tự điều chỉnh sau 48 giờ bằng worker, ngày lễ Việt Nam/nhắc trước một ngày, streak, XP, pet, skin, nạp xu, payment và admin tài chính nằm trong [PLANORA_UPGRADE_PLAN.md](../plans/PLANORA_UPGRADE_PLAN.md).
