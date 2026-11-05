# Kế hoạch NORA chủ động: báo cáo ngày, gợi ý việc và xử lý lệch lịch

Ngày: 04/11/2026. Trạng thái: kế hoạch đề xuất từ feedback người dùng; chưa triển khai các tính năng mới trong tài liệu này. Đối chiếu source local hiện tại. Giữ model `gemini-3.8-flash`, Notes và luồng duyệt nhóm.

## 1. Feedback thực sự nói gì?

Người dùng cần hệ thống tổng hợp thông tin và giảm các quyết định nhỏ hằng ngày. Có ba nhu cầu liên quan nhưng khác nhau:

1. **Biết tình hình:** hôm nay có gì cố định, việc nào cần làm, việc nào có nguy cơ và còn bao nhiêu thời gian dùng được.
2. **Biết hành động tiếp:** bắt đầu việc gì, vì sao, vào lúc nào và có cần thay đổi lịch hiện tại không.
3. **Thích nghi khi thực tế thay đổi:** khi bỏ lỡ/làm chưa xong, đưa ra phương án đã kiểm tra để chọn, không bắt tự nghĩ prompt hoặc tự sắp xếp lại.

Vì vậy, báo cáo mỗi ngày là điểm vào; năng lực chính cần nâng cấp là vòng `nhìn tình hình → đề xuất → áp dụng → ghi nhận thực tế → điều chỉnh`. Chỉ thêm một đoạn chat chào buổi sáng sẽ chưa giải quyết được feedback.

## 2. Phần hiện có và phần phải bổ sung

| Nhu cầu | Nền tảng có thể tái dùng | Khoảng trống |
| --- | --- | --- |
| Phân tích hôm nay | `AgentScheduleReview`, `dashboardModel`, lịch và phân tích khả năng | Chủ yếu cần người dùng gọi; chưa có bản báo cáo đầu ngày thống nhất với hành động |
| Chọn việc nên làm | `EisenhowerFocus`, `AssessEisenhower`, rủi ro và phụ thuộc | Gợi ý và lập lịch chưa thành một trải nghiệm đầu ngày dùng chung dữ liệu |
| Xếp lịch | `PlanningEngine`, PlanProposal/apply, bảo vệ deadline và phiên cố định | Cần phương án riêng cho phần còn lại hôm nay, giải thích tác động, tránh lập lịch lại trùng |
| Phiên hết giờ | `WorkTimeReview`, thêm giờ, hoàn thành sớm | Thiếu lựa chọn chuyển/chia/giữ danh sách ngay trong một luồng chung |
| Phiên bị lỡ | Catch-up và `RecoveryService` | Catch-up và phục hồi dài ngày còn tách biệt; chưa đưa nhiều lựa chọn cụ thể đã kiểm tra cho một phiên |
| Nhắc chủ động | Worker thông báo, Telegram outbox/dedupe | Chưa có báo cáo theo ngày, giờ gửi cá nhân, chống gửi lặp và cập nhật nội dung theo ngày |
| Giảm nhập liệu | Inbox, bản nháp, NORA hiểu mục đích, ước lượng cần xác nhận | Inbox chưa là task thống nhất; NORA Agent chưa có công cụ ghi Inbox. Không được giả định phần này đã hoàn chỉnh |

Đặc biệt: câu “Hôm nay tôi nên làm gì?” hiện thuộc tư vấn/đọc dữ liệu; chính sách Agent không cho phép công cụ ghi lịch từ mục đích này. Giữ nguyên sự phân biệt; bổ sung hành động “Xếp lịch giúp tôi” hoặc “Áp dụng phương án”.

## 3. Trải nghiệm đích

### 3.1. Đầu ngày: mở PLANORA là có hướng xử lý

Thẻ đầu tiên trên Hôm nay là **NORA · Kế hoạch hôm nay**. Không yêu cầu nhập prompt để có thẻ.

Ví dụ minh họa, chỉ hiển thị nếu số liệu thực tế đúng:

> Chào buổi sáng! Hôm nay bạn có 2 lịch học cố định và 3 việc cần xử lý. Báo cáo SWP đang thiếu 60 phút có thể xếp trước hạn theo phương án hiện tại. Mình đề xuất ưu tiên SWP; phiên tiếng Nhật có thể cân nhắc dời nếu vẫn giữ được hạn của việc đó.

