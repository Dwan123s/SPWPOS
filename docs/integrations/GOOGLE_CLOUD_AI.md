# Gemini trên Google Cloud cho PLANORA

## Cập nhật model — 31/10/2026

Đã kiểm tra thực tế `gemini-3.8-flash` với ba yêu cầu mẫu: cuộc họp có giờ cố định, công việc liên tục 120 phút có deadline cứng và trích xuất công việc từ ghi chú. Cả ba đạt kiểm tra dữ liệu có cấu trúc và hoạch định; không lưu dữ liệu mẫu vào lịch người dùng. Tên model được cập nhật trong User Secrets bằng credential Google Cloud hiện có. Các bằng chứng đánh giá dùng 3.5 Flash-Lite trước đây là lịch sử, không phải kết quả đo chất lượng của model mới.

## Lịch sử kết nối — 15/10/2026

Lúc 01:06 (Asia/Ho_Chi_Minh), kiểm tra Cloud thật đạt cả ba yêu cầu mẫu: cuộc họp ngày mai 14:00–16:00, báo cáo SWP 120 phút liên tục trước 18:00 và trích xuất task từ ghi chú. Kết quả `cloud-ai-check.result.json` là success=true/code=OK; trạng thái setup là activated. User Secrets đã lưu Provider=VertexAI, Enabled=true, VertexAuthentication=ApiKey, VertexExpressMode=true, VertexLocation=global, project planora-511614, model gemini-3.5-flash-lite; key đã có trong secret store. Backend khởi động lại và trang chính trả HTTP 200. Không ghi dữ liệu mẫu vào lịch thật. Chưa xác minh việc khấu trừ credit trên Cloud Billing.

