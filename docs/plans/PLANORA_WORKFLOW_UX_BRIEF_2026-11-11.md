# PLANORA — Báo cáo cải tiến luồng làm việc, NORA và Workload

**11/11/2026 · Bản viết lại cho nhóm SWP391 · Đối chiếu source local tại `dbf6d5c`**

Đọc mục 1 để chốt hướng, mục 2–5 để triển khai và nghiệm thu. [Phụ lục đối chiếu bốn báo cáo](PLANORA_WORKFLOW_UX_COVERAGE_2026-11-11.md) giữ các phát hiện chi tiết, cả những mục nên hoãn. Báo cáo phân biệt **đã thấy trong source**, **quan sát từ báo cáo cũ** và **đề xuất cần triển khai hoặc xác minh**. Chưa chạy lại ứng dụng hoặc bộ test trong lần tổng hợp này.

## 1. Quyết định đề xuất cho nhóm

**PLANORA nên giúp người dùng biết việc có kịp hạn không, bắt đầu từ đâu và sửa kế hoạch thế nào khi thực tế thay đổi.** Luồng chính là **Ghi việc → Lập kế hoạch → Thực hiện → Điều chỉnh → Hoàn thành → Rút kinh nghiệm**. Eisenhower, checklist, lịch và NORA giúp người dùng ra quyết định ở từng bước; người dùng không phải đi qua tất cả màn hình để hoàn thành một việc đơn giản.

Ba ưu tiên mà thành viên nhóm nêu đều nên nằm trong đợt cải tiến sớm. Thứ tự dưới đây là thứ tự phụ thuộc khi triển khai, không phải loại Workload hay NORA khỏi phạm vi:

| Ưu tiên của nhóm | Phạm vi đợt đầu | Điều kiện đạt |
| --- | --- | --- |
| **1. Sửa UX** | Đưa người mới tới việc đầu tiên; cho ghi việc trước hoặc thiết lập giờ làm trước. Gỡ các điểm chặn khi AI tắt/lỗi. Làm rõ nơi ghi việc, nơi xem lịch và nơi ghi kết quả. | Tài khoản mới tự lưu việc, cấu hình thời gian và chốt lịch khả thi; có đường thủ công khi NORA không dùng được. |
| **2. NORA chủ động hơn** | Trên **Hôm nay**, cho biết việc tiếp theo và rủi ro quan trọng. Khi lịch hoặc tiến độ thay đổi, hiện lý do và nút xem phương án ngay tại chỗ. | Người dùng không phải nghĩ prompt mới để biết bước tiếp. Tư vấn chỉ đọc; đề xuất chưa ghi lịch; xác nhận trước khi áp dụng thay đổi. |
| **3. Thiết kế lại Workload trong Tiến độ** | Bảng theo ngày, hai hàng: **quỹ giờ + trạng thái/lời khuyên** và **việc đã xếp + ghi kết quả**. Dùng chung số liệu với Hôm nay và NORA. | Người dùng mở một ngày, thấy tải và lý do, ghi phút thực làm, rồi thấy kế hoạch còn lại hoặc cảnh báo cập nhật đúng. |

**Bảo vệ phạm vi:** ưu tiên luồng cá nhân chạy xuyên suốt; giữ một luồng nhóm giao–nhận–nộp–duyệt. Giữ bộ lập lịch và các kiểm tra hiện có trừ khi tìm được lỗi cụ thể. Để sau việc thay toàn bộ giao diện, thêm dashboard thứ hai, mở rộng pet/social/Pomodoro và tự động điều phối phụ thuộc toàn nhóm.

## 2. Luồng 12 bước của thành viên nhóm, đối chiếu với hiện trạng