Thẻ gồm:

- Tóm tắt lịch cố định, phiên đã có, quỹ thời gian có thể làm việc còn lại.
- Tối đa 3 việc ưu tiên, mỗi việc một lý do ngắn: Q1/Q2, deadline, thiếu giờ, tiền nhiệm hoặc người dùng đã chọn.
- Rủi ro chính, bao gồm thiếu dữ liệu để đánh giá; không biến thiếu thời khóa biểu thành thiếu thời gian.
- Hành động: **Bắt đầu việc tiếp theo**, **Xem lịch đề xuất**, **Điều chỉnh hôm nay**.

Không phải mọi Q1 đều được làm ngay: tiền nhiệm, giờ cố định và độ khả thi vẫn quyết định slot. Nếu lịch hiện tại đã phù hợp, thông báo “Lịch hôm nay đã phù hợp” và dẫn tới phiên tiếp theo thay vì tạo lịch mới.

### 3.2. Hỏi NORA cùng một câu sẽ nhận cùng một cơ sở tư vấn

“Hôm nay nên làm gì?”, “Tôi nên bắt đầu từ đâu?” hoặc “Hôm nay ưu tiên việc nào?” dùng cùng dịch vụ báo cáo/ngữ cảnh với thẻ Hôm nay. Câu trả lời nêu việc cụ thể và lý do; không đi qua một chuỗi công cụ đọc lại toàn bộ dữ liệu.

Nếu hỏi “Xếp lịch hôm nay giúp tôi”, mở bản đề xuất cho phần còn lại của ngày. Thay đổi được mô tả rõ: phiên mới, phiên giữ nguyên, phiên cần dời và phần việc còn lại ngoài hôm nay.

Không hứa hoàn thành mọi công việc trong một ngày. Việc dài có thể chỉ làm một phần hôm nay; không đủ quỹ thì báo số phút chưa xếp và các lựa chọn khả thi.

### 3.3. Khi không theo đúng lịch

Hết phiên mà chưa có kết quả: hỏi **“Phiên này đã kết thúc, bạn đã làm được đến đâu?”**. Không kết luận người dùng bỏ lỡ chỉ vì không bật timer/không có log. Có các nút Đã xong / Làm một phần / Chưa làm.

Sau khi đã biết kết quả, đề xuất theo dữ liệu thực:

| Lựa chọn | Điều kiện hiển thị |
| --- | --- |
| Chuyển sang tối mai | Có slot cụ thể, còn trước deadline, không vi phạm work window, ngày đã xác nhận giờ làm và lịch bảo vệ |
| Chia thành hai phiên ngắn | Task cho chia, đủ min/max phiên và nghỉ; các phiên hợp lệ trước hạn |
| Giữ trong danh sách ưu tiên | Task vẫn hoạt động, giữ deadline và cảnh báo; phiên lỡ được ghi nhận, chưa thêm phiên mới |

Mỗi đề xuất dời/chia hiện ngày giờ thật. Không có slot hợp lệ thì không hiển thị một lựa chọn hứa hẹn suông; giải thích ràng buộc và gợi ý khả thi khác. Chia hai phiên không có nghĩa chia đều nếu độ dài tối thiểu/slot không cho phép.

Nếu phần còn lại chưa biết, NORA đề xuất một thời lượng có nhãn ước lượng để xác nhận hoặc cho sửa nhanh. Không tự coi toàn bộ thời lượng phiên đã trôi là actual. Nếu đã có phiên tương lai cho cùng task, tính chúng trước để không xếp trùng phần việc.

“Giữ trong danh sách” không tự hạ xuống Q4, không xóa deadline và không ngầm hủy các phiên tương lai chưa được người dùng xem tác động. Không tạo thêm trách nhiệm từ task nhóm chưa nhận, không bỏ qua leader duyệt.

### 3.4. Cuối ngày: báo cáo ngắn và chuẩn bị ngày mai

Giai đoạn sau bổ sung thẻ tổng kết: việc đã xác nhận xong, thời gian thực có ghi nhận, việc chưa xong/chưa báo kết quả và một phương án tiếp nối. Phân biệt hoàn thành phiên với hoàn thành task, và member gửi duyệt với leader đã duyệt. Không tạo chỉ số năng suất giả từ đồng hồ theo lịch.

