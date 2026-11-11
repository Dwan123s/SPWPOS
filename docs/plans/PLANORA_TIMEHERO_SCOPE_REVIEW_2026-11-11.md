# PLANORA — Đối chiếu TimeHero và đề xuất chốt lại phạm vi

Ngày khảo sát 11/11/2026. Đề tài đích là **Hệ thống hoạch định và tối ưu khối lượng công việc thông minh**. Người dùng xác nhận **luồng nhóm là bắt buộc**.

## 1. Cơ sở và giới hạn khảo sát

Đã mở app.timehero.com, kết quả chuyển tới trang đăng nhập. Phân tích TimeHero bên dưới dựa trên website sản phẩm và Help Center chính thức, cùng ảnh người dùng đã cung cấp. Chưa thao tác trực tiếp tài khoản TimeHero đã đăng nhập. Một số bài hướng dẫn được xuất bản từ 2018–2019, vì vậy chúng chứng minh luồng được tài liệu mô tả, chưa chứng minh mọi chi tiết giao diện hiện hành. [Trang đăng nhập](https://app.timehero.com/login) · [Tổng quan sản phẩm](https://www.timehero.com/features)

Đã đối chiếu source local của PLANORA, bao gồm các thay đổi refactor đang chưa commit. “Có trong source” và “đã hoạt động trọn vẹn trên ứng dụng chạy thực” là hai mức xác minh riêng. Lượt khảo sát này chưa chạy lại bộ test hoặc xác nhận UX trên tài khoản thật. Kết quả tự động trong checklist trước đây cần gắn đúng bản build.

## 2. Luồng TimeHero cần học

Luồng sử dụng được tổng hợp từ tài liệu chính thức là **xác định giờ làm và lịch bận → đưa công việc vào → hệ thống phân bổ → xem kế hoạch → ghi thực tế → điều chỉnh phần còn lại → đánh giá tải và rủi ro**. Đây là cách tổng hợp của báo cáo, không phải tên một wizard của TimeHero.

| Bước | Hành vi được tài liệu mô tả | Ý nghĩa với PLANORA |
| --- | --- | --- |
| Nhập việc | Inbox nhận task nhanh, có thể dùng câu tự nhiên; task có thể tồn tại khi chưa có due date. | Một cửa nhập việc rõ ràng, xem và sửa thông tin đã hiểu. |
| Xem kế hoạch | Task được sắp vào thời gian phù hợp; thay đổi công việc hoặc sự kiện có thể làm lịch được tính lại. | Nối task với kết quả hoạch định và tác động của thay đổi. |

Nguồn cho hai bước trên là [TimeHero basics](https://help.timehero.com/en/articles/1270270-timehero-basics).

| Bước | Hành vi được tài liệu mô tả | Ý nghĩa với PLANORA |
| --- | --- | --- |
| Kiểm tra đầu vào | Giờ làm, effort, thời hạn và quan hệ công việc ảnh hưởng khả năng xếp lịch. Task dài có thể cần bật chia phiên. | Phân biệt thiếu dữ liệu, thiếu tổng giờ và thiếu khoảng liên tục. |
| Ghi thực tế | Có timer, ghi partial work, giữ estimate gốc và ghi thời gian cần thêm để hệ thống xếp tiếp. | Đưa ghi actual và remaining vào cùng luồng thực hiện. |
| Xem tải nhóm | Workload giúp nhận biết thành viên còn khả năng nhận việc và người quá tải. | Nhóm cần hỗ trợ quyết định phân công dựa trên khả năng thực hiện. |

Nguồn tương ứng là [Task chưa được xếp](https://help.timehero.com/en/articles/1372077-why-doesn-t-my-task-have-a-planned-date), [Timer và partial work](https://www.timehero.com/product/timer), [Workload nhóm](https://help.timehero.com/en/articles/2334181-visualize-your-team-s-workload).

Một khác biệt chính sách đáng chú ý là TimeHero có thể xếp task sau due date rồi đánh dấu Behind. PLANORA đang có strict proposal để từ chối áp dụng khi còn thiếu giờ hoặc vi phạm hạn trong phạm vi. Đề xuất giữ cách xác nhận rõ ràng của PLANORA và hiển thị phần thiếu cùng lựa chọn xử lý. Người dùng phải chủ động xác nhận nếu muốn thay đổi deadline. [Lịch sau due date](https://help.timehero.com/en/articles/1372082-why-did-timehero-plan-my-task-after-the-due-date)

TimeHero phân biệt Behind, tức kế hoạch dự kiến hoàn thành muộn, với Overdue, tức hạn đã qua mà việc chưa xong. PLANORA nên thể hiện riêng nguy cơ trước hạn và tình trạng quá hạn, để người dùng còn thời gian can thiệp. [Rủi ro và thay đổi kế hoạch](https://www.timehero.com/post/time-management-for-remote-and-working-from-home-teams)

Không có cơ sở từ các nguồn đã đọc để kết luận TimeHero dùng LLM trong bộ lập lịch, hoặc thuật toán bên trong đạt tối ưu toàn cục. Việc so sánh trong báo cáo tập trung vào hành vi sản phẩm công bố.

## 3. PLANORA đang lệch ở đâu

| Điểm cần đối chiếu | Bằng chứng trong source PLANORA | Hệ quả |
| --- | --- | --- |
| Nhiều cửa nhập và xử lý | Task form, Notes, NORA chat, chia nhỏ và nhập tài liệu nhóm. `secretaryMode` còn điều khiển ngoại lệ thời gian, phụ thuộc và một số đường chia nhỏ. | Người dùng khó đoán nơi thao tác và lúc nào cần AI. |
| Hôm nay chưa tập trung | Dashboard cũ vẫn mở cùng khối bước tiếp theo, Eisenhower, danh sách việc, lịch và phân tích. Shell còn thanh capacity riêng. | Nhiều thành phần cùng đề xuất việc cần làm. |
| Workload mới chủ yếu cá nhân | `WorkloadView` và `GetWorkloadAsync` trả dữ liệu tài khoản hiện tại. | Có năng lực đọc tải cá nhân nhưng chưa đủ để tuyên bố đã có bảng điều phối tải nhóm. |
| Nhóm có nghiệp vụ thật | Leader/member, designated recipient, claim riêng, task cá nhân liên kết, gửi và duyệt hoàn thành. | Phải giữ theo yêu cầu đề tài và nối với hoạch định cá nhân. |
| Nhánh ngoài hoạch định | Có API bạn bè/hồ sơ xã hội, pet/XP/skin, checkout payOS và quản trị giao dịch. | Tốn diện tích giao diện, ca kiểm thử và công sức giải thích ngoài giá trị chính. |
| Tính thông minh có nền tảng | Planner có ràng buộc, scoring ứng viên, repair giới hạn; có estimate/actual, remaining budget và learning. | Nên làm rõ tiêu chí và chứng minh kết quả của phần đã có. |

Các điểm tựa trong source là [shell](../../frontend/src/shell.jsx), [dashboard](../../frontend/src/dashboard.jsx), [task](../../frontend/src/tasks.jsx), [ngoại lệ giờ làm](../../frontend/src/calendar.jsx), [Workload](../../src/SPWPOS.Infrastructure/Workspace/WorkloadService.cs), [hợp đồng Workload](../../src/SPWPOS.Application/Workspace/WorkloadContracts.cs), [nhận việc nhóm](../../src/SPWPOS.Infrastructure/Workspace/PrivateTeamClaims.cs), [nộp và duyệt](../../src/SPWPOS.Infrastructure/Workspace/TeamCompletionService.cs), [social API](../../src/SPWPOS.Api/Controllers/Workspace/SocialController.cs), [payment API](../../src/SPWPOS.Api/Controllers/Commerce/PaymentsController.cs), [scoring](../../src/SPWPOS.Application/Workspace/PlanningScoring.cs).

## 4. Phạm vi sản phẩm đề xuất

**PLANORA giúp cá nhân và nhóm xác định khối lượng việc có thể hoàn thành trong quỹ thời gian thực tế, tạo kế hoạch khả thi và điều chỉnh kế hoạch khi có thay đổi.**

Đây là đề xuất chốt phạm vi sau khảo sát, cần dùng làm căn cứ duyệt backlog. Chưa thực hiện cắt hoặc ẩn tính năng trong lượt này.

| Quyết định | Thành phần | Phạm vi cụ thể |
| --- | --- | --- |
| Giữ làm lõi | Công việc | Tên, effort, thời hạn khi cần cam kết, ưu tiên, người chịu trách nhiệm, phụ thuộc, cho phép chia phiên. |
| Giữ làm lõi | Thời gian | Giờ làm, nghỉ, lịch cố định, ngoại lệ và phiên được bảo vệ. |
| Giữ làm lõi | Hoạch định | Xem phương án, kiểm tra khả thi, áp dụng, giải thích thiếu giờ và xử lý đề xuất cũ. |
| Giữ làm lõi | Thực hiện và phục hồi | Actual, total remaining, partial/missed, hoàn thành task và lập lại phần bị ảnh hưởng. |
| Giữ làm lõi | Workload | Cần làm bao nhiêu, đã có lịch bao nhiêu, còn chưa xếp bao nhiêu, có kịp hạn không. |
| Giữ làm lõi | Nhóm bắt buộc | Tạo nhóm, mời thành viên, phân công, claim theo lịch riêng, cập nhật thực tế, nộp và duyệt. |
| Thu gọn | NORA | Nhập tự nhiên, đề xuất effort/chia nhỏ, giải thích cảnh báo và gọi cùng planner. Các thao tác cơ bản có nút trực tiếp. |
| Thu gọn | Notes | Nơi ghi nháp và đầu vào tùy chọn cho task. Dùng lại editor hiện có, hạn chế mở rộng định dạng. |
| Thu gọn | Eisenhower | Một cách hỗ trợ đánh giá ưu tiên, có thể chỉnh trong task. Người mới có thể đi thẳng tới lịch. |
| Thu gọn | Timer | Một nguồn ghi thời gian gắn với phiên. Đồng hồ đếm lịch phải có nhãn riêng với actual. |
| Thu gọn | Chat và file nhóm | Trao đổi liên quan công việc, yêu cầu bổ sung và minh chứng nộp. |
| Giữ nền tảng | Auth, quyền, thông báo, quản trị | Đủ phục vụ vận hành đúng và an toàn cho luồng chính. |
| Dừng mở rộng, đưa khỏi điều hướng chính | Bạn bè, kết nối xã hội, cover profile | Tài khoản vẫn có tên/avatar cần cho phân công. |
| Dừng mở rộng, đưa khỏi luồng làm việc | Pet tiến hóa, skin, shop, thanh toán và báo cáo doanh thu skin | Bảo toàn dữ liệu hiện hữu trong giai đoạn thu gọn. Việc loại bỏ backend cần đánh giá riêng các giao dịch còn hiệu lực. |
| Hoãn | Chatbot tổng quát, thêm nhiều provider, thêm kênh nhắc, template phức tạp, nhiều tích hợp mới | Chỉ xét lại sau khi hoàn tất tiêu chí lõi. |
| Hoãn nếu đề cương không bắt buộc | Tự phân người toàn nhóm, tự dời lịch liên thành viên, phụ thuộc xuyên thành viên | Đòi hỏi mô hình quyền, dữ liệu và nghiệm thu riêng. |

Một tính năng mới chỉ được đưa vào đợt refactor nếu nhóm mô tả được nó cải thiện bước nào trong luồng hoạch định, dùng dữ liệu gì và được kiểm chứng bằng tình huống nào. Tính năng bắt buộc theo đề cương là căn cứ độc lập để giữ phạm vi.

## 5. Hai luồng đích

### Cá nhân

1. Ghi việc qua cùng một cửa. Nhập tên để lưu nháp, hoặc bổ sung effort và thời hạn để lập kế hoạch.
2. Xác nhận giờ làm và lịch bận khi chưa có đủ dữ liệu.
3. Xem phương án gồm các phiên dự kiến, ràng buộc và phần thiếu nếu có.
4. Xác nhận lịch khả thi. Lưu task riêng vẫn được hỗ trợ.
5. Hôm nay hiển thị một việc tiếp theo, phiên sắp tới và cảnh báo cần xử lý.
6. Ghi actual và tổng effort còn lại. Xong phiên và hoàn thành task là hai thao tác rõ ràng.
7. Khi lịch hoặc effort đổi, mở cùng một luồng xem tác động và áp dụng phương án mới.

Chọn một chính sách nhất quán cho đợt hiện tại là hệ thống chủ động tính và đưa đề xuất, người dùng xác nhận thay đổi lịch. Việc bật tự áp dụng qua chat hiện có cần được rà soát tương thích trước khi thay mặc định. Không yêu cầu thêm tự lập lịch nền trong đợt này.

Task chưa có deadline là một quyết định sản phẩm cần triển khai có chủ đích. Hiện `TaskInput.Deadline` là trường không nullable, nên chỉ đổi chữ hoặc bỏ `required` ở form chưa đủ. Giai đoạn đầu dùng Notes làm nháp; hỗ trợ task không hạn trên toàn domain cần đánh giá API, planner và dữ liệu cũ riêng.

### Nhóm

1. Leader tạo công việc và chỉ định người dự kiến, bằng form hoặc NORA.
2. Member mở đề xuất nhận việc. Hệ thống xét cả việc cá nhân và các việc nhóm đã nhận trên cùng lịch.
3. Hệ thống trả kết quả đủ điều kiện, thiếu dữ liệu hoặc không khả thi, kèm lý do. Đây là kết quả đánh giá, chưa cần thêm trạng thái workflow.
4. Member xác nhận nhận việc và áp dụng lịch riêng.
5. Khi phát sinh trễ hoặc thiếu giờ, member ghi remaining và mở phương án điều chỉnh. Leader được thông báo nguy cơ của task thuộc nhóm.
6. Nếu cần đổi người hoặc deadline, leader quyết định và người nhận kiểm tra lại theo cơ chế hiện có.
7. Member nộp kết quả, leader duyệt hoặc yêu cầu sửa.

Giữ việc nộp–duyệt ở mức gọn. Phần cần phát triển để đúng trọng tâm workload nhóm là tóm tắt tình trạng khả thi của công việc được giao, có mốc cập nhật và quy tắc chia sẻ. Leader có thể thấy “chưa đủ dữ liệu”, “có nguy cơ trễ” hoặc tổng tải được phép chia sẻ. Chi tiết lịch riêng vẫn cần quyền phù hợp. Source hiện tại chưa cung cấp một bảng tải nhóm tương đương TimeHero.

## 6. Giao diện cần tập trung lại

Đề xuất năm điểm đến chính là **Hôm nay · Công việc · Kế hoạch · Nhóm · Workload**.

| Nơi sử dụng | Nội dung và hành động cần có |
| --- | --- |
| Hôm nay | Một hành động chính theo trạng thái thực tế; phiên hiện tại/sắp tới; nguy cơ cần xử lý. Setup nhỏ, có thể bỏ qua và quay lại. |
| Công việc | Cùng một cửa nhập, lọc việc cá nhân/nhóm, chưa xếp/đã xếp; chi tiết task và mở phương án. Notes là đầu vào nháp trong khu vực này. |
| Kế hoạch | Lịch sự kiện và phiên, giờ làm trong phần thiết lập, preview trước–sau và phạm vi thay đổi. |
| Nhóm | Công việc, người phụ trách, đánh giá khả thi, nhận việc, nộp và duyệt. |
| Workload | Theo ngày, quỹ giờ và trạng thái phía trên; công việc và actual phía dưới; chuyển cá nhân/nhóm theo quyền. |

NORA vẫn có màn hình hội thoại hiện tại và link truy cập, đồng thời có nút hỗ trợ theo ngữ cảnh. Giảm vai trò cổng bắt buộc của chat. Giữ deep link cũ khi đổi điều hướng. Mô phỏng “nhận thêm việc có kịp không” đưa vào thao tác nhận việc và Workload.

Đợt UI trước đã thêm thành phần nhưng chưa hoàn tất trải nghiệm thực tế. Cần thay bố cục Hôm nay và Workload trên trang thật, xử lý trạng thái trống/lỗi và ghi bằng chứng theo bản chạy. Ảnh trang kiểm thử riêng chỉ chứng minh thành phần hiển thị với fixture đó.

## 7. Định nghĩa có thể bảo vệ cho “thông minh” và “tối ưu”

**Đầu vào** gồm effort, deadline, mức ưu tiên, giờ làm, lịch cố định, phụ thuộc, giới hạn phiên, thời gian nghỉ, actual và remaining đã xác nhận.

**Ràng buộc** gồm quyền người dùng, phiên được bảo vệ, lịch bận, tính liên tục, thứ tự công việc và deadline cứng. Phương án vi phạm bị loại hoặc hiện rõ không khả thi.

**Mục tiêu đề xuất** là giảm effort quan trọng chưa xếp được trước hạn; sau đó giảm trễ, hạn chế dịch chuyển phiên hợp lệ và giảm phân mảnh. Cần chốt thứ tự ưu tiên này bằng tình huống thực tế. Đây là mục tiêu nghiệm thu đề xuất, chưa phải mô tả rằng hàm scoring hiện tại đã tối ưu đủ mọi tiêu chí.

Planner hiện có trọng số cho lateness, idle time, fragmentation và preferred period, cùng cơ chế thử sửa thứ tự giới hạn. Có thể trình bày là thuật toán heuristic có ràng buộc. Đánh giá bằng kết quả trên cùng tập dữ liệu; chưa tuyên bố tối ưu toàn cục. NORA đảm nhiệm hiểu đầu vào và diễn giải phương án, engine chịu trách nhiệm kiểm chứng lịch. [Scoring hiện tại](../../src/SPWPOS.Application/Workspace/PlanningScoring.cs) · [Repair hiện tại](../../src/SPWPOS.Application/Workspace/PlanningRepair.cs)

Đánh giá bằng các chỉ số sau, trước và sau điều chỉnh, trên cùng fixture.

- Số phút chưa được xếp trước hạn, xét theo mức ưu tiên.
- Số task dự kiến đúng hạn, dự kiến trễ và đã quá hạn.
- Số phiên phải dời, tổng phút dời và các cam kết được bảo vệ.
- Sai số estimate so với actual trên các việc đã hoàn thành.
- Thời gian và số thao tác để người mới có kế hoạch khả thi.

Thanh tiến độ task/checklist, phần trăm chiếm dụng quỹ giờ và actual là các chỉ số riêng. Việc ở trạng thái chờ leader duyệt cần được nhận diện khi tính hoàn thành nhóm.

## 8. Thứ tự chỉnh sửa và điều kiện kết thúc

| Đợt | Công việc cụ thể | Điều kiện nghiệm thu |
| --- | --- | --- |
| 1. Chốt scope và điều hướng | Nhóm duyệt bảng giữ/thu gọn/hoãn; đưa social và commerce ra khỏi luồng chính; gộp cửa nhập và nhãn. | Người mới nhận ra ngay nơi nhập việc, xem lịch, nhận việc nhóm và xem tải. |
| 2. Hoàn tất vòng hoạch định cá nhân | Sửa Hôm nay, cửa nhập, giờ làm, preview và outcome/recovery trên trang thật; gỡ các gate chat còn chặn thao tác cơ bản. | Luồng chạy với AI tắt, reload giữ đúng task/phiên/actual, lỗi AI giữ nội dung. |
| 3. Nối tải với nhóm | Làm rõ chỉ định/claim, kết quả khả thi, cảnh báo cho leader và quyền đọc dữ liệu tổng hợp. Tái sử dụng planner của lịch riêng. | Người có giờ làm nhưng chưa có sự kiện vẫn nhận được việc; việc mới được xét cùng tải hiện hữu. |
| 4. Chứng minh tối ưu | Bộ tình huống cố định, đo chỉ số trước–sau, kiểm tra ràng buộc và quyền; sửa lỗi chặn demo. | Có bản chạy, kết quả và giới hạn được ghi lại. |

Hạn chế 30 ngày hiện tại là giới hạn sản phẩm/engine đang được áp dụng, cần hiển thị rõ. “Phạm vi tính lịch” và “deadline của công việc” là hai khái niệm riêng. Chỉ mở rộng horizon khi một tình huống trong đề cương yêu cầu và đã đánh giá ảnh hưởng.

Không ước lượng lịch hoàn thành theo số ngày khi chưa biết nhân lực, thời gian còn lại và nội dung đề cương đã duyệt. Có thể phân công theo bốn đợt và chốt từng điều kiện đạt.

## 9. Kịch bản bảo vệ xuyên suốt

Leader giao An báo cáo 5 giờ, hạn thứ Sáu 17 giờ. An có giờ làm đã xác nhận và các task khác. PLANORA kiểm tra rồi cho An nhận việc và xem các phiên phù hợp.

Một cuộc họp trùng phiên báo cáo. PLANORA chỉ ra phần ảnh hưởng và đề xuất xếp lại, giữ phiên được bảo vệ. An ghi thực làm 60 phút, xác nhận tổng còn lại 90 phút. Chạy hai biến thể có và chưa có phiên tương lai bao phủ phần còn lại.

Nếu đủ giờ, xác nhận phương án khả thi. Nếu chỉ còn 60 phút có thể xếp cho 90 phút chưa được bao phủ, hiện thiếu 30 phút; lưu actual, giữ deadline và đưa lựa chọn để An/leader quyết định. Leader nhận biết nguy cơ của công việc nhóm. An nộp và leader duyệt khi kết quả hoàn tất.

Kịch bản này chứng minh từ đầu vào đến phân công, hoạch định, theo dõi thực tế, tái hoạch định và hoàn thành. Các kiểm tra tự động đã có cần chạy trên bản tích hợp, bổ sung bằng chứng UI thật; chưa coi đoạn mô tả này là kết quả diễn tập đã đạt.
