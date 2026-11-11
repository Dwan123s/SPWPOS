# PLANORA — Phụ lục truy vết và checklist nghiệm thu

**11/11/2026 · Đi kèm [Báo cáo cải tiến luồng làm việc, NORA và Workload](PLANORA_WORKFLOW_UX_BRIEF_2026-11-11.md)**

Dùng tài liệu này khi chia việc hoặc rà soát trước buổi bảo vệ. **P0**: gỡ điểm chặn hoặc bảo vệ tính đúng của demo; **P1**: sửa trong đợt cải tiến nếu ảnh hưởng luồng chính; **Sau**: giữ trong backlog, chỉ làm sớm nếu phạm vi demo cần. Các ca dưới đây **chưa được chạy trong lượt tổng hợp**. Điền bản build, tài khoản thử, kết quả và liên kết bằng chứng khi nghiệm thu.

## A. Đối chiếu từng nguồn

| Nguồn đã nhận | Phạm vi giữ lại |
| --- | --- |
| **PLANORA_Bai_Toan_Thuc_Te.md** | Bài toán, năng lực cần giữ, luồng cá nhân, luồng nhóm, các điểm chặn, thứ tự triển khai và bằng chứng so sánh được đưa vào báo cáo chính. Các ca riêng về báo cáo 4 giờ, thiếu 90 phút, đứng yên 20 phút, deadline 60 ngày và ba người/tám gói giữ ở bảng D. |
| **PLANORA_So_Sanh_Chi_Tiet.md** | Bảng B giữ từng mục trong hai bảng Google Calendar (14) và Trello (16), cùng quyết định nghiệm thu hoặc hoãn. Kịch bản lịch học đổi giữa tuần, A → B → C, thứ tự giữ–kiểm tra–sửa–kết nối và số đo nằm ở báo cáo chính và bảng D. |
| **So_Sanh_Phan_Mem.md** | Bảng E giữ vị trí của từng sản phẩm và bài học cho PLANORA. Hai tình huống “phát sinh cuộc họp” và “làm 60 phút, còn 90 phút” giữ ở bảng D. |
| **PLANORA_UX_Report_vi.md** | Bảng C giữ đủ C1–C9 của tệp được cung cấp, gồm auth, CSS và tình trạng test. Tệp nhận được kết thúc ở C9 dù có dẫn tới các phần sau; không suy diễn các mục không có trong tệp. |
| **Ảnh đợt đầu và feedback mới** | Các ảnh về khởi đầu, giờ làm, nhập tự nhiên, lịch/checklist và tải công việc được đưa vào các mục UX và Workload. Tin nhắn mới về **UX / NORA chủ động / Workload** và cả **12 bước** được phản ánh trong báo cáo chính. Ba ảnh TimeHero mới là tham chiếu cho bảng theo ngày, chi tiết sức chứa và danh sách việc. |

## B. Checklist từ hai bảng so sánh chi tiết

Các mục dưới đây là câu hỏi về PLANORA. Mô tả đối thủ đến từ tài liệu đã gửi; chưa thực hiện benchmark độc lập.

### B1. Lịch và thời gian (Google Calendar)

| ID | Khả năng | Cần kiểm tra hoặc quyết định |
| --- | --- | --- |
| G01 | Tạo sự kiện | **P0:** giờ bắt đầu/kết thúc, loại, nội dung lưu đúng và hiển thị trên Lịch. |
| G02 | Sửa/xóa sự kiện | **P0:** lịch và xung đột không đọc bản cũ; xác định lại phiên bị ảnh hưởng, không làm mất task gốc. |
| G03 | Lịch lặp | **P1:** sửa hoặc xóa một lần xuất hiện; các tuần khác giữ nguyên. Kiểm tra thêm trường hợp lịch lặp trùng phiên. |
| G04 | Xem lịch | **P1:** phân biệt sự kiện, WorkSession, phiên đã xong và deadline trên các chế độ hiện có. Chế độ tháng/năm mới để sau nếu không cần. |
| G05 | Nhắc lịch | **P1:** nhắc phiên/hạn đúng giờ và đúng tài khoản, không lặp; chỉ kiểm tra kênh được bật trong bản demo. |
| G06 | Quản lý task | **P0:** tạo–sửa–hoàn thành nhất quán giữa danh sách, Workload và Lịch. |
| G07 | Bận/rảnh | **P0:** tôn trọng giờ làm, lịch bận, ngoại lệ và các ranh giới sát nhau; không coi toàn bộ phần trống là giờ làm. |
| G08 | Xung đột | **P0:** chặn chồng lịch, nêu lý do và phương án nếu có. Không cứu một task bằng cách dời việc đã khóa hoặc làm việc khác trễ hơn. |
| G09 | Chia sẻ lịch | **P0 về quyền:** claim riêng tư không tương đương chia sẻ lịch. Không mở chi tiết lịch cho leader. Chia sẻ theo nhiều cấp quyền để sau. |
| G10 | Mời người dự sự kiện | **Sau:** lời mời vào nhóm khác lời mời tham dự sự kiện; không thêm chỉ để ngang tính năng. |
| G11 | Focus time | **P0:** phân biệt phiên đặt lịch, phiên đang ghi thời gian, timer cá nhân và actual đã lưu. |
| G12 | Múi giờ | **P0:** đúng múi giờ tài khoản và ngày tương đối ở ca demo; **P1:** thử qua nửa đêm, khác múi giờ nếu có người dùng phù hợp. |
| G13 | Điều chỉnh khi có lịch mới | **P0:** xác định phiên bị ảnh hưởng, xem trước–sau, giải thích, xác nhận và kiểm tra lại bản lưu. |
| G14 | Hạn không khả thi | **P0:** hiển thị effort cần, khả năng xếp và phần thiếu nếu tính được; phân biệt thiếu giờ với thiếu cấu hình hoặc lỗi AI. |

