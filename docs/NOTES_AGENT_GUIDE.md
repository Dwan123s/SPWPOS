# Ghi chú và agent lập lịch

Mục **Ghi chú & AI** lưu ghi chú theo tài khoản và chuyển nội dung thành công việc để lập lịch bằng bộ lập kế hoạch của PLANORA.

## Sử dụng

1. Thiết lập giờ rảnh, sự kiện bận và thời gian nghỉ trong **Giờ rảnh**.
2. Vào **Ghi chú & AI**, nhập tiêu đề và nội dung. Ví dụ:

   ```text
   Viết báo cáo SWP trước thứ Sáu, khoảng 3 tiếng
   Ôn chương 4 vào ngày mai, khoảng 90 phút
   ```

3. Chọn số ngày xếp lịch (1–30). Có thể lưu ghi chú để tiếp tục sau.
4. Chọn **Tạo và lưu lịch ngay**. Hệ thống lưu ghi chú, phân tích, tạo công việc và áp dụng lịch ngay; không cần bấm áp dụng thêm.
5. Xem các phiên đã tạo và cảnh báo. Nếu chưa đủ giờ rảnh, phần chưa xếp được vẫn được báo rõ. Vào **Hôm nay** hoặc **Lịch làm việc** để thực hiện phiên.

Nếu tài khoản chưa có giờ rảnh, nút **Tạo công việc & xem lịch** hoặc **Tạo và lưu lịch ngay** mở phần **Chọn giờ rảnh để tạo lịch** ngay trong note. Chọn ngày trong tuần, giờ bắt đầu/kết thúc rồi bấm **Lưu giờ rảnh & tiếp tục**. Hệ thống lưu giờ bạn chọn và tiếp tục thao tác đang thực hiện; tên, thời lượng và lựa chọn công việc đã chỉnh sửa được giữ nguyên. Hủy bước này chưa tạo công việc hoặc lịch. Giờ kết thúc phải sau giờ bắt đầu trong cùng ngày; có thể dùng màn hình Giờ rảnh để cấu hình nhiều khoảng khác nhau.

Nếu nút còn bị khóa, lý do hiện ngay cạnh nút: note đã sửa cần phân tích lại, kết quả cũ chưa dùng AI hoặc chưa chọn công việc.

Nếu muốn kiểm tra trước, chọn **Xem trước công việc**, sửa tên, deadline, thời lượng, ưu tiên, loại việc và chọn các công việc cần lưu. **Tạo công việc & xem lịch** mở bản xem trước; lịch chỉ thay đổi khi chọn **Lưu lịch này**.

## Phân tích hoàn toàn bằng AI

- Khi bấm **Xem trước công việc** hoặc **Tạo và lưu lịch ngay**, nội dung note, thời điểm và múi giờ được gửi đến AI trên server. Không có checkbox chuyển sang bộ quy tắc. **Lưu bản nháp** chỉ lưu dữ liệu, chưa gọi AI.
- Gemini sử dụng `AiSettings:Enabled`, `AiSettings:Provider`, `AiSettings:Model` và `AiSettings:GeminiApiKey`. API key không nằm trong trình duyệt.
- Nếu AI chưa cấu hình, các nút phân tích/tự xếp lịch bị vô hiệu hóa; vẫn có thể lưu note. Server trả `NOTE_AI_NOT_CONFIGURED` (503) khi client gọi phân tích.
- Nếu provider lỗi, hết quota, trả dữ liệu sai hoặc phản hồi bị cắt, server trả `NOTE_AI_UNAVAILABLE` (503). Ghi chú được giữ nguyên, không tạo công việc/lịch từ phản hồi lỗi và không chuyển sang bộ phân tích quy tắc.
- AI tách tối đa 12 công việc, nhận diện thời lượng, deadline, ưu tiên, loại việc và deadline cứng. Dòng tâm trạng hoặc chỉ dẫn cho agent không được chuyển thành việc.
- AI trả cờ `needsReview` cho nội dung mơ hồ, ngày không hợp lệ, quá nhiều việc hoặc điều kiện giờ rảnh/sự kiện/phụ thuộc cần khai báo riêng. Luồng tự động dừng để kiểm tra trước khi tạo công việc.
- Thông tin thiếu được AI gợi ý thời lượng 60 phút và deadline sau 7 ngày. Người dùng có thể sửa trong chế độ xem trước.
- AI đọc và diễn giải note; bộ lập lịch kiểm tra giờ rảnh, sự kiện, deadline, phiên khóa và revision trước khi áp dụng. Viết điều kiện trong note chưa tự cập nhật thiết lập thời gian/phụ thuộc.
- Kết quả phân tích bằng quy tắc từ phiên bản cũ cần được phân tích lại bằng AI trước khi tạo công việc. Các công việc và lịch đã tạo được giữ nguyên.
- Server bắt buộc AI dù client cũ gửi `useAi=false`. Thử lại sau khi đã tạo công việc không tạo trùng; lịch đã áp dụng trả lại kết quả cũ.
- Luồng chỉ chạy khi người dùng bấm nút, chưa tự theo dõi note ở nền.

