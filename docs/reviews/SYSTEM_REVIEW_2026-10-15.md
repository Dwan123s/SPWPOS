# Đánh giá hệ thống PLANORA / SPWPOS và kế hoạch nâng cấp

Ngày đánh giá: **15/10/2026 — Asia/Saigon**. Phạm vi: working tree hiện tại, gồm cả thay đổi chưa commit. Đây là đánh giá kỹ thuật và sản phẩm; điểm số là nhận định có giải thích, không phải chứng nhận bảo mật hay kết quả nghiên cứu thị trường.

## 1. Kết luận điều hành

PLANORA đã có nền tảng tốt cho một sản phẩm lập kế hoạch cá nhân: quản lý task, giờ rảnh, sự kiện, phiên thực hiện, AI hiểu tiếng Việt, xem trước/áp dụng/hoàn tác, streaming và nhập giọng nói. Điểm đáng giữ nhất là **AI không trực tiếp quyết định tính hợp lệ của lịch**; bộ lập lịch và kiểm tra phía server vẫn làm nhiệm vụ đó.

Tuy nhiên, mức hoàn thiện giữa các phần chưa đều. Giao diện thương hiệu và NORA tiến khá nhanh; tính đúng đắn của thống kê, khả năng phục hồi lịch bị bỏ lỡ, vòng đời dữ liệu chat, kiểm soát chi phí AI và vận hành production cần đi trước đợt mở rộng tính năng tiếp theo.

**Điểm nền tảng hiện có: 6,65/10, làm tròn 6,7/10.** Hệ thống phù hợp tiếp tục hoàn thiện MVP và thử với nhóm người dùng nhỏ. Chưa đủ bằng chứng để kết luận sẵn sàng vận hành thương mại hoặc nhận tiền thật.

Định vị nên hướng đến: **“NORA giúp bạn giữ một kế hoạch thực tế và quay lại nhịp làm việc khi bị trễ.”** Đây là đề xuất định vị cần thử với người dùng, không phải kết luận rằng thị trường đã xác nhận.

## 2. Cách đánh giá và giới hạn

Đã thực hiện trong đợt này:

- Đọc luồng backend, các controller, hợp đồng dữ liệu, planner, thực hiện phiên, học từ lịch sử, notes, agent, voice, notification worker, bảo mật, CI và các trang frontend liên quan.
- Chạy harness riêng, biên dịch **PlanningEngine.cs hiện tại cùng DTO/entity hiện tại**, không gọi API AI và không dùng database người dùng. Xác nhận hai trường hợp: phụ thuộc việc cha và rủi ro không phản ánh giới hạn theo ngày.
- Chạy phép thử trực tiếp với `dashboardModel` hiện tại; xác nhận khả năng hiển thị không áp giới hạn ngày.
- Chạy 91 ca frontend thuộc 5 file chính: **90 đạt, 1 thất bại**. Chạy riêng ca thất bại: **1 đạt, 9 ca khác trong file không được chọn**. Kết quả chỉ ra kiểm thử NORA có phụ thuộc thời điểm/selector; không được ghi thành “toàn bộ 91 ca luôn đạt”.
- Mở 8 trang × 2 chiều rộng 1440/390px bằng frontend đã build và API minh họa riêng: không thấy tràn ngang ở 16 trường hợp này. Đây là kiểm tra bố cục với fixture, không xác minh dữ liệu production hay mọi thao tác trên trang.
- Đọc header tài nguyên từ API localhost đang chạy: file CSS 312.081 byte trả `Cache-Control: no-store`.
- Đối chiếu tài liệu chính thức Google Cloud, Microsoft, W3C và Stripe cho những khuyến nghị liên quan nền tảng, hạn mức, chi phí, accessibility và webhook.

Chưa thực hiện: pentest độc lập, kiểm tra toàn bộ backend mới nhất, chạy lại SQL concurrency suite, benchmark tải đồng thời, đo Core Web Vitals trên thiết bị thật, kiểm tra hóa đơn Cloud/IAM trong tài khoản của bạn, restore backup, nghiên cứu người dùng hoặc thử nghiệm thanh toán. Không đọc hoặc đưa API key vào báo cáo. Không thay đổi code sản phẩm trong đợt đánh giá này; chỉ thêm tài liệu và công cụ kiểm chứng ở `artifacts/`.

Ký hiệu độ chắc chắn:

- **Đã tái hiện:** có đầu vào/đầu ra kiểm chứng trong đợt này.
- **Đã thấy trong code:** xác nhận nhánh xử lý hoặc cấu trúc hiện tại, chưa chạy end-to-end trên dữ liệu thật.
- **Chưa tìm thấy trong phạm vi rà soát:** không đồng nghĩa tính năng chắc chắn không tồn tại ngoài repository hoặc trên hạ tầng bên ngoài.
- **Giả thuyết sản phẩm:** cần phỏng vấn, thử nghiệm hoặc đo lường trước khi đầu tư lớn.

## 3. Bảng điểm chi tiết

Thang điểm: 0 = chưa triển khai; 3 = mới có khung; 5 = có luồng cơ bản nhưng thiếu vòng đời/khả năng vận hành; 7 = MVP tương đối đầy đủ và có kiểm thử; 9 = có bằng chứng vận hành, đo lường, phục hồi và trải nghiệm ổn định; 10 không được dùng nếu thiếu kiểm chứng độc lập.

| Hạng mục hiện có | Điểm /10 | Trọng số | Lý do chính bị trừ điểm |
|---|---:|---:|---|
| UX, thương hiệu, onboarding | 7,5 | 8% | Nhiều cửa nhập việc, thiết lập tương đối dày, chưa có usability study |
| Task, subtask, phụ thuộc | 7,5 | 7% | Một cấp việc con; phụ thuộc vào container chưa được dự báo hoàn thành đúng |
| Giờ rảnh, sự kiện, ngoại lệ | 7,5 | 7% | Chưa có lịch lễ, series thực thụ, đồng bộ lịch ngoài; không nhập qua đêm trong một slot |
| Bộ lập lịch, ràng buộc | 7,0 | 12% | Greedy, có trường hợp phụ thuộc bị chặn, chưa có mục tiêu giảm xáo trộn được đo |
| Phiên tập trung và thực hiện | 6,5 | 5% | Resume chưa kiểm tra lại như start; chưa có chính sách timer bỏ quên rõ ràng |
| Thống kê và cá nhân hóa | 5,0 | 6% | Ba cách hiểu capacity/risk; lịch sử 7 ngày theo giờ dự kiến; chưa có 3/7/30 ngày đầy đủ |
| Ghi chú và chuyển thành việc | 7,0 | 5% | Đã có version/idempotency; note đã chuyển đổi bị khóa sửa, dữ liệu thiếu được gợi ý |
| NORA agent và streaming | 7,0 | 9% | Có kiểm tra/undo; context bị cắt, giới hạn lịch sử cứng, thiếu đo chất lượng AI |
| Voice-to-text | 6,5 | 3% | Có preview/final/cancel; Cloud preview gửi lại clip tích lũy, khác nhau theo browser |
| Thông báo và tự động hóa | 5,5 | 5% | Chủ yếu in-app, worker quét tuần tự; chưa có recovery khi trễ hai ngày |
| Tài khoản và bảo mật | 8,0 | 8% | Nền tảng tốt; chưa chứng minh vận hành nhiều instance, MFA admin và privacy lifecycle |
| Kiến trúc và khả năng bảo trì | 6,5 | 5% | Đã chia lớp; WorkspaceService/controller/frontend còn tập trung quá nhiều trách nhiệm |
| Hiệu năng và tăng trưởng dữ liệu | 5,0 | 5% | Snapshot lớn, nhiều truy vấn lặp, no-store cho static assets, chưa benchmark |
| Kiểm thử và CI | 7,0 | 8% | Bộ test rộng, CI có SQL; một ca không ổn định, browser scripts chưa là quality gate CI |
| Triển khai, quan sát, phục hồi | 4,0 | 4% | Có dev scripts/job log; chưa thấy pipeline production, SLO, restore drill |
| Admin hiện tại | 5,0 | 3% | Có khóa/mở tài khoản và log; còn mỏng về phân quyền, tìm kiếm, chi phí, hỗ trợ |
| **Tổng nền tảng** | **6,65** | **100%** | `Σ(điểm × trọng số)` |

Các phần thuộc tầm nhìn mở rộng được chấm riêng, không âm thầm trộn vào điểm MVP:

| Tầm nhìn mở rộng | Điểm triển khai /10 | Hiện trạng trong phạm vi rà soát |
|---|---:|---|
| Pet có XP, level, evolution | 0 | Có mascot hình ảnh/animation; chưa tìm thấy entity và luật XP/level/inventory |
| Streak và thành tích | 0 | Chưa tìm thấy cơ chế streak với lịch sử thưởng và quy tắc ngày nghỉ |
| Shop skin và thanh toán | 0 | Chưa tìm thấy order, payment webhook, refund, entitlement |
| Admin tài chính | 0 | Admin hiện tại quản lý tài khoản/hệ thống; chưa có sổ giao dịch/đối soát |
| Ngày lễ VN và nhắc trước 1 ngày | 0 | Sự kiện thủ công và deadline lead time đã có; chưa có nguồn ngày lễ/reminder riêng |
| Đồng bộ Google/Outlook Calendar | 0 | Đăng nhập Google qua Firebase không phải quyền đọc/ghi Google Calendar |

Điểm 0 ở đây phản ánh chưa có triển khai được tìm thấy, không phải đánh giá ý tưởng kém.