### B2. Điều phối nhóm (Trello)

| ID | Khả năng | Cần kiểm tra hoặc quyết định |
| --- | --- | --- |
| T01 | Tạo nhóm | **P0 nếu demo nhóm:** người tạo và thành viên nhìn đúng nhóm và đúng quyền. |
| T02 | Mời/duyệt tham gia | **P0 nếu demo nhóm:** từ link/mã tới trạng thái tham gia, hết hạn và từ chối đúng. |
| T03 | Tạo task | **P0 nếu demo nhóm:** đường nhập/giao cơ bản khi AI tắt; AI hỗ trợ chia gói là lựa chọn. |
| T04 | Phân công | **P0:** chỉ định không tự biến thành đã nhận; không cho người ngoài quyền claim. |
| T05 | Deadline | **P0:** hạn hiển thị nhất quán; xử lý chênh lệch 365/30 ngày bằng chính sách rõ. |
| T06 | Trạng thái | **P0:** nộp chuyển chờ duyệt; duyệt hoặc yêu cầu sửa đúng trạng thái. |
| T07 | Chia nhỏ | **P1:** gói có đầu ra và tiêu chí, leader sửa được, một người có thể nhận nhiều việc. Kiểm tra giới hạn theo số thành viên. |
| T08 | Trao đổi theo việc | **P1:** xác minh chat/ghi chú hiện tại đủ để truyền lý do trễ và quyết định; chỉ mở rộng bình luận khi có khoảng trống. |
| T09 | Tệp và minh chứng | **P0 nếu dùng demo:** nộp, mở đúng quyền, phản hồi và duyệt. Test lỗi tệp và không lộ tệp riêng. |
| T10 | Tiến độ | **P0:** kiểm tra trạng thái và actual từ thao tác thật; không coi lịch đặt hoặc checklist là hoàn thành. |
| T11 | Thông báo | **P1:** giao việc, nộp và đổi trạng thái có đường cập nhật; kiểm tra nhắc quan trọng, trì hoãn việc thêm kênh. |
| T12 | Phụ thuộc | **P1 có điều kiện:** giữ phụ thuộc cá nhân. Liên thành viên chỉ thêm khi nhóm cam kết cảnh báo A → B; trước đó dùng cách xử lý của leader. |
| T13 | Tự động hóa | **P0:** chứng minh đề xuất dựa trên dữ liệu thật và dẫn tới hành động có thể xác nhận; không đếm số quy tắc. |
| T14 | Xem lịch việc nhóm | **P1:** sau claim, thấy phiên cá nhân liên kết đúng việc; leader thấy tình trạng, không cần xem lịch riêng. |
| T15 | Khả thi trước khi nhận | **P0:** ca có giờ làm nhưng không có FixedEvent, ca thiếu giờ, ca đã có phiên được bảo vệ. |
| T16 | Một người trễ ảnh hưởng người khác | **P0 nếu demo nhóm:** xử lý có người chịu trách nhiệm và có bước xác nhận; tự cảnh báo B/C chỉ nghiệm thu khi có quan hệ và kết quả đã kiểm tra. |

## C. Các phát hiện UX cần giữ