| # | Bước đề xuất | Đã có cơ sở / việc cần chốt |
| --- | --- | --- |
| 1 | **Thiết lập** giờ rảnh, lịch cố định, múi giờ; Google Calendar và kênh nhắc tùy chọn. Cho bỏ qua để ghi việc trước. | Có dữ liệu lịch và availability; hướng dẫn đầu phiên đang tắt và các màn hình thao tác chính thường đi qua NORA. Phải cho **Bỏ qua / Tiếp tục thiết lập**, phân biệt chưa cấu hình với 0 phút rảnh. Tích hợp không chặn luồng cơ bản. |
| 2 | **Tạo việc** thủ công, qua NORA hoặc từ tài liệu. | Có Inbox/Notes lưu nháp không cần AI; task thật và đề xuất lịch là các trạng thái khác. Khôi phục thao tác thủ công; nhập tài liệu chia gói đã có trong luồng nhóm, chưa coi là hỗ trợ tải mọi tài liệu trong chat cá nhân. |
| 3 | **AI phân tích**: gợi ý mức quan trọng, thời lượng và dữ kiện còn thiếu. | Có gợi ý importance và hỏi làm rõ. Hiện rõ dữ liệu do người dùng nhập, ước lượng của NORA và phần đã xác nhận. Hỏi một câu khi dữ kiện thiếu thực sự chặn hành động; không bịa hạn hoặc giờ làm. |
| 4 | **Eisenhower**: xem Q1–Q4, điều chỉnh khi cần. | Có ma trận và đánh giá urgency. Kiểm tra lý do hiển thị khi người dùng đặt khẩn thủ công; “khẩn do bạn chọn” khác “đến hạn trong 24 giờ”. Việc chưa đủ dữ liệu giữ trạng thái chưa phân loại. |
| 5 | **Chia nhỏ** bằng checklist/subtask hoặc nhờ NORA đề xuất. | Có checklist, quan hệ cha–con và luồng gợi ý chia việc; xác minh cách lưu theo từng điểm vào. Checklist là các đầu ra nhỏ, **chia phiên** là chia thời gian. Cho sửa đề xuất; không tự tách việc bắt buộc làm liên tục. |
| 6 | **Chọn chỉ lưu hoặc xếp lịch.** | Inbox cho lưu bản nháp; source chưa có luồng thống nhất lưu task thật chỉ với tên qua NORA. Đợt đầu cần hai lựa chọn rõ: **Lưu nháp** và **Xem lịch đề xuất**; nếu việc có đủ trường, cho lưu task mà chưa xếp lịch. Không lấy hạn giả để đáp ứng schema. |
| 7 | **Đề xuất kế hoạch** theo deadline, effort, ưu tiên, phụ thuộc và giờ làm. | Dùng PlanningEngine tính phương án; NORA diễn giải lý do và giới hạn. Giờ trống nhìn thấy có thể không đủ cho một phiên hợp lệ. Nêu thiếu bao nhiêu trước hạn khi tính được, hoặc nói rõ dữ liệu nào còn thiếu. |
| 8 | **Xem trước, chỉnh và áp dụng.** | Có Preview/Apply và undo theo điều kiện. Làm nổi bật giờ cũ/mới, mục giữ nguyên, tác động tới hạn. Chỉnh điều kiện rồi tính lại; preview cũ phải bị từ chối khi lịch đổi. WorkSession chỉ hiện là đã lưu sau khi xác nhận thành công. |
| 9 | **Thực hiện**: nhắc lịch, bắt đầu phiên, ghi thực tế và tiến độ. | Có phiên thực thi và nhắc lịch. Đồng hồ theo lịch, Stopwatch/Pomodoro và phút thực làm là ba thứ khác nhau. Đưa thao tác cập nhật kết quả vào chi tiết phiên và Workload, dùng chung dữ liệu. |
| 10 | **Tái hoạch định** khi chưa xong, bỏ lỡ hoặc phát sinh lịch bận. | Có catch-up, recovery và xem trước thêm giờ. Nối chúng bằng một lối vào rõ ràng; sử dụng phần còn lại đã xác nhận, kiểm tra các phiên tương lai để tránh xếp hai lần. Không nới hạn hoặc dời cam kết được bảo vệ để làm cho phương án có vẻ khả thi. |
| 11 | **Báo cáo Workload**: còn bao nhiêu, tải cao ở đâu, dự kiến so với thực tế, tỷ lệ hoàn thành và quá hạn. | Tiến độ đã có phân tích và backend có dữ liệu capacity theo ngày. Bảng hai hàng, thao tác ghi kết quả và trạng thái đồng bộ là thiết kế đề xuất. Ghi rõ kỳ báo cáo, đơn vị và mẫu số; khi chưa có mẫu actual thì ghi “Chưa có dữ liệu”. |
| 12 | **Cải thiện kế hoạch** từ kết quả thực. | Có hiệu chỉnh estimate/capacity dựa trên lịch sử cho luồng hỗ trợ; không mặc định mọi lượt chat đều bật hiệu chỉnh. Giữ estimate gốc để so sánh; gợi ý estimate lần sau khi có đủ mẫu, cho người dùng sửa/xác nhận, không tự sửa hạn hoặc các phút vừa xác nhận. |