## Bật Gemini cho ghi chú dạng tự do

Phân tích note cần cấu hình AI. Lấy Gemini API key trong [Google AI Studio](https://aistudio.google.com/api-keys), rồi từ thư mục gốc dự án chạy:

```powershell
powershell.exe -NoProfile -ExecutionPolicy Bypass -File .\scripts\Configure-Gemini.ps1
```

Script hỏi key bằng ô nhập ẩn và lưu qua stdin vào User Secrets; key không nằm trong lịch sử lệnh, tham số tiến trình hay Git. Mặc định dùng `gemini-3.5-flash-lite`; có thể đổi bằng `-Model "YOUR_GEMINI_MODEL"`.

Nếu muốn cấu hình từng giá trị bằng tay:

```powershell
Set-ExecutionPolicy -Scope Process -ExecutionPolicy Bypass -Force
. .\scripts\Initialize-Dotnet.ps1
& $SpwposDotnet user-secrets set "AiSettings:Enabled" "true" --project src/SPWPOS.Api
& $SpwposDotnet user-secrets set "AiSettings:Provider" "Gemini" --project src/SPWPOS.Api
& $SpwposDotnet user-secrets set "AiSettings:Model" "gemini-3.5-flash-lite" --project src/SPWPOS.Api
& $SpwposDotnet user-secrets set "AiSettings:GeminiApiKey" "YOUR_GEMINI_API_KEY" --project src/SPWPOS.Api
```

Chọn model mà tài khoản API của bạn có quyền sử dụng và hỗ trợ Structured Outputs. [Gemini 3.5 Flash-Lite](https://ai.google.dev/gemini-api/docs/models/gemini-3.5-flash-lite) hỗ trợ trích xuất dữ liệu có cấu trúc. Khởi động lại API, mở **Ghi chú & AI** và kiểm tra nhãn **Gemini sẵn sàng**. Nhãn này xác nhận có cấu hình, không kiểm tra quota hay khả năng kết nối provider. Khi người dùng bấm phân tích hoặc tự xếp lịch, server gửi nội dung note, thời điểm hiện tại và múi giờ đến Gemini; không gửi hồ sơ, email hay dữ liệu đăng nhập.

Adapter gọi `generateContent` với schema JSON và key trong header `x-goog-api-key`, sau đó kiểm tra giá trị trả về trên server. Phản hồi bị cắt (`MAX_TOKENS`), bị chặn, lỗi quota hoặc dữ liệu không hợp lệ không được nhập thành công việc AI; giao diện báo lỗi và cho phép thử lại, không chuyển sang bộ phân tích quy tắc. Xem [Gemini generateContent](https://ai.google.dev/api/generate-content) và [Structured Outputs](https://ai.google.dev/gemini-api/docs/generate-content/structured-output). Kiểm thử provider dùng phản hồi giả, không dùng note thật hoặc phát sinh phí API.

REST `generationConfig.responseFormat.text.mimeType` dùng enum `APPLICATION_JSON` theo [TextResponseFormat](https://ai.google.dev/api/generate-content#TextResponseFormat); chuỗi MIME `application/json` tại trường này bị API từ chối với HTTP 400. Không nhầm trường này với `responseMimeType` của cấu hình cũ/SDK.

Lỗi được phân biệt theo trường hợp: quyền/API key (`NOTE_AI_AUTH_FAILED`), model không khả dụng (`NOTE_AI_MODEL_UNAVAILABLE`), quota (`NOTE_AI_QUOTA_EXCEEDED`), request không hợp lệ (`NOTE_AI_REQUEST_INVALID`), timeout/kết nối, phản hồi bị cắt hoặc nội dung bị chặn. Không hiển thị hay ghi log phản hồi lỗi thô của provider vì có thể chứa note hoặc thông tin nhạy cảm.

Cấu hình OpenAI cũ vẫn được hỗ trợ khi đặt `AiSettings:Provider=OpenAI` và `AiSettings:ApiKey`. Key OpenAI không được dùng thay cho key Gemini. Firebase login độc lập với cấu hình agent.

## Lưu trữ và thử lại

Ghi chú và kết quả phân tích được lưu trong bảng `PlanningNotes`. Nội dung tối đa 8000 ký tự, tiêu đề 120 ký tự, tối đa 200 ghi chú mỗi tài khoản.

Sửa ghi chú chưa chuyển thành công việc sẽ xóa kết quả phân tích cũ. Version ngăn yêu cầu từ bản cũ ghi đè dữ liệu mới.

Việc tạo công việc từ note diễn ra trong một transaction. Retry trả cùng các ID đã tạo. Note đã tạo công việc giữ nguyên nội dung; người dùng sửa công việc ở mục **Công việc** hoặc tạo note mới cho việc bổ sung.

Luồng tự động lưu ID kế hoạch đã áp dụng. Retry sau thành công trả lại kết quả cũ. Nếu tạo công việc thành công nhưng lập lịch lỗi, giao diện tải lại note và cho phép tiếp tục xếp lịch, không tạo trùng công việc. Các phiên hiện có của những công việc khác được giữ nguyên.

Xóa note không xóa các công việc hoặc phiên đã tạo.

## Triển khai

Bản này thêm migration `AddPlanningNotes`. Mở Docker Desktop; nếu API đang chạy, dừng bằng `Ctrl+C` tại terminal của API. Từ thư mục gốc dự án, chạy:

```powershell
powershell.exe -NoProfile -ExecutionPolicy Bypass -File .\scripts\Start-Development.ps1
```

Lệnh cho phép chạy script trong tiến trình PowerShell mới, chuẩn bị cấu hình development, cập nhật migration và chạy lại API. Mở `https://localhost:7043/` sau khi terminal báo ứng dụng đã chạy.

Nếu chỉ cần cập nhật database trong phiên PowerShell hiện tại:

```powershell
Set-ExecutionPolicy -Scope Process -ExecutionPolicy Bypass -Force
. .\scripts\Initialize-Development.ps1 -StartContainers
& $SpwposDotnet tool restore
if ($LASTEXITCODE -ne 0) { throw 'Không khôi phục được công cụ EF.' }
& $SpwposDotnet ef database update --project src/SPWPOS.Infrastructure
```

`Initialize-Development.ps1` khởi tạo cả SDK và connection string. Chỉ chạy `Initialize-Dotnet.ps1` không khởi tạo connection string cho terminal mới. Không đưa giá trị connection string hoặc AI key vào Git.

Frontend build lại bằng quy trình hiện có. Kiểm thử AI mặc định sử dụng provider giả; không gọi provider thật hay dùng dữ liệu người dùng.