## 4. NORA dựa vào những thông tin nào?

Tạo một snapshot trên server theo tài khoản và múi giờ, gồm:

1. Thời điểm hiện tại và khoảng còn lại trong ngày; không đề xuất slot đã qua.
2. Lịch cố định, lịch đồng bộ đã biết, giờ sinh hoạt/di chuyển/nghỉ đã xác nhận.
3. Phiên Scheduled/Running/Paused, phiên khóa và các phiên chờ phản hồi.
4. Task đang hoạt động, deadline thật, mức quan trọng/khẩn cấp và nguồn phân loại.
5. Phần việc còn lại đã xác nhận, ước lượng, khả năng chia phiên, min/max và work window.
6. Quan hệ tiền nhiệm, việc con, hạn của các việc có thể bị ảnh hưởng.
7. Quyền/trạng thái nhóm, task đang chờ nhận hoặc chờ leader duyệt.
8. Lịch sử bỏ lỡ/hoãn và hệ số ước lượng/năng lực khi đủ dữ liệu và luồng cho phép.

Inbox thiếu effort/deadline chỉ tạo gợi ý tổ chức hoặc mời chọn một việc để làm, không tự tính thành tổng phút bắt buộc hoặc đặt deadline giả. Không đồng nhất danh sách Today với task đến hạn hôm nay: việc hạn xa có phiên hôm nay vẫn thuộc kế hoạch ngày.

Xếp ưu tiên theo quyết định có thể giải thích: cam kết bảo vệ → tiền nhiệm và việc khẩn/thiếu quỹ trước hạn → việc quan trọng cần tiến triển → việc nhanh cần xử lý → backlog theo yêu cầu. Dùng kết quả khả thi của planner; không để model tự tính số phút hay tự đổi lịch. Cảnh báo “thiếu 60 phút” phải chỉ rõ thiếu trong lịch đã xếp hay thiếu ngay cả khi dùng các khoảng khả thi trước deadline; không đánh đồng hai loại.

Phạm vi rủi ro phải nhìn tới deadline liên quan (trong horizon hỗ trợ), không chỉ quỹ giờ hôm nay. Tác động dời việc tiếng Nhật phải kiểm tra cả deadline và tiền nhiệm của tiếng Nhật. Nếu phạm vi ngoài horizon hoặc chưa đủ dữ liệu, thể hiện giới hạn thay vì kết luận chắc chắn.

## 5. Quy tắc tự động và quyền áp dụng

| Hoạt động | Mặc định đề xuất |
| --- | --- |
| Tổng hợp báo cáo khi mở Hôm nay | Tự động, không đổi task/lịch |
| Xếp thứ tự gợi ý | Tự động; không ghi đè lựa chọn thủ công |
| Chuẩn bị phương án | Một nút bấm, không ghi WorkSession |
| Áp dụng phương án | Một lần xác nhận với thay đổi cụ thể; kiểm tra lại tại thời điểm áp dụng |
| Tự xếp các khoảng trống | Tùy chọn đăng ký riêng ở giai đoạn sau, phạm vi rõ ràng, có lịch sử và hoàn tác |
| Dời phiên của việc khác / cứu deadline | Vẫn xác nhận tác động như cơ chế hiện tại |
| Đổi deadline, lịch cố định, sinh hoạt hoặc hợp đồng nhóm | Không tự đổi từ báo cáo |

Tôn trọng lựa chọn tự lưu/tự phục hồi hiện có; việc hợp nhất luồng không được lặng lẽ mở rộng quyền đó sang mọi task hoặc mọi ngày. Câu hỏi tư vấn không phải lệnh áp dụng lịch. Hoàn tác chỉ thực hiện nếu dữ liệu/lịch chưa thay đổi theo quy tắc an toàn hiện có.

## 6. Kế hoạch kỹ thuật theo đợt

### Đợt 1 — Một dịch vụ báo cáo ngày dùng chung

**Backend:** thêm hợp đồng `DailyBrief` và dịch vụ tổng hợp, tái dùng State, Eisenhower, capacity/planner, lịch và time-review. Kết quả có `localDate`, `asOf`, phạm vi, revision/fingerprint nguồn, mức đủ dữ liệu, số liệu, các việc khuyến nghị với reason code và hành động được phép.