## 4. Những vấn đề cần ưu tiên, có bằng chứng

### F01 — Dashboard báo khả năng cao hơn giới hạn người dùng

**Mức: P1. Độ chắc chắn: đã tái hiện.**

Tình huống: rảnh thứ Năm 08:00–18:00 = 600 phút; hệ số capacity = 0,8; giới hạn ngày = 60 phút. `dashboardModel` trả capacity **480 phút**, trong khi `PlanningEngine.Analyze` áp trần ngày và trả **60 phút** ở tình huống tương ứng.

Tác động: người dùng nhìn dashboard tưởng còn 8 giờ, nhưng lúc xếp lịch chỉ có 1 giờ. Đây là vấn đề niềm tin vào số liệu, không chỉ khác cách trình bày.

Nguồn: `frontend/src/dashboard-model.js:133`, `src/SPWPOS.Application/Workspace/PlanningEngine.cs:229`. Bằng chứng: `artifacts/system-review-dashboard-results.json`.

Nâng cấp: thống nhất mô hình trả về từ backend gồm `calendarFree`, `dailyLimitRemaining`, `effectiveCapacity`, `reserved`, `remainingSchedulable`. Frontend trình bày thay vì tự định nghĩa lại nghiệp vụ. Acceptance: cùng ngày/múi giờ/filter phải ra cùng số ở Hôm nay, Tiến độ và Xem trước lịch.

### F02 — Tổng thể quá tải nhưng task vẫn được đánh giá rủi ro thấp

**Mức: P1. Độ chắc chắn: đã tái hiện.**

Tình huống: việc 120 phút, giới hạn ngày 60 phút, deadline trong ngày, 600 phút giờ rảnh. Phân tích tổng thể báo `Overload`, `LoadRatio=2`; task báo `Low`, chỉ có lý do deadline gần. Planner thực tế báo thiếu 60 phút.

Nguyên nhân: rủi ro task dùng tổng free slots × capacity trước deadline; chưa sử dụng cùng ngân sách ngày/breaks/feasibility như planner.

Nguồn: `PlanningEngine.cs:237`. Bằng chứng: `artifacts/system-review-planner-results.txt`, mục `dailyLimitRisk`.

Nâng cấp: bổ sung feasibility dùng chung, phân biệt rõ “không đủ ngân sách ngày”, “thiếu khoảng liên tục”, “vướng phụ thuộc” và “deadline sát”. Không hứa tỷ lệ phần trăm xác suất trễ khi chưa có mô hình được hiệu chỉnh.

### F03 — Phụ thuộc vào công việc cha có thể bị chặn không cần thiết

**Mức: P1. Độ chắc chắn: đã tái hiện.**

Tình huống: P là việc cha; A là việc con 60 phút; B cần 60 phút và phụ thuộc P. Có 600 phút trống. Planner xếp A, nhưng B nhận `DEPENDENCY_UNSCHEDULED`.

Nguyên nhân: planner loại container khỏi danh sách cần xếp để không cộng đôi workload; sau khi xếp việc con lại chưa tính thời điểm hoàn tất dự kiến cho container. Bộ phát hiện vòng lặp đã hiểu quan hệ cha/con, nhưng bước dự báo hoàn tất chưa đồng bộ cách hiểu đó.

Nguồn: `PlanningEngine.cs:142`, `:143`, `:213`. Bằng chứng: file probe trên, mục `parentDependency`.

Nâng cấp: xác định quy tắc completion của container; nếu P hoàn thành khi mọi việc con hoàn thành, predictedFinish(P) = thời điểm muộn nhất của các con. Nếu có bước duyệt riêng, mô hình hóa thành milestone, không dùng một container mơ hồ. Acceptance: B được xếp sau A khi đủ thời gian; vẫn chặn đúng khi thiếu một việc con hoặc có chu kỳ.

### F04 — Resume không tái kiểm tra như Start

**Mức: P1. Độ chắc chắn: đã thấy trong code, cần integration test xác nhận trọn luồng.**

`start` kiểm tra trạng thái, cửa sổ thời gian, dependency, sự kiện đang diễn ra, giờ rảnh và deadline. `resume` hiện chỉ kiểm tra Paused rồi bật timer.

Tình huống cần kiểm chứng: phiên 14:00–15:00 được pause; sự kiện 16:00–17:00 đã có sẵn; người dùng quay lại 16:30 và resume phiên cũ. Nhánh resume không có kiểm tra sự kiện “đang diễn ra” như start.

Nguồn: `ExecutionService.cs:24`, `:31`, `:38`.

Nâng cấp: tách validator chung cho thao tác bắt đầu/tiếp tục, nhưng phân biệt **khung lịch dự kiến** và **việc người dùng thực sự đang làm**. Nếu cho phép làm quá giờ, phải ghi rõ và hiển thị xung đột; nếu cấm, hướng dẫn tạo/xếp lại phần còn lại. Không âm thầm tính một phiên cũ là đang tuân lịch.

### F05 — Lịch sử NORA có điểm chặn sau thời gian sử dụng

**Mức: P1 trước mở rộng người dùng. Độ chắc chắn: đã thấy trong code.**

API lịch sử chỉ trả 100 lượt gần nhất. Tổng lượt mỗi tài khoản được giới hạn 2000. Trong controller/hợp đồng đang rà soát chưa tìm thấy phân trang, xóa hoặc archive hội thoại để người dùng tự phục hồi khi chạm trần.

Tác động: hội thoại cũ có thể không còn xuất hiện dù chưa bị xóa; dùng lâu có thể không gửi thêm được. Tìm kiếm frontend không thể tìm phần lịch sử server chưa trả.

Nguồn: `AgentService.cs:24`, `:56`; `AgentController.cs`.

Nâng cấp: entity Conversation với title/archivedAt; endpoint liệt kê theo cursor; endpoint lấy messages theo conversation; tìm kiếm server; archive/delete/export; retention và hạn mức được thông báo trước. Giữ tombstone idempotency riêng đủ thời gian thay vì mất chống trùng khi xóa lịch sử.

### F06 — NORA chỉ thấy một phần dữ liệu nhưng chưa biểu đạt phạm vi rõ ràng

**Mức: P1 cho độ tin cậy AI. Độ chắc chắn: đã thấy trong code.**

Context hiện lấy tối đa 50 task, 50 event, 60 session và 8 lượt hội thoại; event/exception chủ yếu trong 30 ngày. Giới hạn token là cần thiết, nhưng câu hỏi “tất cả công việc của tôi” có thể được trả lời từ một tập con.

Nguồn: `AgentService.cs:73–87`.

Nâng cấp: context theo mục đích, truy vấn task cụ thể theo ID/tên và khoảng ngày; metadata `totalCount`, `includedCount`, `truncated`, `range`. Khi thiếu dữ liệu, hỏi hoặc nạp thêm. Không cần vội thêm vector database; dữ liệu lịch có cấu trúc nên lọc SQL trước.

### F07 — Chính sách lập lịch của chat khác luồng planner thông thường

**Mức: P1/P2 tùy định vị. Độ chắc chắn: đã thấy trong code; có thể là lựa chọn sản phẩm có chủ đích.**

Chat tạo plan với `UseCapacityFactor:false` và `UseEstimateBias:false`; planner thông thường mặc định bật. Yêu cầu rõ “làm đúng hai tiếng” có lý do để giữ 120 phút, nhưng người dùng chưa được giải thích vì sao các luồng có thể cho lịch khác nhau.

Nguồn: `AgentService.cs:116`, `Contracts.cs:50`.

Nâng cấp: một policy rõ ràng: “giữ thời lượng bạn yêu cầu” và “dùng thời lượng đã cá nhân hóa”. Giữ hard constraints; cho biết khi AI đang dùng 120 phút người dùng nhập hay 150 phút hệ thống dự báo. Không mặc định gắn nhãn đây là bug nếu đó là ý đồ giữ chính xác thời lượng.

### F08 — Bỏ lỡ phiên chưa tạo thành vòng phục hồi kế hoạch

**Mức: P1 đối với lời hứa trợ lý tự động. Độ chắc chắn: đã thấy trong code.**

Worker đánh dấu phiên Scheduled đã hết giờ thành Missed, cập nhật learning/risk và thông báo. Tự xếp lại đang gắn với thay đổi giờ rảnh/sự kiện/tùy chọn. Chưa thấy luật chạy riêng cho “hai ngày không thực hiện”.

Nguồn: `NotificationAdminService.cs:32–48`, `TimeService.cs`.

Nâng cấp: Recovery Policy theo người dùng: nhắc → đề xuất → tự áp dụng khi đã cho phép và còn khả thi. Phân biệt trễ phiên với deadline đã qua: hệ thống có thể dời phiên nếu deadline còn hợp lệ; không tự đổi deadline cứng để làm biến mất cảnh báo.

### F09 — Thống kê lịch sử chưa trả lời đúng “tôi đã tiến bộ thế nào?”

**Mức: P1. Độ chắc chắn: đã thấy trong code.**

Biểu đồ 7 ngày lấy phiên Completed theo `dayKey(s.end)`, tức ngày kết thúc **dự kiến**, không phải ngày thực sự hoàn thành hoặc thời điểm các đoạn tập trung xảy ra. Nhãn phụ hiện đã nói rõ điều này, nên không phải dữ liệu giả; tuy nhiên đó chưa phải nhật ký thực hiện theo ngày. Bảng task chỉ lấy 8 dòng đầu và chưa thấy bộ chọn 3/7/30 ngày ở phần này.