Luồng ngắn cho việc đã rõ: **Thêm việc → Xem lịch → Xác nhận → Làm → Ghi kết quả**. Khi nhập liệu chưa đủ, NORA có thể giúp làm rõ; khi người dùng chỉ muốn ghi nhớ, cho lưu trước. Checklist và Eisenhower nên mở khi cần, không thành bước bắt buộc.

## 3. Chi tiết ba thay đổi ưu tiên

### UX: một đường rõ tới lịch đầu tiên

- **Mới / chưa có giờ làm:** Hôm nay hiện một khối với hai lựa chọn rõ: **Thiết lập giờ làm** hoặc **Ghi việc trước**. Khi chỉ có lịch bận, nói “Chưa xác nhận giờ làm”. Có thể bỏ qua hướng dẫn và quay lại bất cứ lúc nào.
- **Đã có dữ liệu:** Hôm nay ưu tiên **Việc tiếp theo** hoặc **Kế hoạch cần xử lý**; trạng thái rỗng giải thích bước kế tiếp thay vì đầy số 0. Tiến độ là nơi xem và phân tích workload; Lịch là nơi xem/cập nhật các mốc giờ. Một thao tác sửa không cần xuất hiện ở nhiều nơi bằng nhãn khác nhau.
- **Khi AI không dùng được:** đưa đường thêm/sửa task, giờ rảnh và lập lịch thủ công ngay tại thông báo lỗi. Kiểm tra cả AI tắt, timeout và cấu hình không khả dụng. Giữ task/bản nháp nếu mới chỉ thất bại ở bước đề xuất.
- **Sau khi luồng chạy:** gom những cửa vào trùng, giữ liên kết sâu và hành vi Back. Tăng khả năng đọc của chữ/nút trong các màn hình chính. Dọn CSS ở phần vừa sửa; tách việc đổi toàn bộ thanh bên và thiết kế lại đăng ký khỏi đường găng trừ khi chúng thực sự chặn người dùng.

### NORA: chủ động từ dữ liệu, rõ quyền quyết định

| Tín hiệu | Hiện gì và dẫn đi đâu | Ranh giới |
| --- | --- | --- |
| Mở **Hôm nay** | Một việc có thể bắt đầu, một lý do và một cảnh báo quan trọng nếu có. Dẫn tới phiên hoặc nút **Xem phương án**. | Có thể tạo tóm tắt từ dữ liệu server khi AI chậm/tắt. Không ghi lịch chỉ vì mở trang hoặc hỏi “Hôm nay nên làm gì?”. |
| Có sự kiện mới hoặc deadline sắp tới | Nêu việc bị ảnh hưởng, khoảng giờ trước hạn, thiếu hụt hoặc ràng buộc đang chặn. | Đánh giá theo hạn liên quan trong phạm vi tính được, không chỉ nhìn số giờ hôm nay. Chưa đủ dữ liệu thì nói rõ. |
| Phiên hết giờ hoặc có kết quả mới | Hỏi **Đã xong / Làm một phần / Chưa làm**; nếu cần, cho nhập phút thực làm và phần còn lại. Đưa phương án thêm/chia/dời thời gian khi có slot thật. | Không suy luận thất bại từ việc timer chạy 20 phút hoặc thiếu log. Không tự lấy thời gian đã trôi làm actual; nếu không có phương án thì giải thích, không báo đã xử lý. |
| Người dùng muốn đổi lịch | Cho xem giờ cũ/mới, mục được giữ, lý do và tác động; kiểm tra lại lúc xác nhận. | Tách tư vấn, preview và lưu. Hành động chủ động đợt đầu chỉ đọc hoặc mở preview; không mở rộng quyền tự lưu của chat hiện tại. |