| ID | Phát hiện từ báo cáo 08/11 | Cách xử lý và xác minh |
| --- | --- | --- |
| C1 | Hướng dẫn đầu phiên tắt; tài khoản trống vào dashboard với nhiều số 0. Báo cáo còn ghi phải đăng nhập lại sau OTP. | **P0:** hướng dẫn ngắn, cho bỏ qua, lưu trạng thái đã làm. Kiểm tra tài khoản mới đi được tới lịch đầu tiên, kể cả chọn ghi việc trước. Ma sát xác thực đi với C7. |
| C2 | Chế độ secretary chuyển các thao tác chính qua NORA dù AI có thể tắt. Ghi nhanh lưu Note, không phải task có thể xếp lịch. | **P0:** đối chiếu source vẫn phát cờ `secretary-v1`. Thử AI tắt/lỗi và từng điểm vào Công việc, Giờ rảnh, Lập kế hoạch; cung cấp đường thủ công có thể dùng được. |
| C3 | Báo cáo cũ đo 42 điều khiển và khoảng 430 từ ở lần đầu; nhiều khối tranh nhau chỉ một bước tiếp. | **P1:** bố cục theo trạng thái tài khoản; một hành động nổi bật. Đo lại trên build đích với tài khoản rỗng/có dữ liệu, laptop và điện thoại; số cũ chỉ làm mốc so sánh. |
| C4 | Khoảng 12 điểm đến, cửa nhập việc chồng chéo, nhiều đường phục hồi, route thử kịch bản ít thấy; avatar dẫn tới Hồ sơ/Cài đặt khác nhau. | **P1:** thống nhất nhãn và lối vào, thử nhóm tối đa khoảng năm điểm chính và mục Thêm nếu cần; giữ link sâu và Back. Kiểm tra có nơi truy cập `#scenario` nếu giữ tính năng. Không hợp nhất planner cũ/Eisenhower chỉ để giảm số menu. |
| C5 | Nhiều chữ nhỏ và nhãn vị trí lặp; báo cáo đếm khoảng 37% khai báo font từ 11,5px trở xuống. | **P1:** ưu tiên chữ đọc được, nhãn rõ, tiêu đề gọn, focus và độ tương phản ở luồng chính. Kiểm tra ở kích thước thật; tỷ lệ khai báo CSS không phải tỷ lệ chữ đang hiện. |
| C6 | Hôm nay/Tiến độ/Thông báo trống vẫn dài; xuất hiện số 0 hoặc thuật ngữ khó khi chưa đủ dữ liệu. | **P1:** “chưa thiết lập”, “chưa có mẫu actual”, “không có việc” là trạng thái khác nhau. Nêu bước tiếp theo phù hợp, dùng nhãn tiếng Việt cho chỉ số khó. |
| C7 | Checklist mật khẩu báo xanh trước khi điều kiện ẩn từ chối; regex username có thể lỗi ở Chromium; form dài, Google login tắt vẫn hiện, URL sau đăng nhập có thể còn `#login`. | **P1 nếu chặn người mới, sau nếu chỉ gây ma sát nhỏ:** tái hiện riêng từng ca, đồng nhất checklist với validator, kiểm tra pattern trong trình duyệt, lỗi gần trường, thứ tự tab và điều hướng sau OTP/login. |
| C8 | Báo cáo ghi 61 tệp CSS, khoảng 716 KB và 231 lần `!important`; lớp CSS chồng lên nhau và các module phụ dùng chung shell. | **Sau đối với viết lại toàn bộ; P1 với màn hình đang sửa:** thống nhất token/kiểu dùng chung trong phạm vi Hôm nay và Tiến độ, kiểm tra regressions; đo lại trước khi trích số. |
| C9 | Môi trường báo cáo cũ: 643/656 bài frontend qua, 13 lỗi (10 NORA, 3 workflows); Node 22, trong khi repo yêu cầu Node 24.15+. Bản nén backend thiếu test theo báo cáo. | **P0 cho lỗi chặn:** chạy lại suite liên quan bằng Node đúng phiên bản; phân biệt test phụ thuộc câu chữ với lỗi hành vi. Ưu tiên tái hiện nút thêm lịch, lưu lịch và Cài đặt/Bảo mật. Repo local có test backend; không lấy phát hiện về bản nén làm nhận định về repo. |

Báo cáo UX dùng Chromium 1366×768 với API giả. Không có bằng chứng backend, model hoặc planner thật từ lượt đó; lỗi do mock thiếu dữ liệu và phần chưa khảo sát (admin, quên mật khẩu, timer, kết nối, phần lớn mobile) cần kiểm tra riêng nếu nằm trong phạm vi. Số liệu lịch sử không phải kết quả hiện tại.