**Frontend:** thêm `DailyBriefCard` ở đầu Hôm nay. Tránh lặp ba lần nội dung giữa card này, EisenhowerFocus và WorkTimeReview; những thẻ sau chỉ mở chi tiết hoặc phần liên quan. Không làm trang dịch chuyển sau mỗi lần polling.

**NORA:** thêm công cụ đọc báo cáo ngày; mở đúng tool theo mục đích, không gửi thêm tất cả schema vào mọi câu chào. Tư vấn dùng lại snapshot. Model chỉ diễn đạt nếu cần; số liệu/chọn slot dựa trên server. Giữ template có ích khi provider chậm/lỗi, không để báo cáo hằng ngày chặn vì chờ AI.

**API dự kiến:** `GET /api/daily-brief?date=YYYY-MM-DD`. GET chỉ đọc/tính, không tạo đề xuất/lịch/thông báo. Cache trong bộ nhớ theo nguồn và hạn thời gian; không tái dùng kết quả giữa tài khoản. Brief thay đổi khi giờ/nguồn khiến slot hết hợp lệ dù revision chưa đổi.

**Điều kiện xong:** mở trang không nhập prompt vẫn thấy tình hình và một hành động tiếp; chat hỏi hôm nay dùng cùng dữ liệu; không phát sinh request ghi lịch.

### Đợt 2 — Lịch hôm nay có thể áp dụng ngay

**Backend:** `POST /api/daily-plans/preview` nhận yêu cầu, task IDs hợp lệ, phạm vi ngày và snapshot version; tạo PlanProposal bằng planner hiện có. Tái dùng endpoint apply nếu hợp đồng tương thích. Không viết bộ lập lịch thứ hai.

Đề xuất mặc định giữ các phiên hợp lệ và bảo vệ phần đã làm; chỉ lấp phần còn thiếu. Muốn đổi phiên đã xếp thì thể hiện lý do và tác động. Không xếp thêm toàn bộ effort của một task đã có lịch. Rủi ro xét horizon, phần lịch mới cho hôm nay chỉ nằm trong ngày; các phần việc ngày khác giữ nguyên trừ thay đổi được xem và xác nhận.

**Frontend:** popup/drawer một màn hình cho 3 phần Giữ nguyên / Thêm mới / Cần đổi; nút Áp dụng. Thời điểm trôi hoặc revision đổi thì làm mới phương án, không ghi stale. Retry không nhân phiên.

**Điều kiện xong:** người dùng từ báo cáo tới lịch đã lưu trong một luồng ngắn; xem preview chưa đổi lịch; áp dụng không vượt deadline, không đụng lịch bảo vệ và có thông tin hoàn tác.

### Đợt 3 — Hợp nhất xử lý lệch lịch

Tái dùng WorkTimeReview, catch-up và RecoveryService trong một bộ điều phối; giữ ý nghĩa khác nhau giữa “chưa ghi nhận”, “đã xác nhận chưa làm” và “làm dở”. Không chạy đồng thời hai luồng bù cho cùng phần việc.

**API dự kiến:** preview các lựa chọn cho session/task; trả option IDs, slot thật, tác động, trạng thái khả thi và lý do chặn. Apply một lựa chọn là thao tác transactional, có khóa chống trùng, task version, calendar revision, membership và hạn đề xuất.

Tạo bảng trạng thái chuẩn dùng chung thay vì bổ sung một trạng thái mơ hồ vào enum của task: outcome phiên → remaining effort đã biết/chưa rõ → đề xuất phục hồi → applied/dismissed/superseded. Phiên đã có kết quả không bị hỏi lại qua thẻ khác hoặc Telegram khác.

**Điều kiện xong:** chọn chuyển tối mai/chia phiên/giữ danh sách không cần gõ câu mới; phương án không hợp lệ không được áp dụng; vẫn giữ quy trình báo cáo và leader duyệt.

### Đợt 4 — Báo cáo chủ động trong app và Telegram

Một thiết lập gọn: nhận báo cáo đầu ngày, giờ nhận theo múi giờ cá nhân và kênh. Báo cáo hiển thị trong app khi mở trang; gửi chủ động là lựa chọn riêng. Đầu ngày cần chính sách rõ khi máy chủ khởi động muộn: không gửi lời chào buổi sáng cũ vào tối; khi mở app tạo bản hiện tại.