Dùng cùng snapshot theo tài khoản và múi giờ cho Hôm nay, Tiến độ và câu trả lời NORA. Lịch, kết quả thực thi và trạng thái task là nguồn dữ liệu; model không tự cộng giờ hoặc đặt lịch từ lời giải thích. Dùng nhắc việc/catch-up hiện có, tránh lặp popup cho cùng một phiên và cùng trạng thái rủi ro. Báo cáo gửi định kỳ hoặc đề xuất tự động qua Telegram/email có thể làm sau khi luồng trong ứng dụng ổn định. [Kế hoạch NORA chủ động đã có trong repo](NORA_PROACTIVE_DAILY_ASSISTANT_2026-11-04.md) là nguồn để tái sử dụng, không phải bằng chứng tính năng đã triển khai.

### Workload trong Tiến độ: hai hàng theo ngày

| Hàng | Hiện mặc định | Khi mở chi tiết |
| --- | --- | --- |
| **Trên — Quỹ giờ và lời khuyên** | Mỗi cột là một ngày. Thanh quỹ giờ + số giờ dùng được, đã đặt, còn xếp thêm; nhãn **Còn chỗ / Gần đầy / Thiếu giờ / Chưa thiết lập** kèm lý do ngắn. | Xem lịch bận, giờ làm được xác nhận, giới hạn, các phiên đã giữ chỗ và ràng buộc làm giảm thời gian khả dụng. Nếu có việc có nguy cơ, dẫn tới chi tiết task hoặc preview. |
| **Dưới — Việc và kết quả** | Hiện nhóm task hoặc phiên đã xếp trong ngày, cùng tổng phút kế hoạch. Mở một ô thấy danh sách chi tiết với checkbox/kết quả và phút. | Ghi **thực làm bao nhiêu** cho phiên; chọn **xong phiên / làm một phần / chưa làm** hoặc xác nhận **xong toàn bộ việc** nếu đúng. Hiện phần còn lại và cập nhật hàng trên sau khi lưu thành công. Task nhóm vẫn qua bước gửi duyệt. |

Ví dụ minh họa, không phải số đo: **“Hôm nay còn xếp thêm 60 phút. Báo cáo còn 90 phút chưa được phiên tương lai nào bao phủ và phải xong hôm nay. Thiếu 30 phút.”** Chỉ kết luận thiếu 30 phút nếu planner xác nhận có thể dùng đủ 60 phút cho chính báo cáo, trước deadline, với các quy tắc chia phiên và nghỉ. Nếu báo cáo cần một phiên liên tục, 60 phút nằm ở hai khoảng rời không đủ để kết luận như vậy.

**Hợp đồng số liệu cần thống nhất trước khi dựng bảng:**

- Quỹ giờ theo ngày lấy từ backend. Phân biệt giờ làm đã xác nhận, giờ đã trôi, lịch bận, phút đã đặt, phút thực làm và **phút còn xếp thêm**. Nếu trường backend đã trừ reservation hoặc giới hạn ngày thì không trừ lần hai. Dùng hợp khoảng bận khi có giao nhau; không cộng thời lượng của cùng một task nhiều lần ở các ngày có nhiều phiên.
- **Chưa xếp** là phần effort chưa có phiên hợp lệ bao phủ; **thiếu trước hạn** là phần planner chưa thể phân bổ với các ràng buộc hiện có. Một task có thể chưa xếp nhưng vẫn có đủ quỹ. Ngày chưa cài giờ làm, ngày nghỉ, ngày không có việc và ngày quá tải phải có nhãn riêng.
- **Dự kiến/Thực tế:** so sánh estimate gốc với actual đã ghi, cùng phạm vi và đơn vị. Không coi thiếu actual là 0 phút thực làm. Tỷ lệ hoàn thành tính trên task trong phạm vi đã chọn, không lấy tỷ lệ tick checklist hoặc tỷ lệ giờ đã trôi làm tỷ lệ xong task. **Quá hạn** chỉ áp dụng với hạn thật và việc chưa hoàn tất.
- **Checkbox:** xác định rõ đối tượng là checklist, WorkSession hay task. Ghi actual có thể nhập sau hoặc sửa theo quyền và trạng thái; không tạo phút giả khi tick. Nếu cần ghi kết quả một phần, cho nhập phút đã làm và phần còn cần rõ ràng. Lưu lặp không cộng phút, tạo phiên bù hay tăng XP hai lần.
- **Tương tác:** hover chỉ là xem nhanh như ảnh mẫu. Bấm/chạm hoặc Enter mở cùng chi tiết; có focus rõ, nhãn chữ thay cho chỉ dùng màu, đóng và quay lại đúng ô. Trên điện thoại dùng danh sách theo ngày hoặc một ngày đang chọn, không bắt người dùng cuộn cả trang theo chiều ngang.