## D. Ma trận nghiệm thu trước buổi bảo vệ

| ID | Chuẩn bị / thao tác | Phải quan sát được |
| --- | --- | --- |
| D01 | Tài khoản rỗng, AI tắt; ghi việc trước rồi thiết lập giờ làm. Chạy lại với AI bật và một lỗi giữa lúc đề xuất. | Lưu nháp/task đúng loại, có đường thủ công, không mất dữ liệu, không sinh phiên nếu chỉ lưu nháp. |
| D02 | Báo cáo 5 giờ hạn thứ Sáu 17:00, lịch học và lịch đã chốt. Thêm họp trùng một phiên. | Phiên thay đổi đúng phạm vi; fixed event và phiên khóa/đang chạy giữ nguyên; hạn và lịch sau refresh nhất quán. |
| D03 | Biến thể báo cáo 4 giờ: đã làm 60 phút và người dùng **xác nhận còn cần 90 phút**. Một lần đủ giờ, một lần chỉ có 60 phút hợp lệ trước hạn. | Actual 60 và phần còn lại 90 là hai dữ kiện riêng; không tự thay estimate gốc, không đếm lại phút đã có phiên. Ca thiếu hiện thiếu 30 phút; không ghi lịch bất khả thi. |
| D04 | Có 90 phút trống nhưng chia thành hai khoảng, task yêu cầu làm liên tục; sau đó thử một task được phép chia với cùng điều kiện. | Lý do khác nhau; giữ work window, deadline, giới hạn phiên/ngày và nghỉ. |
| D05 | Mở Hôm nay, hỏi “Hôm nay nên làm gì?”, đợi một phiên hết giờ hoặc chỉ để timer chạy 20 phút không ghi kết quả. | Một hành động tiếp theo có lý do và số liệu khớp Workload; gợi hỏi kết quả khi đúng điều kiện. Không tự suy ra người dùng bị kẹt hoặc tự lưu lịch. |
| D06 | Mở Workload ở ngày chưa thiết lập, ngày nghỉ, ngày rảnh chưa có việc và ngày quá tải; dùng chuột, bàn phím và điện thoại. | Hai hàng dễ đọc, trạng thái đúng, mở cùng chi tiết trên mọi cách nhập; không dựa riêng vào màu hay hover. |
| D07 | Từ hàng dưới, ghi actual cho một phiên, tick checklist, rồi thử hoàn thành task và gửi lại thao tác. | Ba hành động hiển thị riêng, số liệu cập nhật sau refresh, không cộng phút/XP hai lần; team task chuyển chờ duyệt. |
| D08 | Tạo preview, sửa lịch ở tab khác, rồi áp dụng bản cũ. Thử lặp lại cùng yêu cầu và undo nếu còn hợp lệ. | Bản cũ bị từ chối và dẫn tới tính lại; retry không tạo trùng; undo chỉ đổi đúng các mục có thể khôi phục. |
| D09 | Leader chỉ định An; An có giờ làm hợp lệ và không có sự kiện. Chạy ca đủ giờ, thiếu giờ, hạn 60 ngày và AI tắt. | Trạng thái chỉ định/nhận rõ; không cần sự kiện giả; thiếu giờ và hạn xa có hướng xử lý đúng chính sách. Đường nhập cơ bản không cần AI nếu đã chọn đưa nhóm vào demo. |
| D10 | An trễ ba giờ, Bình cần đầu ra của An, Cường cần đầu ra của Bình. | Leader xử lý và lưu quyết định; quyền riêng tư giữ nguyên. Nếu công bố cảnh báo dây chuyền tự động, kiểm tra riêng A → B → C bằng quan hệ được lưu và mốc thời gian thực. |
| D11 | Nhóm ba người cần tám gói việc; nộp có file, leader yêu cầu sửa rồi duyệt. | Ghi rõ giới hạn hiện tại và cách làm phù hợp nếu chưa gỡ. Nộp/chờ duyệt/đã duyệt khác nhau; file đúng quyền và tiến độ không sai. |
| D12 | Cùng một tình huống, người mới làm bằng PLANORA và quy trình lịch + bảng việc + chat. | Ghi thời gian tới kế hoạch khả thi, số thao tác sửa lịch, số lượt hỏi, phát hiện rủi ro trước hạn và người dùng có hiểu lý do không. Không suy ra kết quả từ số lượng tính năng. |

**Mẫu ghi bằng chứng:** ID · Build/model/cấu hình · Dữ liệu đầu vào · Kỳ vọng · Kết quả thấy trên UI · Task/phiên trước–sau · Đạt / Không đạt / Chưa chạy · Người phụ trách.