Nguồn: `frontend/src/pages.jsx:25`, `:36`, `:123`. Backend đã có `CompletedAt`, `SessionExecution`, `ExecutionSegment`, thuận lợi để xây phần đúng.

Nâng cấp: tách ba biểu đồ: lịch đã lên, thời gian thực sự làm theo segment, kết quả hoàn thành theo CompletedAt. Phân bổ segment qua nửa đêm đúng múi giờ. Thêm bộ lọc 3/7/30 ngày, kỳ so sánh trước đó, drill-down ngày và trạng thái chưa đủ dữ liệu. Không đồng nhất “làm lâu” với “làm hiệu quả”.

### F10 — AI có quota nhưng chưa có ngân sách sản phẩm theo người dùng

**Mức: P1 trước public beta. Độ chắc chắn: đã thấy rate limiter hiện tại; chưa tìm thấy usage ledger trong phạm vi rà soát.**

Giới hạn 60 request/phút hiện theo IP; voice có 24/phút theo IP. Limiter chạy trước authentication, chưa thấy hạn mức token/chi phí theo account hoặc giới hạn số AI request đồng thời riêng. Nhiều tài khoản chung Wi-Fi có thể chia cùng trần, trong khi đổi IP không phải cách kiểm soát chi phí một account.

Chưa thấy lưu input/output token, model version, chi phí ước tính và tổng mức sử dụng theo user. Job log chỉ cho biết thành công/thất bại và thời gian, chưa đủ quản trị chi phí.

Nguồn: `Program.cs:92–106`, `:221–222`, `JobHistoryService.cs`; các integration AI trong Workspace.

Nâng cấp: giữ IP limiter chống lạm dụng sớm; thêm limiter theo user sau authentication, một quota ledger bền vững, giới hạn concurrency và ngân sách ngày/tháng. Retry có backoff/jitter cho lỗi tạm thời ở bước gọi model, cùng request ID; không tự lặp mutation đã thành công. Thêm nút ngắt AI trong admin.