Ba ảnh TimeHero mới minh họa **bảng tuần**, **chi tiết quỹ giờ** và **danh sách việc**. Đề xuất ghi actual từ hàng dưới đến từ thành viên nhóm; ảnh danh sách có checkbox và phút nhưng không tự chứng minh đó là ô nhập thời gian thực tế. Lấy cấu trúc và cách mở chi tiết, dùng quy tắc dữ liệu của PLANORA. Đợt đầu ưu tiên báo cáo cá nhân; bộ lọc “Everyone” của ảnh không phải lý do để mở lịch cá nhân cho trưởng nhóm.

## 4. Luồng nhóm và các quyết định cần chốt

**Luồng trình bày:** trưởng nhóm tạo/giao việc → thành viên xem khả thi trên lịch riêng → xác nhận nhận → cập nhật → nộp → trưởng nhóm duyệt. Nếu An cần thêm ba giờ và Bình cần đầu ra của An, trưởng nhóm có thể chọn đổi mốc bàn giao hoặc cho Bình làm phần độc lập. Kiểm tra lại tính khả thi trên lịch của người bị ảnh hưởng. Chỉ gọi là cảnh báo phụ thuộc tự động nếu hệ thống thực sự lưu A → B, tính tác động và có bằng chứng kiểm thử.