## E. Định vị theo báo cáo so sánh phần mềm

| Tham chiếu | Bài học được giữ | Phạm vi cho PLANORA |
| --- | --- | --- |
| **Google Calendar** | Lịch đáng tin, bận/rảnh và các mốc thời gian rõ. | Làm đúng nền tảng lịch và chứng minh đề xuất khôi phục khi có xung đột. |
| **Todoist** | Ghi việc và theo dõi việc còn lại với ít ma sát. | Ghi nhanh ngay; hỏi deadline/effort ở bước thực sự cần. |
| **Trello** | Trách nhiệm, trạng thái và minh chứng rõ. | Giữ giao–nhận–nộp–duyệt; riêng tư ở bước kiểm tra lịch. |
| **Asana** | Phụ thuộc và nguy cơ trễ ảnh hưởng công việc khác. | Tham chiếu nếu mở rộng cảnh báo nhóm; không tuyên bố đã làm. |
| **Sunsama** | Lập kế hoạch ngày có hướng dẫn và giới hạn lượng việc. | Lấy ý tưởng một bước tiếp theo và nhìn quỹ khả dụng. |
| **Reclaim.ai** | Giữ chỗ cho việc khi lịch thay đổi. | Dùng như thước đo bảo vệ cam kết và xử lý lịch bận mới. |
| **Motion** | Lập và tái lập lịch là nhóm năng lực cạnh tranh trực tiếp. | Chứng minh hiệu quả bằng cùng tình huống; có AI không tự tạo khác biệt. |
| **TimeHero** | Tham chiếu ảnh cho quỹ giờ, phần việc còn lại, chi tiết theo ngày và trạng thái trống. | Mượn bố cục hai hàng; thêm luồng ghi actual theo yêu cầu nhóm và dùng số liệu PLANORA. |

Nếu hội đồng hỏi “khác gì”, cho xem **lịch trước/sau, ghi nhận thực tế và thao tác phục hồi**. Nếu hỏi “AI tắt thì sao”, chứng minh đường thủ công trên build demo. Nếu hỏi “tự điều phối được cả nhóm không”, mô tả đúng phạm vi kiểm tra lịch cá nhân và quyết định của leader.

## F. Bằng chứng kiểm thử bản refactor

| ID | Bằng chứng tự động | Kết quả |
| --- | --- | --- |
| D01 | Kiểm tra UI khi bật và tắt nút NORA, lưu nháp vào Ghi chú, lưu task riêng, lưu giờ rảnh và xác nhận lịch ở bước riêng. | Các ca luồng thủ công đạt. |
| D02, D08 | API với báo cáo 5 giờ và cuộc họp trùng một phiên. Kiểm tra phiên khác của cùng task, phiên khóa, thiếu giờ, đề xuất cũ và gửi lại xác nhận. | Chỉ phiên trùng được thay thế. Khi không có phương án hợp lệ, lịch đã lưu giữ nguyên. |
| D03, D04, D07 | Đồng hồ cố định, ghi 60 phút thực làm và 90 phút còn lại. Chạy ca đủ giờ, ca đã giữ 60 phút và ca chỉ còn 60 phút khả dụng. Bộ test planner kiểm tra giới hạn phiên liên tục. | Xếp lần lượt 90 và 30 phút ở hai ca khả thi. Ca thiếu 30 phút từ chối áp dụng, giữ actual và deadline. |
| D05, D06 | API so sánh lời khuyên từ NORA và Workload. Chrome chạy với dữ liệu giả lập ở 1280 px và 390 px, mở bằng Enter, đóng vẫn giữ focus, ghi kết quả trước khi yêu cầu tái hoạch định. | Đạt các kiểm tra tự động. [Kết quả Chrome](../../artifacts/workflow-browser/result.json) · [Desktop](../../artifacts/workflow-browser/desktop.png) · [Mobile](../../artifacts/workflow-browser/mobile.png) |
| D09 | API bản nháp thủ công khi AI tắt, ranh giới 5 phút và 30 ngày, nhận task khi đã có giờ làm và chưa có sự kiện cố định. Bộ test nhóm kiểm tra quyền và quy trình duyệt. | Đạt các kiểm tra tự động. |

Build frontend thành công. Bộ .NET đạt 1.210 unit test và 189 integration test, bỏ qua 40 ca cần cấu hình bổ sung. Bộ frontend đầy đủ còn 12 lỗi NORA và cài đặt mật khẩu tái hiện được trên `dbf6d5c`. Chưa diễn tập toàn bộ kịch bản trên tài khoản demo có kết nối dịch vụ AI thật.