Có credit Google Cloud **không đảm bảo không gặp 429**; tài nguyên và throughput vẫn có giới hạn. Google mô tả cơ chế và khuyến nghị xử lý retry trong [tài liệu throughput](https://docs.cloud.google.com/vertex-ai/generative-ai/docs/resources/throughput-quota). Cấu hình budget cảnh báo khác spend cap; spend cap hiện có cho dịch vụ đủ điều kiện nhưng vẫn có độ trễ, nên không thay thế quota nội bộ. Xem [Google Cloud spend cap budgets](https://docs.cloud.google.com/billing/docs/how-to/budgets-spend-caps).

### F11 — Voice preview có thể tốn nhiều lượt suy luận hơn người dùng nghĩ

**Mức: P2; nâng thành P1 nếu đông người dùng voice. Độ chắc chắn: đã thấy trong code.**

Khi nhận dạng browser không khả dụng hoặc ngừng có kết quả, Cloud preview dùng `new Blob(ctx.chunks)` và gửi clip tích lũy, tối thiểu cách nhau khoảng 4 giây, rồi vẫn gửi bản đầy đủ lúc dừng. Đây là cách hợp lý để giữ container audio hợp lệ; không phải streaming STT hai chiều thực sự. Phần đã nói có thể được xử lý lại nhiều lần.

Nguồn: `voice-input.js:126`, `:136`, `:146`, `:293`. Giới hạn hiện có: 60 giây frontend, 2 MiB backend.

Nâng cấp: đo số giây audio xử lý/giây người dùng nói; adaptive debounce, VAD/đoạn im lặng, giới hạn preview; chỉ chuyển sang dịch vụ STT streaming chuyên dụng khi số liệu chứng minh cần. Giữ quyền xem lại bản final và không tự gửi chat. Test giọng vùng miền, tên môn học, số giờ, tiếng ồn, mobile Safari và ngắt micro.

### F12 — Dữ liệu lớn được đọc lại ở nhiều đường đi

**Mức: P1/P2 theo quy mô. Độ chắc chắn: đã thấy trong code; tác động tải cần benchmark.**

`State()` có thể tải 5000 task, 10000 session, dependency và danh sách event/exception. Nhiều GET phụ như danh mục/task detail/notification gọi `GetAsync` lấy snapshot rộng. Mỗi mutation có revision thường cập nhật learning và persist risk, đọc lại lịch sử. Worker quét mọi tài khoản hoạt động tuần tự mỗi 60 giây. Admin search lấy 100 kết quả đầu, chưa có cursor.

Nguồn: `WorkspaceService.cs:42`, `:54`, `:66`; `WorkspaceControllers.cs`; `NotificationAdminService.cs:94–103`.

Nâng cấp: query service theo use case, projection DTO, khoảng ngày và cursor. Recalculate phần bị ảnh hưởng; gom learning/snapshot ở background khi không cần đồng bộ để bảo vệ ràng buộc. Giữ transaction đồng bộ cho dữ liệu nghiệp vụ quan trọng. Worker xử lý theo due time và batch; khi nhiều instance, dùng lease/claim và dedupe, không chỉ tăng số task chạy song song.

Không kết luận “server chắc chắn sập ở N người dùng”: chưa có load test.

### F13 — Static assets đang bị no-store

**Mức: P2. Độ chắc chắn: đã xác nhận trên localhost.**

Middleware đặt `Cache-Control:no-store` trước `UseStaticFiles`. HEAD CSS thực tế trả no-store, kích thước 312.081 byte. Build hiện có main JS khoảng 480 KB, CSS khoảng 312 KB và sprite đi bộ 1,93 MB, chưa tính các tài nguyên được tải ở từng route. Đây là kích thước file thô, không phải tổng byte chắc chắn truyền qua mạng hoặc thời gian LCP đã đo.

Nguồn: `Program.cs:188`, `frontend/src/styles/index.css`; các file trong `wwwroot/app/assets`.

Nâng cấp: API/dữ liệu nhạy cảm giữ no-store; HTML revalidate; asset có hash cache dài immutable. Kiểm tra compression ở app hoặc reverse proxy thực tế. Tách CSS theo tính năng, nén sprite sang WebP/AVIF nếu giữ chất lượng/alpha, ưu tiên WOFF2 subset tiếng Việt. Đo Network/CPU trên mobile trước và sau. Không giảm chất lượng mascot bằng cách hạ kích thước tùy tiện.

### F14 — Khởi động gặp lỗi mạng có thể quay về trang public

**Mức: P2. Độ chắc chắn: đã thấy trong code.**

`startController` kết thúc bằng `loggedIn().catch(publicPage)`. Lỗi mạng/5xx trong session/workspace có thể đi cùng đường với trường hợp chưa đăng nhập. Người dùng có thể hiểu nhầm rằng đã bị đăng xuất.

Nguồn: `frontend/src/controller.jsx`, đoạn cuối `startController`.

Nâng cấp: phân biệt 401, timeout, offline và 5xx. Có trạng thái “Chưa kết nối được — Thử lại”, giữ draft, không chuyển về landing vì mọi loại lỗi. Loading hiển thị ngay; không cố giữ màn hình để khoe animation.

### F15 — Note chuyển thành task không còn sửa được nội dung

**Mức: P2. Độ chắc chắn: đã thấy trong code; đây có thể là quyết định chống tạo trùng có chủ đích.**

`CheckNoteEditable` chặn sửa note khi `CreatedTaskIdsJson != []`. Cơ chế này bảo vệ lịch sử tạo task, nhưng ghi chú thường là tài liệu sống: người dùng còn muốn sửa câu chữ hoặc thêm ý sau buổi học.

Nguồn: `NoteService.cs:24–25`, `:38`.

Nâng cấp: tách document version khỏi import batch. Cho sửa note; phần từng chuyển thành task có snapshot và liên kết nguồn. Khi phân tích lại, hiển thị diff “việc mới / cập nhật đề xuất / đã tạo”, người dùng chọn áp dụng. Không tự sửa task đã thực hiện chỉ vì note đổi.

### F16 — Kiểm thử NORA có selector chưa ổn định

**Mức: P2. Độ chắc chắn: đã quan sát thất bại khi chạy nhóm, chạy riêng đạt.**

Test `sends a clear request...` dùng `getByText(/14:00.*16:00/)`. Khi UI hiển thị cùng giờ ở thông điệp và panel lịch, có thể có hai phần tử phù hợp. Thất bại này không chứng minh xếp lịch hoặc undo sai; nó làm giảm giá trị tín hiệu CI.

Nguồn: `frontend/tests/agent.test.jsx:106–118`. Kết quả: `artifacts/system-review-frontend-tests.json`, `system-review-agent-recheck.json`.

Nâng cấp: scope bằng `within` vùng plan, chờ trạng thái UI ổn định và kiểm tra hành vi người dùng. Đưa smoke browser, bàn phím, CSP, SSE stop/retry và reduced-motion vào CI chính thức, thay vì chỉ chạy script trong artifacts. Không đổi thành `getAllByText(...)[0]` chỉ để che selector mơ hồ mà không xác định đang kiểm tra gì.

### F17 — Vòng đời .NET cần được lên lịch nâng cấp ngay

**Mức: P1 về kế hoạch vận hành. Độ chắc chắn: đối chiếu tài liệu chính thức.**

Hệ thống đang dùng .NET/EF Core 8. Theo Microsoft, .NET 8 kết thúc hỗ trợ **10/11/2026**; tại ngày đánh giá 15/10/2026 còn khoảng 26 ngày. Hiện chưa hết hỗ trợ; không nên mô tả là đã lỗi thời hoặc có lỗ hổng chỉ từ số phiên bản. .NET 10 LTS được hỗ trợ đến 14/11/2028. [Microsoft .NET support policy](https://dotnet.microsoft.com/en-us/platform/support/policy/dotnet-core).

Nâng cấp: lập nhánh thử nghiệm .NET 10, xem breaking changes ASP.NET/EF/Identity, chạy migration trên bản sao database, auth/refresh/OTP/SSE/voice/concurrency tests, diễn tập rollback. Không đổi framework trực tiếp trên môi trường đang phục vụ người dùng.

## 5. Đánh giá sâu từng hạng mục và ý tưởng nâng cấp

### 5.1. Trải nghiệm tổng thể — 7,5/10

Điểm tốt: có thương hiệu, mascot thống nhất ở login/getting-started/loading/NORA; tiếng Việt tương đối rõ; layout mobile không tràn ngang trong mẫu đã xem; có hướng dẫn, focus management và lựa chọn giảm chuyển động ở các phần chính.

Điểm chưa hợp lý: người mới phải hiểu Nhập việc, Công việc, NORA, Giờ rảnh và Lịch. Đây đều là chức năng hữu ích nhưng cùng tranh vai trò điểm bắt đầu. Sự đồng nhất về màu xanh không đồng nghĩa đồng nhất về tương tác: NORA gần giao diện chat tối giản, các trang khác thiên nhiều card kính và form. Sidebar, heading, nội dung phụ tạo khá nhiều tầng trước khi đến việc cần làm, nhất là mobile.

Ý tưởng: một luồng mở đầu **“Chọn giờ rảnh → nhập ba việc → xem một ngày khả thi → bắt đầu phiên đầu tiên”**. NORA và note đều ghi vào một activity feed. Khi người dùng đã quen, mở form nâng cao. Nút chính trên Hôm nay chỉ nên phản ánh hành động tiếp theo có ích.

Không tiếp tục thêm animation vào mọi phần. Dùng chuyển động để thể hiện thay đổi: phiên được dời, dữ liệu được lưu, NORA bắt đầu/kết thúc. Giảm animation nền sau khi người dùng bắt đầu tập trung. Cần kiểm tra tương phản và kích thước vùng bấm theo [WCAG 2.2](https://www.w3.org/TR/WCAG22/); chưa có kết quả audit đủ để tuyên bố đạt AA.

Đo: tỷ lệ người mới tạo được lịch đầu tiên, số bước, thời gian hoàn tất, tỷ lệ cần trợ giúp. Mục tiêu thử nghiệm ban đầu: người mới hoàn thành tác vụ cơ bản trong khoảng 5 phút, ghi rõ đây là mục tiêu thiết kế.

### 5.2. Task/subtask — 7,5/10

Đã có: deadline, hard/soft deadline, duration, priority, loại việc, tiến độ, một cấp subtask, dependency, chống chu kỳ, ownership và soft delete. Tốt hơn một CRUD task đơn giản.

Cần nâng cấp: phân biệt task cần làm với container/dự án và milestone; hiển thị vì sao bị chặn; làm rõ hoàn thành phiên không tự chứng minh hoàn thành task. Estimate nên có nguồn “bạn nhập / AI gợi ý / hệ thống điều chỉnh”. Một việc cha không nên có estimate gây hiểu nhầm khi workload chỉ tính các con.

Ý tưởng: Task Detail thống nhất gồm mục tiêu, phần còn lại, lịch sử phiên, dependency và nút “Xem lý do chưa xếp được”. Thêm template báo cáo SWP: tìm tài liệu → viết → rà soát → nộp, có dependency rõ. Chỉ thêm task recurrence khi đã có quy tắc series/instance và chống tạo trùng.

### 5.3. Giờ rảnh và sự kiện — 7,5/10

Đã có: lịch tuần, ngoại lệ rảnh/bận, sự kiện với khung giờ thật; không tự suy ra giờ rảnh khi chỉ nghe yêu cầu task; bảo vệ phiên khóa và đang chạy; tự dời phiên linh hoạt bị ảnh hưởng khi bật.

Bất tiện: chỉnh lịch tuần bằng nhiều dòng form; sự kiện chỉ lặp hàng tuần hữu hạn 1–12 lần rồi thành các bản ghi riêng; chưa có “sửa cả chuỗi / từ lần này trở đi”. Cửa sổ tuần không qua nửa đêm trong một dòng; với người học tối cần tách hai ngày. Chưa có thời gian di chuyển, khoảng đệm riêng giữa các cuộc hẹn và ngoại lệ ưu tiên được giải thích trực quan.

Ý tưởng: lưới tuần có kéo chọn giờ, nhưng luôn giữ form nhập chính xác cho bàn phím/mobile; preset theo lịch học; dùng màu khác cho Rảnh, Bận, Sự kiện và Khóa. Khi lưu, xem rõ bao nhiêu phiên bị dời và vì sao. Dữ liệu ngày lễ là lớp calendar riêng, mặc định nhắc/đánh dấu, không tự chiếm toàn bộ ngày của mọi người.

### 5.4. Bộ lập lịch — 7,0/10

Đã có: pure engine, timezone, xử lý DST gap, merge/subtract interval, hard constraints, ưu tiên giờ làm hiệu quả, nghỉ giữa phiên, giới hạn ngày/phiên, giữ phiên đã khóa, dependency, cảnh báo phần chưa xếp, revision và kiểm tra lại khi apply.

Giới hạn: greedy theo deadline/priority chưa có điểm tối ưu toàn cục. Lịch hợp lệ không đồng nghĩa lịch ít mệt hoặc ít thay đổi nhất. Chưa thấy đánh giá chất lượng theo số lần chuyển loại việc, độ phân mảnh, giờ tập trung, khoảng đệm trước deadline và số phiên bị dời.

Ý tưởng: giữ engine hiện tại làm baseline; thêm mục tiêu theo trọng số và giải thích. So sánh vài phương án “ít đổi lịch / hoàn thành sớm / ít chuyển ngữ cảnh”. Chỉ đưa solver phức tạp khi benchmark ca khó chứng minh greedy không đạt, không rewrite toàn bộ chỉ để có chữ “optimization”.

### 5.5. Tập trung và timer — 6,5/10

Đã có state machine start/pause/resume/complete/skip, segment thực hiện, một phiên active/account và điều chỉnh actual duration. Đây là nền quan trọng cho thống kê và pet.

Cần rõ: chính sách phiên quá giờ, bỏ quên timer, pause sang ngày khác, reconnect/multi-tab; user-adjusted actual nên được nhận diện trong học estimate và thưởng. Không kết luận đây là gian lận hiện tại khi chưa có pet, nhưng phải giải quyết trước khi dùng timer tạo quyền lợi.

Ý tưởng: màn hình Focus riêng có mục tiêu đầu ra và một thao tác chính. Khi kết thúc hỏi nhanh “xong / còn khoảng bao lâu / bị gián đoạn”. Nếu timer chạy quá lâu, gợi ý xem lại, không tự cộng vô hạn XP. Chống trùng thao tác hoàn thành ở server.

### 5.6. Tiến độ và cá nhân hóa — 5,0/10

Đã có bias theo loại task từ ít nhất 3 mẫu, giới hạn ratio, snapshot capacity, workload và risk reasons. Đây là **cá nhân hóa thống kê**, không phải hệ thống tự huấn luyện một LLM riêng.

Cần thêm: freshness của dữ liệu; độ tin cậy khi mẫu nhỏ; làm rõ actual/planned và “đúng hạn” là gì. Capacity lịch sử tổng hợp có thể phản ứng chậm nếu lịch sinh hoạt vừa thay đổi. Thời gian làm lâu hơn không tự động là thành tích tốt hơn.

Ý tưởng: 3/7/30 ngày, heatmap tập trung, tỷ lệ giữ lịch, tỷ lệ hoàn thành task đúng hạn, phiên trễ, estimate error theo loại và weekly review. Hiển thị “chưa đủ dữ liệu” thay vì tạo chỉ số đẹp. Thử cửa sổ gần đây có trọng số và so với baseline, bảo đảm thay đổi policy có version để giải thích.

### 5.7. Notes — 7,0/10

Đã có version chống ghi đè, lưu note trước phân tích, AI structured output, review khi mơ hồ, import không tạo trùng, preview lịch và chuyển thành task. Lỗi AI không làm mất note đã lưu.

Chưa thuận tiện: note và chat dùng hai cách xử lý dữ liệu thiếu. Note có thể gợi ý 60 phút và deadline sau 7 ngày; chat có xu hướng hỏi. Đây là khác biệt cần hiển thị rõ, không để người dùng xem gợi ý như lời hứa deadline gốc. Note đã import bị khóa sửa như F15.

Ý tưởng: chia input thành “ghi chú tự do” và “yêu cầu NORA”, nhưng cùng một cơ chế clarification và provenance. Đánh dấu trực tiếp đoạn tạo ra từng task; cho chỉnh phần AI suy đoán trước áp dụng. Ghi chú dài nhiều hơn 12 việc cần chia batch có kiểm kê số việc, không âm thầm mất ý.

### 5.8. NORA và chất lượng AI — 7,0/10

Đã có agent ứng dụng chuyên lập lịch: đọc context → hiểu intent → lập kế hoạch → kiểm tra → lưu. Có lịch sử, hỏi lại, request ID, lease, stale revision, expiry, undo và streaming reply thực. Public thinking là trạng thái xử lý, không phải lý luận bí mật của model. NORA vẫn do hệ thống của bạn xây; Google cung cấp khả năng model.

Cần thêm: lifecycle hội thoại; retrieval theo nhu cầu; question/options có cấu trúc thay vì đoán từ câu hỏi bằng regex; per-user usage; đo chất lượng ngày giờ tiếng Việt; transcript final có thể khác preview cần hiển thị hợp lý. Lịch sử dài và lời nhắc mang tính cá nhân chưa có bộ nhớ có kiểm soát đủ sâu.

Ý tưởng: tách tool registry nội bộ với read/write và policy rõ; read-only được chạy linh hoạt, thay đổi lịch qua preview/apply idempotent. Memory chỉ lưu sở thích được người dùng xác nhận, có xem/sửa/xóa; không giữ suy luận nhạy cảm. Eval set version hóa 100–200 câu tiếng Việt: 6h mơ hồ, chiều mai, thứ Sáu tuần sau, hai việc trùng tên, qua nửa đêm, lịch khóa, đổi lịch khi stream đang chạy, mất mạng sau commit, prompt injection trong task/note. Chưa cần nhiều agent hay framework nặng để làm tốt tập việc này.

### 5.9. Voice — 6,5/10

Điểm tốt: xin quyền micro theo thao tác, preview ngay trong ô chat, authoritative final, cancel, ngừng track, hạn chế MIME/kích thước và không tự gửi nội dung. Backend không lưu audio lên disk trong luồng đã đọc.

Cần thêm: đánh giá sai số nội dung thời gian/deadline, thông báo rõ browser recognition có thể dùng dịch vụ bên ngoài, số lượt/chi phí Cloud preview, tình huống chồng tiếng và tên riêng. “Streaming chữ” hiện có không đồng nghĩa tất cả browser dùng cùng một speech service realtime.

Ý tưởng: tô nhẹ phần transcript chưa chốt; khi final đổi “6h” thành “16h” cần dễ nhận ra và xem lại trước gửi; đừng tự coi confidence của provider là đảm bảo đúng.

### 5.10. Thông báo và tự động hóa — 5,5/10

Đã có reminder phiên, cảnh báo deadline, workload, conflict, cấu hình lead time và dedupe. Worker có xử lý lỗi từng account để một account không làm dừng toàn bộ lượt quét.

Giới hạn: in-app không đủ để nhắc người đã đóng ứng dụng; chưa thấy push/notification email tổng quát cho lịch làm việc, trong khi email OTP/bảo mật đã có là một luồng khác. Lịch cố định chưa thấy reminder theo sự kiện; không được hiểu lead deadline 24 giờ là đã có nhắc ngày lễ trước một ngày. Worker quét tuần tự mọi active user, có thể chậm khi quy mô tăng.

Ý tưởng: NotificationIntent → DeliveryJob → DeliveryAttempt, quiet hours, timezone, ưu tiên và digest. Dedupe theo event occurrence + lead + channel. Tách “trước đúng 24 giờ” và “ngày hôm trước lúc 20:00”; cho người dùng chọn. Worker chạy bù khi server khởi động lại, không gửi dồn các reminder đã quá hạn vô ích.

### 5.11. Tài khoản/bảo mật — 8,0/10

Điểm tốt trong code: Argon2id, OTP có TTL/attempt limits/single-use, refresh token hash và rotation, cookie HttpOnly/same-origin, kiểm tra user/session family trong protected request, giới hạn request, kiểm tra nguồn cho cookie mutation, CSP, HTTPS, ownership và composite foreign keys. Có khóa account/transaction để chống cạnh tranh ở các thay đổi quan trọng. HttpClient AI tắt log URL/header vì key tương thích nằm cả trong URL. Đây là các điểm thực chất, không nên bỏ khi refactor.

Khoảng trống cần kiểm chứng/nâng cấp: MFA hoặc xác thực tăng cường cho admin; quản lý/thu hồi từng thiết bị; policy dữ liệu và xóa/export account; retention chat/notes/audit; kiểm tra nhiều instance và reverse proxy thật; backup có mã hóa và thử restore. Không suy ra “đã an toàn tuyệt đối” chỉ vì có OTP/JWT. Không có phát hiện rò rỉ key được xác nhận trong đợt này.

Ý tưởng: owner/admin/support/finance theo quyền cụ thể; thao tác nhạy cảm yêu cầu đăng nhập lại; audit actor, target, reason, thời điểm và correlation ID. Trước khi lưu transcript hoặc memory lâu dài, cho người dùng thấy và kiểm soát. Account deletion là quy trình riêng vì có bản ghi kinh doanh/audit cần xử lý theo chính sách; không xóa tràn lan quan hệ đang được bảo vệ.

### 5.12. Kiến trúc — 6,5/10

Điểm tốt: Domain/Application/Infrastructure/API, pure planning engine, dependency injection, hợp đồng rõ, typed HttpClient và cấu hình ngoài code. Không cần chuyển sang microservices để hệ thống trông chuyên nghiệp.

Điểm cần sửa: WorkspaceService là partial class trải nhiều file nhưng vẫn một service nhiều trách nhiệm. Frontend vừa dùng React state vừa dùng global state/controller/event delegation. Snapshot hiện tại có `controller.jsx` 1151 dòng, `pages.jsx` 939, `agent.jsx` 1203, `notes.jsx` 746; các con số thay đổi khi working tree tiếp tục được sửa. `nora-chat.css` hơn 2000 dòng, `getting-started.css` hơn 2400; các stylesheet đều được import qua index. Đây là dấu hiệu chi phí bảo trì, không phải cứ nhiều dòng là code sai.

Ý tưởng: vẫn một ứng dụng triển khai, chia module có ranh giới: Account, Planning, Execution, Notes, Assistant, Notifications, Rewards, Commerce, Administration. Tách query khỏi mutation; chỉ một nguồn cho state dữ liệu server. Route-level components/hooks, shared form/dialog primitives và design tokens. Di chuyển theo từng module, không rewrite toàn frontend cùng lúc.

### 5.13. Hiệu năng — 5,0/10

Vấn đề đã thấy: asset no-store; ảnh sprite lớn; payload workspace có nhiều lịch sử; một thao tác nhỏ có thể kéo theo recalculate rộng. Chưa có số đo để khẳng định mức người dùng tối đa.

Ý tưởng: trước tiên đo p50/p95 API, query count, payload bytes, memory, duration worker và thời gian đến chữ đầu tiên của NORA. Sau đó tối ưu query/projection/cache static và cập nhật incremental. Thêm DB index dựa trên execution plan, tránh tạo index hàng loạt theo phỏng đoán. Cache lịch cá nhân phải gắn revision/ownership, không chia nhầm dữ liệu user.

### 5.14. Kiểm thử — 7,0/10

Nền tốt: unit, HTTP integration, SQL concurrency, Vitest/Testing Library; CI có locked restore, build, SQL Server test riêng, migration consistency và dependency audit. Không được viết rằng dự án “chưa có CI” hoặc “chưa có SQL tests”.

Cần thêm: đưa scenario vừa tái hiện vào regression; browser E2E repeatable trong repository/CI; test nối giữa policy AI và planner; property-based tests không chồng lịch/không vượt giờ rảnh; performance baseline; đánh giá AI có tập câu version hóa. Lịch sử green ở một lần chạy không chứng minh bản working tree hiện tại green.

Đợt đánh giá này không chạy lại toàn bộ backend/SQL suite; kết quả backend từ các lần làm việc trước không được gắn nhãn mới. Một ca frontend thất bại trong batch rồi đạt khi chạy riêng cần được xử lý như tín hiệu test chưa ổn định.

### 5.15. Vận hành — 4,0/10

Đã có dev scripts khá đầy đủ, kiểm tra kết nối AI, job history và trạng thái worker. Compose hiện phục vụ SQL Server Developer/Mailpit cục bộ và volume dữ liệu. Đây không phải cấu hình production hoàn chỉnh.

Chưa thấy trong phạm vi repository: quy trình deploy/rollback production rõ, health/readiness endpoint chuyên dụng, telemetry/tracing có hệ thống, cảnh báo SLO, backup/restore automation và runbook đầy đủ. Hạ tầng ngoài repository có thể đã có; cần kiểm tra riêng trước kết luận.

Ý tưởng: deploy artifact có version; migration chạy qua job riêng; health kiểm tra tối thiểu; queue/job lag, AI error rate, quota/cost, DB latency và outbox pending có dashboard. Đặt RPO/RTO mục tiêu và diễn tập restore từ backup, không coi “có file backup” là hoàn thành. Chọn mục tiêu phù hợp quy mô và kiểm chứng, không tự nhận SLA 99,9%.

### 5.16. Admin — 5,0/10

Đã có tìm user, khóa/mở, chống tự khóa, thu hồi refresh session khi đổi trạng thái, audit lý do, job logs và trạng thái database/worker. API có yêu cầu role Admin.

Giới hạn: query tìm kiếm chỉ trả tối đa 100; chưa có phân trang/tổng số; tìm kiếm chưa thấy username; một role Admin phục vụ nhiều việc; chưa có workflow ticket hỗ trợ hay xem AI usage. Các log trạng thái/account và job không thay thế sổ tiền.

Ý tưởng: User Operations, AI Operations, Finance, Content/Shop, Audit. Tách quyền đọc và thao tác; support không được sửa giá hoặc tự hoàn tiền; finance không cần đọc toàn bộ ghi chú riêng. Mọi điều chỉnh XP/entitlement cần lý do và audit. Không cung cấp màn hình tùy ý sửa số dư DB.

## 6. Thiết kế sản phẩm đề xuất: NORA làm trung tâm vòng lặp sử dụng

### 6.1. Vòng lặp cốt lõi

1. Người dùng cho biết giờ rảnh và mục tiêu.
2. NORA hiểu yêu cầu, hỏi đúng thông tin còn thiếu.
3. Planner tạo phương án thực tế; hiển thị điều phải đánh đổi.
4. Người dùng áp dụng hoặc đã bật tự lưu cho nhóm thao tác được phép.
5. Người dùng thực hiện một phiên, ghi nhận kết quả.
6. Hệ thống cập nhật độ chính xác/tiến độ và đề xuất phục hồi nếu lệch kế hoạch.
7. Pet phản hồi bằng thành tích nhỏ, không cản việc thực hiện bước tiếp theo.

Mỗi màn hình nên phục vụ một bước rõ. Không cần biến mọi thứ thành chat: lịch lưới vẫn phù hợp để kéo/sửa thời gian chính xác, form phù hợp kiểm tra hàng loạt, chat phù hợp diễn đạt ý định và hỏi lại.

### 6.2. Năm ý tưởng nên ưu tiên

**A. NORA Morning Brief.** Hôm nay có việc gì, còn bao nhiêu thời gian khả dụng, deadline quan trọng nhất và một đề xuất bắt đầu. Mỗi số liệu liên kết về nguồn, không AI tự nghĩ ra. Cho xem on-demand trước khi tự gửi notification hàng ngày.

**B. Recovery Mode.** “Bạn đã lỡ hai phiên. Giữ cuộc họp và lịch khóa; mình tìm được 45 phút chiều nay, còn thiếu 30 phút trước hạn.” Cho ba lựa chọn khả thi, không nói đã sắp xếp thành công khi vẫn thiếu thời gian.

**C. Why this schedule?** Bấm vào phiên để thấy lý do: deadline, dependency, khoảng rảnh, thời lượng và thay đổi so với bản trước. Giải thích bằng dữ kiện từ planner; model chỉ diễn đạt, không phát minh nguyên nhân.

**D. Weekly Review.** So kỳ 7 ngày với kỳ trước: đúng hạn, phiên bị lỡ, thời gian tập trung, estimate error và một đề xuất thay đổi. Nếu chỉ có hai phiên thì nói mẫu nhỏ. Người dùng chọn áp dụng thay đổi sở thích.

**E. Campus Templates.** Mẫu báo cáo SWP, ôn thi, bài tập nhóm với milestone. Đây là giả thuyết phù hợp các ví dụ bạn đã đưa; cần chọn một nhóm người dùng đầu tiên để tránh làm cùng lúc công cụ sinh viên, freelancer và quản lý doanh nghiệp.

### 6.3. Quy tắc khi trễ hai ngày

Định nghĩa phải rõ: hai ngày liên tiếp có phiên đến hạn mà không thực hiện, hay task quá deadline hai ngày? Hai trường hợp không thể xử lý giống nhau.

Luồng đề xuất:

- Worker xác định task/session bị ảnh hưởng theo timezone và bản kế hoạch.
- Loại task Completed/Cancelled; không đụng vào Running/Paused hoặc phiên khóa.
- Tạo một recovery proposal với revision, trigger và khóa dedupe.
- Nếu deadline cứng đã qua, xin người dùng chọn gia hạn/hủy/đổi ưu tiên; không tự đổi deadline.
- Nếu có phương án đủ thời gian trước deadline, preview diff; chỉ tự apply khi user đã bật policy đó.
- Nếu không đủ thời gian, hiển thị phần thiếu và lựa chọn nới deadline/mở giờ rảnh/giảm phạm vi.
- Gửi tối đa một nhắc phù hợp và tôn trọng quiet hours; lưu lịch sử lý do dời.
- Áp dụng giữ idempotency, recheck revision và có đường undo trong phạm vi còn an toàn.

Acceptance: chạy lại worker hoặc mất kết nối không tạo hai kế hoạch; user đổi lịch giữa generate/apply thì proposal cũ bị chặn; không tự đẩy việc sau hard deadline rồi báo thành công.

## 7. Pet, level, streak và skin: thiết kế để hỗ trợ công việc

### 7.1. Tách mascot UI và pet kinh tế

NORA hiện là mascot/trợ lý. Hình ảnh và frame animation không đồng nghĩa đã có pet engine. Nên quyết định: NORA là pet phát triển của chính bạn, hay NORA là người hướng dẫn còn pet là nhân vật khác. Đề xuất thử **một NORA đồng hành có hình thái mở khóa** trước; không tạo thêm hai hệ nhân vật khi chưa cần.

Các level bạn muốn giữ: **1, 5, 10, 15, 20, 50, 100**. Mỗi mốc có hình thái/chi tiết rõ; giữa các mốc dùng tiến độ XP nhẹ. Chưa nên sản xuất đủ 100 mức hình ảnh: chỉ cần 7 trạng thái tiến hóa, skin là lớp tách biệt.

### 7.2. Luật thưởng

- Server cấp XP cho outcome hợp lệ, không lấy con số từ frontend.
- Mỗi sự kiện được thưởng một lần qua khóa duy nhất `(user, rewardType, sourceId)`.
- Tách thưởng hoàn thành task, giữ lịch và tập trung; không thưởng vô hạn chỉ vì để timer chạy.
- Manual-adjusted duration không tự tạo thêm XP tương ứng mà thiếu kiểm tra.
- Mở lại task rồi hoàn thành lại không phát thưởng lặp.
- Có trần/ngày và quy tắc cho việc siêu ngắn, chia task để farm, replay request và hai tab.
- Hoàn tác/correction tạo dòng điều chỉnh, không sửa mất dấu vết ledger.
- Nghỉ theo lịch vẫn hợp lệ; pet không bị bệnh hoặc “trừng phạt” khi người dùng nghỉ.
- Skin mua bằng tiền không tăng năng suất giả, không tăng XP để vượt người khác.

Không ấn định đường cong XP cuối cùng chỉ bằng cảm giác. Prototype vài tuần để xem người dùng đạt mốc 5 quá nhanh hay quá chậm. Giá và tỷ lệ chuyển đổi là giả thuyết cần thử, không có cơ sở để dự báo doanh thu trong báo cáo này.

### 7.3. Streak

Streak không nên chỉ là số ngày mở app. Đề xuất “ngày giữ cam kết”: hoàn thành ít nhất một mục tiêu hợp lệ trên ngày có lịch, timezone được chốt theo policy. Ngày nghỉ đã lên lịch không phá chuỗi. Có cơ chế quay lại sau khi lỡ; không dùng cảm giác tội lỗi để buộc đăng nhập.

Cần chống farm đổi timezone, thay ngày hệ thống client, hoàn thành/hủy/hoàn thành lại, và cập nhật dữ liệu muộn. Server quyết định ngày thưởng; có thời hạn ghi nhận bù và log giải thích.

### 7.4. Các bảng đề xuất

| Nhóm | Entity | Nội dung chính |
|---|---|---|
| Pet | PetDefinition, PetEvolution, UserPet | Loại, hình thái theo level, pet đang dùng |
| Reward | XpLedger, RewardRuleVersion | XP cộng/trừ, nguồn, rule version, idempotency key |
| Chuỗi | DailyCommitment, StreakState | Ngày theo timezone, điều kiện đạt, ngày nghỉ, trạng thái |
| Skin | SkinCatalog, UserInventory | Asset/version, khả dụng, quyền sở hữu |
| Thương mại | Order, OrderItem, PaymentAttempt | Giá server, currency, phiên bản hàng, trạng thái |
| Thanh toán | ProviderEventInbox, Refund | Event ID duy nhất, kết quả xác thực, hoàn tiền |
| Quyền lợi | EntitlementGrant | User nhận skin nào từ order nào, trạng thái/thu hồi |
| Kiểm soát | AuditEvent | Người thao tác, lý do, correlation, thời điểm |

Tất cả là thiết kế đề xuất, chưa phải bảng đang có trong hệ thống.

## 8. Thanh toán skin và admin tài chính

### 8.1. Nên bắt đầu bằng mua trực tiếp

Đề xuất giai đoạn đầu: một đơn hàng mua một hoặc nhiều skin, thanh toán qua checkout của nhà cung cấp, nhận entitlement. Chưa cần ví/nạp số dư: ví làm tăng vấn đề số dư, hoàn tiền, thu hồi và đối soát. Lựa chọn cổng phụ thuộc thị trường, điều kiện merchant và nhu cầu thực tế; báo cáo này không giả định bạn đủ điều kiện mở merchant tại bất kỳ nhà cung cấp cụ thể nào.

### 8.2. Luồng có thể kiểm chứng

1. Frontend gửi productId/quantity; server lấy giá hiện hành và tạo order bất biến với currency.
2. Server tạo payment attempt bằng idempotency key của order/attempt.
3. Người dùng thanh toán trên checkout phù hợp của provider.
4. Trang quay lại chỉ hiển thị trạng thái đang xác nhận; query string “success” không cấp skin.
5. Webhook/callback server xác minh chữ ký, provider event/transaction ID, merchant, order, số tiền và currency.
6. Ghi nhận event chống trùng; cập nhật payment và entitlement trong transaction hoặc qua inbox/outbox bền vững.
7. Nếu webhook gửi lại, không cấp skin lần hai. Nếu đến sai thứ tự, state transition không lùi PAID thành PENDING.
8. Có reconciliation job đối chiếu đơn pending/paid và giao dịch provider.
9. Refund/chargeback tạo điều chỉnh có dấu vết và policy quyền sử dụng skin rõ ràng.

Các lưu ý chữ ký, event trùng và không đảm bảo thứ tự được mô tả cụ thể trong [Stripe webhook documentation](https://docs.stripe.com/webhooks). Đây là nguồn tham khảo thiết kế, không phải lựa chọn Stripe cho tài khoản của bạn; khi chọn provider phải đối chiếu hợp đồng webhook/IPN của chính provider đó.

### 8.3. Không gọi một bảng tổng là “quản lý dòng tiền”

Admin cần phân biệt: tổng giá trị đơn, giao dịch đã xác nhận, refund, phí, khoản provider thực chuyển, và chi phí AI/hạ tầng. “Số đơn × giá skin” không phải lợi nhuận. Những số liệu này cần nguồn, thời kỳ, currency và trạng thái rõ.

Màn hình đề xuất: overview, orders, payments, refunds, reconciliation, products/skins, AI usage và audit. Export có quyền riêng; xem dữ liệu riêng tư của user không được mở rộng chỉ vì có quyền finance.

Acceptance tối thiểu trước tiền thật: sai chữ ký không cấp skin; callback giả không cấp skin; event trùng chỉ cấp một lần; sai amount/currency/order bị chặn; mất kết nối sau commit retry không tạo thêm order; refund không xóa lịch sử; phân quyền và audit có test.

## 9. Ngày lễ Việt Nam, lịch ngoài và các tình huống thời gian

### 9.1. Calendar ngày lễ riêng

- Có nguồn dữ liệu, năm, timezone, phiên bản và ngày cập nhật; lưu rõ độ tin cậy.
- Phân biệt ngày kỷ niệm, ngày lễ và lịch nghỉ thực tế theo năm. Không dùng LLM tự suy diễn lịch nghỉ bù hoặc chuyển đổi ngày âm mà không kiểm tra.
- Dedupe theo nguồn + occurrence; cập nhật năm mới không tạo trùng.
- Cho bật/tắt từng lớp và chọn “chỉ nhắc”, “đánh dấu bận” hoặc tự chọn giờ.
- Nhắc trước ngày theo giờ người dùng chọn; thêm sự kiện riêng không làm mất nguồn lễ.
- Ngày lễ không mặc nhiên là ngày rảnh đối với mọi user; lịch học/làm của từng người vẫn quyết định.

Acceptance: bật/tắt lớp không xóa sự kiện cá nhân; đổi timezone không tạo hai reminder; source cập nhật không ghi đè chỉnh sửa riêng; một lần worker chạy lại không gửi trùng.

### 9.2. Đồng bộ Google/Outlook Calendar

Google login hiện không cung cấp quyền calendar. Cần một luồng kết nối riêng, scope tối thiểu theo tính năng, trạng thái kết nối và nút thu hồi.

Nên làm đọc lịch bận trước, ghi phiên ra lịch sau. Lưu external calendar ID/event ID, sync cursor/version và link nguồn. Những phiên do PLANORA tạo rồi đồng bộ ra ngoài không được nhập ngược như sự kiện bận độc lập làm tự xung đột. Cần quy tắc khi user sửa cả hai nơi, khi external event bị xóa và khi token hết hạn.

Không biến lỗi Google Calendar thành việc mất task hoặc không mở được workspace. Dùng queue/outbox cho thao tác sync và hiển thị “đang đồng bộ / cần kết nối lại”. Credentials kết nối phải được bảo vệ như credential server, không lưu dạng rõ ở localStorage.

## 10. Kiến trúc đích vừa đủ

Giữ một hệ thống triển khai được đơn giản, chia các module có trách nhiệm rõ:

- **Planning:** availability, constraints, feasibility, proposals và explanation.
- **Execution:** state machine phiên, segments, actual và outcome.
- **Assistant:** context selection, model provider, intent validation, tool policy, streaming và eval.
- **Insights:** read model/snapshot và truy vấn khoảng ngày.
- **Notifications:** nhắc, delivery, retry/dedupe, quiet hours.
- **Rewards:** XP ledger, streak, pet, inventory; không phụ thuộc việc client tự báo số XP.
- **Commerce:** order, payment adapter, inbox/outbox, entitlement và reconciliation.
- **Identity/Admin:** tài khoản, quyền, audit và thao tác hỗ trợ.

Quy tắc chung: mutation có ownership, concurrency/idempotency; sự kiện nghiệp vụ chỉ phát khi transaction hợp lệ; side effect ngoài hệ thống có outbox/retry; dữ liệu hiển thị có nguồn và revision; không dùng model để xác nhận đã thanh toán hay tự cấp quyền.

Chỉ thêm queue chuyên dụng, distributed cache, vector store hoặc solver khi có nhu cầu đo được. Cần tránh một đợt nâng cấp đồng thời framework + toàn bộ frontend + database + thanh toán; khi lỗi sẽ khó khoanh vùng và rollback.

## 11. Thứ tự làm và backlog có tiêu chí nghiệm thu

Mức ưu tiên:

- **P0:** chặn việc mở nhận tiền hoặc public launch khi một điều kiện bắt buộc chưa được chứng minh. Không dùng nhãn này để ám chỉ đã phát hiện xâm nhập.
- **P1:** tính đúng đắn/độ tin cậy hoặc rủi ro chi phí cần sửa sớm.
- **P2:** độ hoàn thiện, bảo trì và tối ưu sau khi số liệu cốt lõi đáng tin.
- **P3:** mở rộng/khám phá cần thẩm định với người dùng.

Ước lượng dưới đây là khoảng làm việc tập trung cho người quen codebase, chưa gồm thời gian chờ tài khoản provider, review, thử người dùng hoặc xử lý vấn đề phát hiện thêm. Không phải cam kết tiến độ.

| Thứ tự | Công việc | Mức | Phạm vi/đầu ra nghiệm thu | Ước lượng sơ bộ |
|---|---|---|---|---|
| 1 | Thống nhất capacity và risk | P1 | Dashboard/planner/insights cùng giới hạn ngày; case 60/120 phút cảnh báo nhất quán | 2–4 ngày công |
| 2 | Parent dependency và resume validator | P1 | Các ca F03/F04 có regression/integration; không phá protected sessions | 2–4 |
| 3 | Kế hoạch .NET 10 LTS | P1 | Build/test/migration rehearsal/rollback; hoàn tất trước khi vận hành qua mốc EOL | 3–6 |
| 4 | Quota, usage và concurrency AI | P1 | Có số liệu theo user/model, chặn budget trước call, xử lý 429/retry an toàn | 3–5 |
| 5 | Vòng đời hội thoại | P1 | Cursor, conversation list, archive/delete, tìm kiếm server; không dead-end 2000 lượt | 3–5 |
| 6 | Recovery Mode | P1 | Trigger bỏ lỡ/trễ được định nghĩa; preview diff; không đổi hard deadline im lặng | 4–7 |
| 7 | Insights theo actual | P1 | 3/7/30 ngày, CompletedAt/segments, timezone, mẫu nhỏ và drill-down | 3–5 |
| 8 | Cache/static và query nhỏ | P2 | Hash assets cache đúng; API riêng theo trang; có số đo trước/sau | 2–4 |
| 9 | Sửa test không ổn định, đưa browser gate vào CI | P2 | Batch lặp ổn định, main flow trên mobile/desktop và SSE cancel/retry | 2–4 |
| 10 | Staging, backup/restore, monitoring | P0 trước launch | Diễn tập restore/rollback, theo dõi worker/AI/DB, runbook và người chịu trách nhiệm | 4–7 |
| 11 | Pet/XP không tiền thật | P2 | Ledger/idempotency, level 1/5/10, chống farm, thử người dùng | 4–7 |
| 12 | Shop/payment/admin finance | P0 gate trước tiền thật | Webhook, entitlement, refund, reconcile, phân quyền và audit pass | 7–12 |

Các công việc có thể chồng lấn hợp lý nếu đội ngũ đủ người, nhưng không được bỏ phụ thuộc: XP ledger trước skin/entitlement; order/payment model trước admin doanh thu; actual metrics trước việc thưởng “giữ đúng lịch”.

## 12. Lộ trình sản phẩm gợi ý

Đây là thứ tự thực hiện, không phải lịch phát hành đã cam kết. Với 1–2 người phát triển còn phải học provider, thời gian thực tế có thể dài hơn nhiều so với tổng ngày công.

### Đợt A — Làm lõi đáng tin

Sửa F01–F04, định nghĩa completion/progress, policy lập lịch của NORA, xử lý test không ổn định. Mở nhánh nâng .NET ngay trong đợt này. Đo API và AI hiện tại, ghi baseline.

Điều kiện qua: các ví dụ phản chứng không còn mâu thuẫn; task còn thiếu thời gian không bị thể hiện là an toàn; resume có chính sách; auth/refresh/OTP/SQL concurrency không suy giảm sau thay đổi.

### Đợt B — Làm NORA hữu ích khi dùng lâu

Conversation lifecycle, context có phạm vi, question/options có schema, quota/cost, Recovery Mode và statistics theo actual. Onboarding đi đến một phiên làm việc thật.

Điều kiện qua: user biết vì sao lịch đổi, chat cũ tìm được, lỡ phiên có phương án khả thi và không phát sinh thao tác trùng khi retry.

### Đợt C — Thử với nhóm nhỏ và pet MVP

Một pet, ba mốc đầu, XP ledger và streak có ngày nghỉ; không cần skin trả tiền. Chạy pilot với nhóm mục tiêu nhỏ, theo dõi hành vi và phỏng vấn lý do bỏ dùng.

Điều kiện qua: người dùng dùng được phần lập lịch mà không cần hướng dẫn liên tục; pet hỗ trợ quay lại công việc thay vì chỉ tạo click. Chưa có dữ liệu thì chưa tuyên bố retention tốt hoặc sẵn sàng trả tiền.

### Đợt D — Vận hành và thương mại hóa có điều kiện

Staging/production, restore/rollback, security review; cổng thanh toán, shop và finance admin. Mở tiền thật sau acceptance của payment và vận hành, không chỉ sau khi trang “thành công” trông đẹp.

Calendar sync/ngày lễ có thể làm theo nhu cầu pilot; đọc lịch bận thường mang giá trị trực tiếp hơn việc thêm nhiều mẫu skin sớm.

## 13. Bộ chỉ số nên có

Tất cả dưới đây là chỉ số cần thu thập hoặc mục tiêu thử nghiệm; chưa phải số đo hệ thống đang đạt.

| Nhóm | Chỉ số | Cách hiểu đúng |
|---|---|---|
| Onboarding | Người có lịch đầu tiên / người mới bắt đầu | Chọn cohort rõ, không tính chỉ mở trang |
| Giá trị cốt lõi | Phiên hợp lệ được thực hiện / phiên đến hạn | Loại/tách các phiên chủ động hủy và ghi rõ policy |
| Kết quả | Task đúng hard deadline / task đã đến hạn | Phân biệt đổi deadline sau khi đã trễ |
| Chất lượng lịch | Số phiên bị dời / lần reschedule | Đo mức xáo trộn, không chỉ số phiên được tạo |
| Recovery | Đề xuất được chấp nhận và sau đó thực hiện | Không lấy click “áp dụng” làm thành công cuối cùng |
| AI | Intent hợp lệ, hỏi lại phù hợp, action đúng trên eval set | JSON hợp lệ chưa đủ để nói AI hiểu đúng |
| Tốc độ AI | p50/p95 thời gian đến chữ đầu và đến final | Tách model, voice, mạng và lưu lịch |
| Chi phí | Token/audio/provider cost ước tính theo account | Đối chiếu bill, version bảng giá, không giả định credit vô hạn |
| UX | Task completion rate, lỗi/phiên, số lần cần trợ giúp | Dùng usability test có nhiệm vụ cụ thể |
| Vận hành | API error rate, worker lag, retry/outbox age | Alert có người xử lý và runbook |
| Pet | User quay lại làm việc sau phản hồi pet | Không tối ưu thời gian ngắm pet như mục tiêu chính |
| Payment | Giao dịch xác nhận, grant thành công, refund/reconcile mismatch | Không tính return URL thành giao dịch đã thu tiền |

Mục tiêu đo ban đầu có thể là: 100% hard constraints trong bộ regression; không side effect trùng trong các test retry; không grant từ callback giả; task cơ bản trên mobile làm được không cần hướng dẫn. Các ngưỡng latency/retention/cost cụ thể nên đặt sau khi có baseline và khả năng hạ tầng thực.

## 14. Những việc chưa nên đầu tư lớn ngay

1. Huấn luyện một LLM riêng: dữ liệu và vấn đề hiện tại chủ yếu là policy, context, planner và trải nghiệm; fine-tune không tự sửa sai capacity hay webhook.
2. Nhiều agent tự gọi nhau: thêm chi phí, latency và khó kiểm soát side effects nếu chưa có eval và policy vững.
3. Microservices toàn bộ: chia module trong ứng dụng hiện tại trước, đo điểm nghẽn rồi mới tách triển khai.
4. Vector database cho mọi task: query cấu trúc theo ngày/ID/trạng thái thường phù hợp hơn với lịch.
5. Nạp ví, nhiều loại tiền ảo và giao dịch giữa user ngay từ đầu: chưa cần cho nhu cầu mua skin.
6. Feed vô hạn hoặc leaderboard ép so giờ tập trung: có thể đi ngược mục tiêu giảm quá tải và khuyến khích farm.
7. Làm thật nhiều animation để thay cho trạng thái lỗi/rỗng/không kết nối: đẹp phải đi cùng khả năng hiểu và hoàn tất tác vụ.
8. Đưa ra điểm năng suất tuyệt đối khi dữ liệu còn ít hoặc actual do người dùng chỉnh: cần giải thích và khoảng bất định.

## 15. Thử nghiệm người dùng đề xuất

Tuyển một nhóm nhỏ thuộc một persona, thí dụ sinh viên đang làm SWP; đây là đề xuất thử nghiệm, chưa thực hiện.

Nhiệm vụ: nhập lịch rảnh khác nhau trong tuần; tạo báo cáo hai giờ hạn 18:00; thêm cuộc họp chiếm giờ dự kiến; làm một phiên bị gián đoạn; quay lại hôm sau; hỏi NORA vì sao lịch đổi; kiểm tra tiến bộ; tìm hội thoại cũ.

Ghi lại: chỗ do dự, cách gọi thuật ngữ, số lần sửa giờ sai, mức tin vào lịch, lý do từ chối reschedule và mong đợi về pet. Không cho người điều phối giải thích trước mọi bước. Sau đó mới quyết định nên tăng đầu tư chat, lưới lịch hay pet.

Các câu cần trả lời: người dùng biết đâu là lịch đã lưu chưa? hiểu capacity không? thấy lời hỏi lại đúng lúc hay phiền? có muốn tự apply mọi thay đổi không? có cảm thấy bị phạt khi nghỉ? liệu họ mua skin vì thích hay chỉ bấm thử? Không suy ra sẵn sàng trả tiền từ lời khen giao diện.

## 16. Dấu vết kiểm chứng và nguồn

### Bằng chứng tạo trong đợt đánh giá

- `artifacts/system-review-probes/ReviewProbes.csproj` và `Program.cs`: harness không package ngoài, link trực tiếp engine/entity/DTO nguồn; không gọi database/AI.
- `artifacts/system-review-planner-results.txt`: hai trường hợp planner/risk.
- `artifacts/system-review-dashboard-probe.mjs` và `system-review-dashboard-results.json`: khả năng 480 phút so với giới hạn 60.
- `artifacts/system-review-frontend-tests.json`: 91 ca, 90 đạt, 1 fail.
- `artifacts/system-review-agent-recheck.json`: chạy riêng ca fail, 1 đạt, 9 không được chọn.
- `artifacts/system-review-pages-check.mjs`, `system-review-pages.json` và ảnh `system-review-{route}-{width}.png`: mẫu UI với fixture ở 16 trường hợp.

### Mã nguồn ưu tiên đọc khi triển khai

| Khu vực | Điểm bắt đầu |
|---|---|
| Ràng buộc/capacity/risk | `src/SPWPOS.Application/Workspace/PlanningEngine.cs` |
| Truy vấn snapshot và transaction | `src/SPWPOS.Infrastructure/Workspace/WorkspaceService.cs` |
| Time change và auto reschedule | `src/SPWPOS.Infrastructure/Workspace/TimeService.cs` |
| Phiên/timer | `src/SPWPOS.Infrastructure/Workspace/ExecutionService.cs` |
| AI intent/context/apply/undo | `src/SPWPOS.Infrastructure/Workspace/AgentService.cs`, `AgentInterpreter.cs` |
| Transport/provider | `GoogleCloudAi.cs`, `AgentStreaming.cs`, `AgentVoice.cs` |
| Note import/version | `src/SPWPOS.Infrastructure/Workspace/NoteService.cs` |
| Notification/admin/worker | `NotificationAdminService.cs`, `JobHistoryService.cs` |
| Auth/middleware/cache/limit | `src/SPWPOS.Api/Program.cs`, BrowserSessionController, AuthService |
| Dashboard/chart | `frontend/src/dashboard-model.js`, `pages.jsx` |
| UI route và khởi động | `frontend/src/controller.jsx`, `shell.jsx` |
| Chat/voice | `frontend/src/agent.jsx`, `voice-input.js`, `nora-progress.jsx` |
| Schema/ownership | `src/SPWPOS.Infrastructure/Persistence/Configurations/WorkspaceModel.cs` |
| Quality gates | `.github/workflows/ci.yml`, `frontend/tests`, `tests/SPWPOS.*Tests` |

Các line references trong phát hiện là ở snapshot lúc rà soát; working tree đang có thay đổi nên số dòng có thể dịch chuyển.

### Nguồn chính thức đã đối chiếu

- [Microsoft — .NET support lifecycle](https://dotnet.microsoft.com/en-us/platform/support/policy/dotnet-core): mốc .NET 8/.NET 10.
- [Google Cloud — Vertex AI throughput quota](https://docs.cloud.google.com/vertex-ai/generative-ai/docs/resources/throughput-quota): quota/429/throughput.
- [Google Cloud — spend cap budgets](https://docs.cloud.google.com/billing/docs/how-to/budgets-spend-caps): cap theo dịch vụ đủ điều kiện và độ trễ enforcement.
- [W3C — WCAG 2.2](https://www.w3.org/TR/WCAG22/): tiêu chí accessibility; cần audit riêng để kết luận tuân thủ.
- [W3C — target size minimum](https://www.w3.org/WAI/WCAG22/Understanding/target-size-minimum.html): vùng tương tác nhỏ và ngoại lệ.
- [Stripe — webhooks](https://docs.stripe.com/webhooks): xác thực chữ ký, duplicate và thứ tự event; không thay cho tài liệu provider được chọn.

**Quyết định đề xuất:** phê duyệt hướng ưu tiên làm lịch/số liệu nhất quán và phục hồi kế hoạch; thử pet không tiền thật sau khi các nguồn XP đúng; chỉ mở shop/payment khi cơ chế cấp quyền và vận hành đã được kiểm chứng.