| Vấn đề | Hiện trạng đã đối chiếu | Quyết định cho đợt đầu |
| --- | --- | --- |
| Có giờ làm nhưng không có sự kiện | [Claim preview](../../src/SPWPOS.Infrastructure/Workspace/PrivateTeamClaims.cs#L19) vẫn yêu cầu có sự kiện. | Chấp nhận giờ làm hợp lệ mà không cần sự kiện giả; nếu thiếu giờ làm thì hướng dẫn bổ sung. |
| Hạn xa | Draft hỗ trợ tới một năm, preview nhận việc giới hạn 30 ngày. | Trước defense, chọn một chính sách rõ: giới hạn đầu vào nhất quán **hoặc** nếu nghiệp vụ cần hạn xa, thiết kế “đã nhận trách nhiệm, chờ xếp lịch” với cảnh báo. Không hứa đã có trạng thái đó. |
| AI lỗi / chia gói theo số người | Luồng draft nhóm yêu cầu AI; [chia gói từ tài liệu](../../src/SPWPOS.Infrastructure/Workspace/TeamBriefService.cs#L25) giới hạn số gói theo số thành viên. | Có đường tạo/giao việc cơ bản không cần AI nếu đưa nhóm vào demo. Theo dõi ca ba người/tám gói; gỡ giới hạn khi cần chia theo đầu ra, sau khi xác minh các bước biên tập và gán nhiều việc. |
| Quyền riêng tư và trạng thái | Có claim riêng và quy trình chờ duyệt. | Phân biệt **chỉ định**, **đã nhận**, **đã nộp**, **đã duyệt**. Tín hiệu khả thi cho leader, nếu làm, chỉ là trạng thái hoặc tổng hợp được phép chia sẻ; không lộ lịch, sự kiện hay thói quen cá nhân. |

## 5. Triển khai và bằng chứng cho buổi bảo vệ

| Đợt | Việc giao được | Điều kiện chốt |
| --- | --- | --- |
| **1 — Nền tảng** | Sửa đường thủ công, bước đầu tiên và nhãn phân biệt nháp/task/phiên. Dùng cùng fixture để tái hiện lỗi trước và sau. | Người mới tự lập được lịch khi AI bật và khi tắt; không có ngõ cụt sau lỗi. |
| **2 — Cắt xuyên luồng** | Ghép thẻ chủ động và Workload hai hàng vào luồng thực hiện–phục hồi hiện có. Chốt cách tính trước khi vẽ thanh. | Số liệu giữa Hôm nay, Tiến độ và NORA khớp cùng dữ liệu và thời điểm. Ghi actual một lần; phần còn lại cập nhật; preview cũ không áp dụng. |
| **3 — Demo và hoàn thiện** | Chạy ca xung đột, quá tải và nhóm; sửa chỗ vướng trên laptop/điện thoại; bổ sung test hồi quy cho lỗi thật. | Người dùng giải thích được “đổi gì, vì sao, tiếp theo làm gì”. Mọi tuyên bố trong demo có bằng chứng hoặc ghi giới hạn. |

**Demo cá nhân:** dùng tài khoản thử, giờ làm được xác nhận, một lịch học cố định và một phiên khác đã khóa. Tạo báo cáo **5 giờ, hạn thứ Sáu 17:00**. Chốt lịch ban đầu, thêm cuộc họp cố tình trùng một phiên báo cáo, rồi ghi nhận một phiên đã làm và xác nhận **còn 90 phút**. Chạy hai biến thể độc lập: đủ slot hợp lệ để phục hồi; hoặc chỉ đủ 60 phút hợp lệ trước hạn, hiển thị thiếu 30 phút và chưa áp dụng thay đổi bất khả thi. Kiểm tra phiên được đổi, lịch được giữ, actual và trạng thái sau tải lại. Biến thể “báo cáo 4 giờ” trong các báo cáo là fixture khác, dùng cùng tiêu chí thay vì làm một luồng sản phẩm riêng.

**Demo nhóm:** trưởng nhóm chỉ định An; An xem lịch riêng và xác nhận nếu khả thi. Khi An báo cần thêm ba giờ, cho thấy rủi ro của An, quyết định của trưởng nhóm với phần việc của Bình, rồi nộp và duyệt. Chạy thêm ca thiếu giờ hoặc không có sự kiện; kiểm tra thay đổi lịch riêng chỉ thuộc tài khoản phù hợp. Dùng cùng mẫu thử để xác minh rõ ranh giới với tự động điều phối phụ thuộc nhóm.

**Đo cùng đề bài** với quy trình lịch + bảng việc + chat: thời gian đến kế hoạch khả thi, số thao tác tự dời lịch, số lần hỏi thêm, thời điểm phát hiện thiếu giờ, tỷ lệ người mới tự hoàn thành và mức hiểu lý do của đề xuất. Nếu có thể, thử với 3–5 người ngoài nhóm. Ghi build, cấu hình AI, dữ liệu trước/sau và số người tham gia. Chưa có phép đo thì trình bày kết quả dưới dạng tiêu chí hoặc mục tiêu, không nói “tiết kiệm X%”.

**Việc tiếp theo của nhóm:** chốt một fixture chung và tái hiện luồng trên đúng bản sẽ bảo vệ. Song song, thống nhất định nghĩa **quỹ còn xếp / phút chưa xếp / actual / hoàn thành** để người thiết kế Workload và người nối luồng NORA dùng cùng một quy ước. [Phụ lục nghiệm thu và truy vết](PLANORA_WORKFLOW_UX_COVERAGE_2026-11-11.md) dùng để phân công và ghi bằng chứng.

**Điểm tựa trong repo:** [Hợp đồng capacity](../../src/SPWPOS.Application/Workspace/Contracts.cs#L79) · [Trang Tiến độ](../../frontend/src/page-insights.jsx) · [Ghi nhận và thêm thời gian](../features/WORK_TIME_FOLLOWUP.md) · [NORA hiểu mục đích](../features/NORA_ASSISTANT_FIRST.md) · [Inbox và Smart Lists](../features/TASK_SMART_LISTS.md) · [Lịch riêng khi nhận việc nhóm](../features/PRIVATE_TEAM_PLANNING.md).