Worker tạo bản gửi từ snapshot và dùng outbox thông báo hiện có. Khóa duy nhất theo user/ngày địa phương/loại báo cáo, có batch giới hạn/retry. Xem lại hoặc làm mới báo cáo không gửi Telegram lần nữa. Biến động rủi ro chỉ gửi khi thực sự cần hành động, phối hợp với cảnh báo Eisenhower/phiên để tránh spam.

Đường dẫn mở đúng Hôm nay hoặc phương án, không nhét lịch riêng/ghi chú/file vào URL. Telegram vẫn chỉ là thông báo và liên kết trong bản đầu; nút thao tác trực tiếp trên bot là phạm vi sau, cần xác minh đầy đủ như app.

Nếu cần lưu report và delivery state, thêm migration cộng thêm bảng `DailyBriefDeliveries` hoặc tương đương; không lưu nguyên snapshot lịch vào nhiều bản JSON không có vòng đời. Chỉ lưu dữ liệu tối thiểu phục vụ lịch sử, revision và chống gửi lặp. Khi thay giờ nhận/kênh, xử lý rõ bản chờ gửi và không phát lại report cũ.

**Điều kiện xong:** một báo cáo/ngày/kênh theo tùy chọn, đúng múi giờ, refresh/retry không tạo thêm dữ liệu gửi; không phải chạy model cho từng lần quét worker.

### Đợt 5 — Giảm nhập liệu và đo sử dụng thực tế

Hoàn thiện task Inbox và NORA capture theo kế hoạch thống nhất task: không deadline vẫn lưu/hoàn thành được, Notes độc lập. Dữ liệu đã biết phải được dùng lại; thiếu effort chỉ hỏi gộp khi bắt đầu xếp lịch. Cho sửa bằng câu đơn giản như “hôm nay tôi chỉ còn rảnh 1 giờ” với phạm vi ngày, tránh mở form cấu hình cả tuần.

Kiểm tra với ít nhất 5 người mới qua 3 kịch bản: chuẩn bị ngày đầu, bỏ lỡ phiên, hoàn thành sớm. Ghi số prompt, số màn hình chuyển, số lần nhập lại dữ liệu, thời gian tới hành động đầu tiên và lý do bỏ cuộc. So sánh luồng cũ/mới cùng kịch bản; không dùng số test pass hoặc tốc độ model để thay cho kết quả usability.

Mục tiêu thiết kế để đo, chưa phải cam kết: mở trang nhận được việc nên làm mà không gõ; áp dụng lịch cần một preview và một xác nhận; lệch lịch phổ biến xử lý bằng nút chọn; không yêu cầu khai báo mục tiêu dài hạn chỉ để tạo một việc.

### Đợt 6 — Tự xếp lịch có phạm vi rõ (sau kiểm chứng)

Tùy chọn người dùng chủ động bật: chỉ xếp task đủ dữ liệu vào khoảng trống đã xác nhận, không tự dời phiên khác, không tự sửa deadline/effort/sinh hoạt. Có lịch sử thay đổi và hoàn tác có kiểm tra. Khi thiếu điều kiện thì để bản đề xuất, không đoán.

Chỉ mở rộng tự động hóa sau khi các đợt 1–5 được đo và người dùng hiểu hệ thống đang có quyền gì. Giữ những quyền tự lưu hiện có theo đúng phạm vi cũ.

## 7. Ma trận kiểm chứng bắt buộc