Backend hỗ trợ `AiSettings:Provider=VertexAI` cho chat agent, phân tích ghi chú, nhập task và chia task. Model mặc định hiện tại là `gemini-3.8-flash`, project `planora-511614`, location `global`. Gemini 3.8 dùng `thinkingLevel=LOW` cho các yêu cầu có cấu trúc; model này hỗ trợ LOW/MEDIUM/HIGH, không hỗ trợ MINIMAL. Chat văn bản có giới hạn chờ 60 giây; structured interpreter nâng ngân sách đầu ra tối thiểu lên 16.384 token để tính cả reasoning và JSON. Xem [tài liệu model của Google](https://docs.cloud.google.com/gemini-enterprise-agent-platform/models/gemini/3-8-flash) và [kiểm tra chuyển model](../reviews/NORA_GEMINI_38_DEFAULT.md). Kết quả 15/10 ở trên thuộc model cũ, không phải bằng chứng kiểm thử 3.8.

Thêm adapter không tự bật API, tạo credential hoặc chuyển billing. User Secrets chỉ được đổi sau khi kiểm tra Cloud thật thành công. Chưa có key/ADC thì chưa chuyển hệ thống đang chạy.

## Tạo Cloud API key

1. Chọn project PLANORA (`planora-511614`) trong Google Cloud và kiểm tra billing account có credit.
2. Bật [Agent Platform API](https://console.cloud.google.com/apis/library/aiplatform.googleapis.com?project=planora-511614).
3. Mở [API Keys trong Agent Studio](https://console.cloud.google.com/agent-platform/studio/settings/api-keys?project=planora-511614), chọn Create API key. Không tạo key Gemini Developer API trong AI Studio.
4. Với standard API key, principal/tài khoản dịch vụ tương ứng cần quyền `roles/aiplatform.user`. Key thông thường trong APIs & Services mà chưa cấu hình xác thực có thể bị HTTP 403. Tham khảo [quickstart](https://docs.cloud.google.com/gemini-enterprise-agent-platform/models/start) và [Cloud API keys](https://docs.cloud.google.com/gemini-enterprise-agent-platform/models/start/api-keys).

Không gửi key vào chat, Git hoặc frontend. PLANORA không tự cấp quyền IAM hay thay đổi billing account.

## Kết nối trên Windows

Từ thư mục gốc dự án, khi bản Release đã được build:

```powershell
powershell.exe -NoProfile -STA -ExecutionPolicy Bypass -File .\scripts\Configure-VertexAI.ps1 -UseWindow -SkipBuild
```

Nếu chưa build, dừng API và chạy không có `-SkipBuild`. Cửa sổ nhập key dùng ký tự ẩn; bỏ `-UseWindow` để nhập ẩn trong terminal.

Script thử ba yêu cầu mẫu (cuộc họp, task SWP và ghi chú), kiểm tra thời lượng/deadline/schema và bộ xếp lịch. Có thể tiêu thụ credit/quota, không ghi vào lịch thật. Key truyền vào tiến trình kiểm tra qua môi trường, không qua tham số lệnh. Adapter gửi key qua query và header HTTPS; log HTTP của client AI bị tắt. Chỉ sau khi kiểm tra thành công mới lưu User Secrets và nạp lại đúng API trong thư mục dự án.

Trạng thái không chứa key nằm trong `artifacts/cloud-ai-setup.status.json`: `awaiting_credentials`, `validating`, `saved`, `activated`, `cancelled`, `failed`. Kiểm tra thất bại trước bước lưu giữ nguyên cấu hình đang hoạt động. `saved` là đã lưu, `activated` là đã nạp lại API và cổng 7043 đã mở. Xác nhận credit được trừ từ billing account nào trong Cloud Billing.

## Cấu hình

Sau khi đổi cấu hình, khởi động lại bằng `powershell.exe -NoProfile -ExecutionPolicy Bypass -File .\scripts\Restart-PlanoraApi.ps1`. Script cố định môi trường Development và thư mục cấu hình của checkout. Dòng `PLANORA AI: ready; provider=VertexAI; model=gemini-3.8-flash.` xác nhận tiến trình API thực tế đã nạp cấu hình; kiểm tra này không gọi Google và không chứng minh quota/kết nối provider. Có thể kiểm tra cấu hình riêng bằng `--check-ai-config`. NORA kiểm tra lại trạng thái khi quay lại tab hoặc bấm **Kiểm tra lại AI**, giữ nguyên bản nháp và hội thoại.

| Trường | Giá trị |
|---|---|
| `AiSettings:Provider` | `VertexAI` |
| `AiSettings:Enabled` | `true` |
| `AiSettings:Model` | `gemini-3.8-flash` |
| `AiSettings:VertexProjectId` | `planora-511614` |
| `AiSettings:VertexLocation` | `global` |
| `AiSettings:VertexAuthentication` | `ApiKey` hoặc `ADC` |
| `AiSettings:VertexApiKey` | Chỉ lưu trong secret store khi dùng key |
| `AiSettings:VertexExpressMode` | `true` khi dùng API key; `false` khi dùng ADC |

Khi dùng API key, gọi `https://aiplatform.googleapis.com/v1/publishers/google/models/gemini-3.8-flash:generateContent?key=<encoded-key>` và gửi cùng key qua `x-goog-api-key` theo yêu cầu tương thích kép. Project/billing được xác định từ key; `VertexProjectId` là project dự kiến để hiển thị cấu hình, không ghi đè project của key. Key phải được tạo trong project PLANORA và gắn đúng tài khoản dịch vụ. Google hỗ trợ cả header và query; query không phải điều kiện bắt buộc, và thay đổi này chưa đủ để kết luận hết lỗi 401. Tài liệu: [Express mode](https://docs.cloud.google.com/gemini-enterprise-agent-platform/models/start/express-mode/overview), [API quickstart dùng key](https://docs.cloud.google.com/gemini-enterprise-agent-platform/models/start?usertype=apikey). Không dùng key AI Studio/OpenAI để thay thế Cloud key và không tự chuyển provider khi gặp lỗi.

Key trên query được mã hóa bằng Uri.EscapeDataString; HTTP logging của riêng client AI bị tắt bằng RemoveAllLoggers để không ghi URL/header vào log ứng dụng dù bật Trace. Không ghi raw request, URI, provider response hoặc exception message vào log. Công cụ theo dõi HTTP bên ngoài ứng dụng cần che query `key` nếu được sử dụng.

Khi dùng ADC, gọi `https://aiplatform.googleapis.com/v1/projects/planora-511614/locations/global/publishers/google/models/gemini-3.8-flash:generateContent`. Location regional dùng host `{location}-aiplatform.googleapis.com`. Chế độ API key yêu cầu location global; cấu hình regional bị từ chối trước khi gửi key.

Cloud v1 dùng `generationConfig.responseMimeType=application/json` và `responseJsonSchema`. Developer API hiện dùng `responseFormat.text.mimeType=APPLICATION_JSON`. Backend giữ kiểm tra schema và thời gian có UTC offset.

Configure-VertexAI tự bật ExpressMode khi dùng API key, yêu cầu location global và lưu hai giá trị đó sau khi kiểm tra thật thành công. `-ExpressMode` được giữ để tương thích với các lệnh trước đây. Khi dùng ADC, script lưu ExpressMode=false; runtime chọn route theo phương thức xác thực để cờ cũ không đưa Bearer token sang route API key. Đây là cấu hình của adapter; bật cờ không tự đăng ký tài khoản Express Mode trên Google Cloud. Không suy ra loại key hoặc billing account từ mã lỗi; xác nhận project của key và liên kết billing trong Google Cloud.

## ADC khi triển khai

Google khuyến nghị [Application Default Credentials](https://docs.cloud.google.com/gemini-enterprise-agent-platform/models/start/api-keys) cho production. Dùng tài khoản dịch vụ gắn với môi trường chạy, Workload Identity hoặc ADC development. Không đưa file credential vào repo.

Sau khi cài Google Cloud CLI và đăng nhập trên máy development:

```powershell
gcloud auth application-default login
gcloud auth application-default set-quota-project planora-511614
powershell.exe -NoProfile -ExecutionPolicy Bypass -File .\scripts\Configure-VertexAI.ps1 -UseAdc -SkipBuild
```

ADC dùng Google.Apis.Auth, scope cloud-platform, Bearer token và project rõ ràng. Lỗi credential thô không được in. `-NoRestart` chỉ lưu sau kiểm tra, không nạp lại API.

## Hạn mức, credit và kiểm tra

Cloud vẫn có quota/capacity và có thể trả HTTP 429. Lỗi quyền/key/API trả 401/403, model không khả dụng trả 404, định dạng/cấu hình trả 400. Backend báo lỗi, không tự gửi yêu cầu sang AI Studio.

Free Trial chỉ thanh toán dịch vụ đủ điều kiện, có hạn dùng và không áp dụng Developer API/AI Studio mới. Kiểm tra [phạm vi Free Trial](https://docs.cloud.google.com/free/docs/free-cloud-features) và Cloud Billing Reports. Chuyển nền tảng không đảm bảo nhanh hơn hoặc không có limit.

Kiểm thử code dùng HTTP giả cho global/regional, key riêng, schema Cloud, ADC, express mode, cấu hình không hợp lệ và lỗi provider. Luồng note nhận nguồn VertexAI và tạo task có transaction/retry không trùng. Chỉ `Test-LiveAi.ps1` với provider VertexAI mới xác nhận gọi Cloud thật thành công.

## Tổ chức cấm API key

Nếu Agent Studio báo API Keys are Disallowed, dùng ADC theo chính sách tổ chức. Không mở khóa policy, tạo project ngoài tổ chức hoặc tạo service-account key để thay thế. Máy này dùng Google Cloud CLI 589.0.0 từ bản ZIP chính thức, xác minh SHA256 trước khi giải nén vào artifacts/google-cloud-sdk; không thay PATH hệ thống.

Chạy scripts/Connect-GoogleCloudAdc.ps1 để mở đăng nhập Google trong trình duyệt mặc định. Người dùng chọn tài khoản có quyền trên project planora-511614 và tự hoàn tất consent. Script đặt quota project, thử đọc trạng thái billing khi được cấp quyền, kiểm tra/bật aiplatform.googleapis.com nếu cần, rồi gọi Configure-VertexAI -UseAdc -SkipBuild. Chỉ cấu hình sau kiểm tra AI thật đạt. Trạng thái công khai nằm trong artifacts/cloud-adc-setup.status.json; không in credential hoặc access token. Tài liệu nguồn: [ADC local development](https://docs.cloud.google.com/docs/authentication/set-up-adc-local-dev-environment), [SDK versioned archives](https://docs.cloud.google.com/sdk/docs/downloads-versioned-archives).

## Chẩn đoán khi chạy từ CMD - 15/10/2026

Test-LiveAi ghi kết quả vào artifacts/cloud-ai-check.result.json và các dòng LIVE AI an toàn vào artifacts/cloud-ai-check.log, kể cả khi chạy script trực tiếp từ CMD. Windows PowerShell 5.1 nhận stderr dưới dạng ErrorRecord; script thu cả hai luồng trước khi phân loại lỗi. Configure-VertexAI hiện mã lỗi và giai đoạn trong cửa sổ, đồng thời lưu metadata không chứa key ở cloud-ai-setup.status.json. Credential và phản hồi provider thô không được sao chép vào log.

Chẩn đoán Cloud còn ghi `httpStatus` và `providerReason` vào kết quả và cửa sổ lỗi. Backend chỉ giữ mã ErrorInfo nằm trong danh sách cho phép hoặc status chuẩn của Google; không giữ message hay metadata. Ví dụ `IAM_PERMISSION_DENIED` cần kiểm tra quyền của tài khoản dịch vụ, `SERVICE_DISABLED` cần bật API cho đúng project, `API_KEY_SERVICE_BLOCKED` cần kiểm tra API restrictions. Lỗi chung `PERMISSION_DENIED` chưa đủ để kết luận thiếu quyền IAM. Không tự đổi key, provider hoặc endpoint để thử vượt lỗi.

Kiểm tra offline bằng powershell.exe -NoProfile -ExecutionPolicy Bypass -File tests/scripts/CloudAiConfiguration.Tests.ps1: mô phỏng native stderr lỗi quyền, lỗi khởi động và thành công; kiểm tra không lưu cấu hình khi xác minh thất bại, không lộ đầu ra nhạy cảm giả và chỉ lưu sau khi đạt. Không gọi API thật.