| Tình huống | Kỳ vọng |
| --- | --- |
| Có lịch cố định, Q1/Q2 và việc ngắn Q3 | Số liệu đúng, gợi ý có lý do, không phá thời khóa biểu |
| Task Q4 do người dùng chọn nhưng đã quá hạn | Giữ lựa chọn và cảnh báo thực tế, không nói “không cần quan tâm” |
| Không có lịch hôm nay nhưng đủ giờ làm đã xác nhận | Vẫn tư vấn được trong phạm vi dữ liệu, không đòi tạo một sự kiện giả |
| Chưa có giờ làm/sinh hoạt của ngày đó | Chỉ dẫn đúng phần thiếu, không tuyên bố thiếu 60 phút như đã biết capacity |
| Việc đã có đủ phiên | Không xếp lại cùng effort; có thể chỉ đề nghị bắt đầu |
| Thiếu đúng 60 phút trước hạn | Báo con số từ planner và phạm vi đánh giá, phân biệt chưa xếp với không thể xếp |
| Phiên qua giờ nhưng không dùng timer | Hỏi kết quả, không tự kết luận đã bỏ lỡ/đã làm |
| Đề nghị dời tối mai sau deadline | Chặn lựa chọn, giữ hạn, đưa giải thích và lựa chọn khác |
| Task không cho chia | Không hiển thị chia hai phiên như phương án hợp lệ |
| Task có phiên tương lai / đã làm một phần | Trừ phần đã xác nhận, không tạo trùng phần còn lại |
| Task nhóm chưa nhận/chờ leader duyệt | Không tự nhận, không lên lịch thực hiện lại, không tự duyệt |
| Lịch đổi / proposal đã qua giờ / double click | Từ chối stale hoặc xử lý idempotent; không ghi một phần |
| Hỏi tư vấn hôm nay | Không tạo/sửa task hoặc WorkSession |
| Provider AI timeout | Báo cáo facts và thao tác hợp lệ vẫn hoạt động |
| Múi giờ đổi/qua nửa đêm/ngày dài ngắn | Biên ngày tính theo local calendar, không cộng cố định 24 giờ |
| Worker khởi động lại / refresh / hai thiết bị | Không gửi lặp report; không nhân phiên khi áp dụng |
| Hai tài khoản / vai trò leader/member | Không rò báo cáo, lịch riêng, file hoặc quyền |

Backend unit kiểm tra số liệu/chọn việc/phương án; API kiểm tra quyền và transaction; frontend/browser kiểm tra hành trình và trạng thái; opt-in live kiểm tra lời diễn đạt bằng 3.8 Flash trên dữ liệu giả. Test báo cáo lịch không cần provider thật. Benchmark riêng p50/p95, số lượt model, thời gian planner và lỗi theo quy mô task; không quảng cáo tốc độ cố định trước khi đo.

## 8. Phạm vi bản đầu khuyến nghị

Triển khai đợt 1–3 trước: **thẻ kế hoạch ngày + chat dùng chung dữ liệu + một lần áp dụng + các lựa chọn khi lệch lịch**. Đây là phần trực tiếp giải quyết “cứ phải prompt”. Sau đó thêm gửi báo cáo Telegram và tổng kết cuối ngày, rồi kiểm chứng Inbox/capture và tự động hóa có đăng ký.

## 9. Các file hiện tại cần dùng hoặc thay đổi

- `frontend/src/dashboard.jsx`, `dashboard-model.js`: điểm vào và dữ liệu dashboard.
- `frontend/src/eisenhower.jsx`, `eisenhower-board.jsx`: lý do ưu tiên, nguồn phân loại và thao tác task.
- `frontend/src/work-time-review.jsx`, `catch-up.jsx`: hợp nhất trải nghiệm phản hồi.
- `src/SPWPOS.Infrastructure/Workspace/AgentScheduleReview.cs`: báo cáo hiện có.
- `src/SPWPOS.Infrastructure/Workspace/WorkTimeService.cs`, `RecoveryService.cs`: phản hồi và phục hồi.
- `src/SPWPOS.Infrastructure/Workspace/NoraToolRegistry.cs`, `NoraRequestPolicy.cs`, `AgentRuntimeTools.cs`: tool đọc báo cáo/chuẩn bị lịch, giữ ranh giới tư vấn/ghi dữ liệu.
- `src/SPWPOS.Infrastructure/Workspace/NotificationAdminService.cs`, `EisenhowerNotifications.cs` và Telegram outbox: phát và phối hợp thông báo.
- `src/SPWPOS.Application/Workspace/PlanningEngine.cs`, `EisenhowerEngine.cs`: tái dùng logic thay vì để AI tính lịch.
- Dịch vụ/hợp đồng/endpoint/test báo cáo ngày là file mới; migration chỉ khi chốt dữ liệu giao nhận/lịch sử, cộng thêm và có backup.

Kế hoạch này không coi feedback là bằng chứng tính năng đã triển khai, không thay model, không tạo báo cáo hay thông báo thật và không tự sửa lịch người dùng.
