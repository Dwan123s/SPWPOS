# Đánh giá phần Ghi chú — PLANORA, 18/10/2026

**Kết luận: 6,5/10 cho mức sẵn sàng sử dụng thường xuyên.** Phần Note đã có quy trình chuyển ghi chú thành kế hoạch tương đối tốt, đặc biệt ở kiểm tra tải công việc và xác nhận lịch. Điểm yếu lớn nhất là bảo toàn nội dung đang viết, trạng thái kế hoạch và vòng đời ghi chú sau khi tạo việc. Điểm số là nhận định kỹ thuật theo mã nguồn và bằng chứng dưới đây; không phải kết quả khảo sát người dùng, đo hiệu năng hoặc chứng nhận bảo mật.

**Phạm vi và bằng chứng**

- Đọc frontend Note, library, appearance, impact, preview; API/controller/contracts/entity; NoteService, TaskInterpreter, PlanService, giao dịch tài khoản, quota và test liên quan.
- Chạy mới bốn file frontend: `notes`, `note-impact`, `note-plan-preview`, `note-appearance`: **37/37 test đạt**. Log: `artifacts/remediation/note-review-tests.log`.
- Dùng component NotePlanPreview thực và CSS production hiện tại, dựng trang kiểm tra với dữ liệu tổng hợp trên loopback. Xác nhận trực tiếp `position: fixed` và tọa độ thẻ chồng nhau. Ảnh: `artifacts/remediation/note-preview-audit.png`.
- Không gọi LLM thật, không đọc/ghi ghi chú tài khoản thật, không chạy lại backend/SQL integration trong lượt đánh giá này. Test AI đang được đọc hoặc chạy bằng test double không chứng minh độ chính xác của model thật.
- Những kết luận về lỗi layout đã tái hiện trên trình duyệt; các tình huống mất bản nháp, lỗi apply và lệch liên kết bên dưới là phân tích đường đi mã, chưa chạy fault injection end-to-end.
- Lượt này chỉ đánh giá và lưu báo cáo; chưa sửa các vấn đề Note. Các thay đổi sửa Calendar ở lượt trước được giữ nguyên.

**1. Chức năng hiện có thực sự làm được gì?**

Luồng mặc định: viết tiêu đề/nội dung → lưu ghi chú → AI trích xuất tối đa 12 việc → chọn/sửa việc → mô phỏng tác động lên tải công việc và lịch → tạo công việc → sinh đề xuất → xác nhận áp dụng lịch.

Luồng nhanh: lưu ghi chú → AI phân tích → dừng nếu `NeedsReview` → tạo việc → sinh kế hoạch → áp dụng. Luồng này chủ động thay đổi lịch sau thao tác “Tạo và lưu lịch ngay”.

AI đang làm công việc diễn giải. Bộ lập lịch trên server quyết định lịch thực thi và kiểm tra giờ rảnh, xung đột, deadline, phiên giữ nguyên và revision. Đây là cách phân chia trách nhiệm phù hợp.

Các giới hạn đang có: 120 ký tự tiêu đề, 8.000 ký tự nội dung, tối đa 200 ghi chú/tài khoản, 12 việc/lần nhập và 1–30 ngày xếp lịch. Sau khi tạo việc, ghi chú hiện bị khóa chỉnh sửa và phân tích lại.

**2. Chấm điểm theo hạng mục**

| Hạng mục | Điểm /10 | Trọng số | Nhận định |
|---|---:|---:|---|
| Trải nghiệm viết | 7,5 | 12% | Có vùng viết, checklist văn bản, font/màu giấy, chế độ tập trung; còn là textarea, chưa có autosave nội dung |
| Bảo toàn dữ liệu đang sửa | 5,0 | 15% | Version và xác nhận chuyển note tốt; chưa bảo vệ đầy đủ reload, chuyển route, bản sửa công việc AI |
| Thư viện và tìm kiếm | 6,5 | 7% | Tìm không dấu và lọc trạng thái tốt; chưa có archive, tag, trash, phân trang |
| AI trích xuất | 7,0 | 12% | Schema, kiểm tra dữ liệu, múi giờ, phân biệt lỗi provider; chưa có bằng chứng chất lượng model thật riêng cho Note trong lượt này |
| Khả năng giải thích AI | 5,5 | 8% | Có lưu ý và NeedsReview; thiếu đoạn nguồn, nguồn gốc từng trường và câu hỏi làm rõ có cấu trúc |
| Kiểm tra khả thi và xếp lịch | 8,0 | 12% | Mô phỏng nhiều việc, giờ rảnh, cảnh báo, giữ phiên cũ, bước xác nhận; trạng thái liên kết note–plan chưa nhất quán |
| Vòng đời note–task | 4,0 | 10% | Có chống tạo lại trong cùng note; khóa note sau chuyển đổi, chưa đồng bộ tiến độ/ngược về nguồn |
| Bố cục và khả năng truy cập | 6,0 | 8% | Có hai vùng, breakpoint, label, focus; lịch thử có lỗi CSS đã tái hiện, một số thông báo trạng thái gây hiểu nhầm |
| Kiểm soát truy cập và quota | 8,0 | 8% | Lọc owner, version, giao dịch, quota, hạn chế lộ lỗi provider; chưa đánh giá vận hành/retention đầy đủ |
| Hiệu năng | 6,5 | 4% | Route/CSS lazy, debounce; tải toàn bộ tối đa 200 note và chưa hủy request mô phỏng cũ |
| Theo dõi và đo chất lượng | 6,5 | 2% | Có job, token, latency; chưa có chỉ số riêng cho pipeline Note và bộ eval model thật |
| Kiểm thử | 7,0 | 4% | Nhiều test hành vi hữu ích, 37 frontend đạt; thiếu kiểm thử layout production và lỗi apply từng chặng |

Điểm có trọng số: 6,535, làm tròn **6,5/10**. Không nên cộng điểm cho giao diện đẹp để bù cho nguy cơ mất dữ liệu.

**3. Các phát hiện cần xử lý trước**

**N01 — P1: lịch thử trong Note vẫn gặp xung đột class `fixed`. Đã tái hiện.**

Nguồn: `frontend/src/note-plan-preview.jsx:78`, `frontend/src/styles/note-studio.css:848`. Sự kiện tạo `class="note-preview-entry fixed"`; CSS utility production có `.fixed { position: fixed }`.

Trong trang kiểm tra với một việc mới và hai sự kiện cố định, cả ba thẻ đều có top 234,09375px; hai sự kiện có computed position `fixed`, việc mới có `static`. Sự kiện chồng lên phiên làm việc và nhau. Bản sửa Calendar trước đó chưa bao gồm component preview này.

Hướng sửa: đổi tên biến thể thành `is-fixed-event` hoặc namespace tương tự ở JSX và CSS, giữ kiểu sự kiện trong dữ liệu riêng với tên class. Thêm kiểm tra browser dùng CSS đã build: thẻ nằm trong luồng, không chồng nếu các mục không trùng thời gian, không thoát panel sau cuộn. Test jsdom hiện tại chỉ xác nhận tên/giờ có trong DOM nên vẫn pass.

**N02 — P1: chưa có lớp bảo vệ bản nháp khi rời trang hoặc reload. Xác nhận qua mã.**

Nguồn: `frontend/src/notes.jsx:53`, `frontend/src/notes.jsx:143`, `frontend/src/note-appearance.js:61`, `frontend/src/controller.jsx:181`.

Nội dung và các task gợi ý đang sửa sống trong React state. Xác nhận bỏ thay đổi chỉ được gọi qua `requestSelect` khi đổi note; chuyển route tạo RoutePage theo key khác. LocalStorage hiện lưu phong cách viết, không lưu nội dung. Không thấy cơ chế draft recovery hoặc cảnh báo unload ở đường đi này.

Tình huống: viết vài đoạn chưa lưu → bấm Calendar hoặc reload → component bị tháo, bản nội dung chưa lưu không được khôi phục. Chỉnh thời lượng/chọn việc trong bản phân tích cũng chưa được lưu riêng.

Hướng sửa: lưu nháp phục hồi theo account/note bằng IndexedDB, debounce sau ngừng gõ; autosave server với version; trạng thái “Đang lưu/Đã lưu/Chưa đồng bộ”; guard ở điều hướng nội bộ và beforeunload khi còn dữ liệu chưa bền vững. Không gọi AI do autosave. Khi conflict, giữ cả bản máy và bản server, cho so sánh/khôi phục, không tự ghi đè. Nên cho người dùng quản lý/xóa bản nháp trên thiết bị dùng chung.

**N03 — P1: `PlanId` đang bị dùng như bằng chứng lịch đã áp dụng. Xác nhận qua mã; chưa fault-inject.**

Nguồn: `NoteService.cs:137` lưu PlanId trước khi `ApplyPlanAsync` ở dòng 141. `note-studio.jsx:168` đánh dấu bước cuối done khi có PlanId; dòng 455/494/501 dùng cùng điều kiện để ẩn nút đề xuất, mở Hôm nay và ẩn nút tiếp tục.

Nếu apply thất bại do revision đổi hoặc xung đột, note có thể còn PlanId trỏ tới kế hoạch chưa áp dụng. Frontend tải lại note nhưng không nhận trạng thái plan trong NoteView, có thể đưa người dùng sang Hôm nay và mất đường tiếp tục ngay trên Note. Luồng manual lại tạo/reschedule và apply qua endpoint Plan mà chưa thấy cập nhật liên kết Note.PlanId trong PlanService, nên trạng thái Note có thể thiếu dù lịch đã lưu.

Hướng sửa: tách `proposalId`, `appliedPlanId`, `planStatus`, `appliedAt`; hoặc trả một summary kế hoạch có trạng thái rõ ràng. Chỉ đánh dấu hoàn tất khi server xác nhận APPLIED. Có nút thử áp dụng/tạo lại khi PROPOSED, FAILED, STALE, EXPIRED. Liên kết bản đề xuất manual với note và cập nhật khi apply. Kiểm thử lỗi ở mỗi chặng, không gộp toàn pipeline dài có gọi LLM vào một transaction.

**N04 — P1 về sản phẩm: Note bị khóa ngay sau khi tạo việc.**

Nguồn: `NoteService.cs:24`, `notes.jsx:70`, `note-studio.jsx:305`. Hệ thống đang coi Note như một phiếu nhập một lần. Người dùng muốn bổ sung ý, đổi kế hoạch hay ghi thêm tiến độ phải tạo note khác; các task trong Note hiển thị snapshot từ analysis thay vì tiến độ thực.

Hướng nâng cấp: giữ note luôn chỉnh sửa được; có revision nội dung và các lần phân tích; so sánh lần mới với việc đã liên kết thành “thêm/sửa/giữ/tách liên kết”. Chỉ áp dụng thay đổi người dùng chọn. Giữ task đang làm/đã xong; không tái tạo chúng mỗi lần AI chạy. Có link trực tiếp từng task, số phút còn lại, trạng thái và lịch thực tế.

**N05 — P2: xóa note là xóa thật, không có Trash/Undo nội dung.**

Nguồn: `NoteService.cs:53`, hành động delete trong `notes.jsx:550` trở đi. UI có xác nhận và nói rõ task/lịch được giữ, đây là điểm tốt. Tuy nhiên chưa có thùng rác/khôi phục note hoặc lịch sử phiên bản.

Hướng sửa: archive riêng với trash; soft delete có thời điểm, khôi phục trong thời hạn được quy định; xóa vĩnh viễn là thao tác riêng. Xác định rõ task/lịch liên kết khi khôi phục hoặc xóa nguồn. Không dùng Undo giả chỉ thay danh sách frontend.

**N06 — P2: nguồn gốc suy luận AI còn quá chung.**

Nguồn: `TaskInterpreter.cs:35`, `NoteContracts.cs:9`, `note-studio.jsx:433`.

Khi thiếu dữ liệu, prompt gợi ý 60 phút và hạn sau 7 ngày. Có thông báo chung, nhưng model dữ liệu không phân biệt deadline nào người dùng nói thật, deadline nào mặc định; không có đoạn nguồn cho từng việc. NeedsReview chỉ là boolean, messages hiện là hai câu khái quát; người dùng khó biết chính xác cần sửa trường nào.

Hướng sửa: mỗi task có source span/quote ngắn, field provenance (`explicit`, `inferred`, `default`), các lý do needsReview và câu hỏi cụ thể. Đánh dấu “Thời lượng gợi ý” / “Chưa có hạn do bạn xác nhận”. Đừng tạo phần trăm confidence nếu chưa hiệu chỉnh trên dữ liệu eval. Cho hỏi lại về ngày/giờ thật sự mơ hồ. Giờ rảnh, sự kiện và dependencies cần dạng đề xuất riêng và xác nhận trước khi cập nhật.

**N07 — P2: nhãn “Đã kiểm tra” chưa phản ánh hành động người dùng.**

`note-library.jsx:7` trả nhãn này chỉ vì tồn tại analysis, bao gồm kết quả NeedsReview. Nên dùng “Đã phân tích”, “Cần làm rõ”, “Đã xác nhận việc”, “Đã tạo việc”, “Đã lưu lịch” theo trạng thái server. Không dùng một cờ có/không analysis để đại diện cả tiến trình.

**N08 — P2: sửa và tổ chức việc trước khi nhận còn hạn chế.**

Có sửa tên, deadline, thời lượng, ưu tiên, loại việc và hard deadline. Chưa có UI sửa mô tả, thêm thủ công một việc AI bỏ sót, tách/gộp việc, category/parent/dependency ngay tại review; import chủ động bắt category/parent null và tạo độc lập. Với ghi chú dài, phải quay lại màn khác để hoàn thiện điều kiện.

Hướng nâng cấp: giữ review gọn mặc định, thêm “Chi tiết” và “Thêm việc”; cho liên kết phụ thuộc/chọn dự án bằng ID thực của tài khoản. Mọi ràng buộc mới phải được planner và server kiểm tra. Cho xem trước diff thay vì chỉ thêm nhiều nút vào một card.

**N09 — P2: giới hạn và cách tải thư viện phù hợp quy mô nhỏ.**

GetNotes lấy tối đa 200 bản đầy đủ; search/lọc tại client. Riêng phần content có thể đạt tổng 1,6 triệu ký tự khi 200 note cùng chạm giới hạn, chưa tính analysis và JSON. Đây là mức trần ký tự tính từ code, chưa đo kích thước response/độ trễ thực tế.

Hướng nâng cấp: endpoint danh sách trả summary/cursor; chi tiết note tải theo ID, tìm kiếm server khi quy mô tăng; archive thay vì buộc xóa để tạo mới. Lazy route/CSS hiện có nên giữ. Không cần vector database chỉ để tìm tiêu đề hoặc lọc trạng thái.

**N10 — P2: request mô phỏng cũ vẫn có thể tiếp tục tốn tài nguyên.**

`note-impact.jsx:58` có debounce 450ms và chặn kết quả cũ ghi đè UI, xử lý này tốt. Nhưng cleanup chỉ đánh dấu cancelled/clear timeout; `api.js` chưa nhận AbortSignal, nên request đã gửi vẫn chạy. Thay đổi từng trường hoặc identity workspace có thể tạo thêm mô phỏng.

Hướng sửa: hỗ trợ AbortController xuyên API, hash đầu vào, cache ngắn có revision và dedupe request giống nhau; server kiểm tra hủy. Giữ cơ chế bỏ qua kết quả cũ kể cả khi đã thêm abort. Đo số mô phỏng/phiên chỉnh sửa và p95 trước khi chọn cache/throttle.

**N11 — P2: Note và chat NORA chưa thành một không gian làm việc liên tục.**

BuildAgentContext đưa task, lịch, giờ rảnh và preferences vào context, chưa có note được người dùng chọn. Luồng phân tích Note là request riêng, không có hội thoại làm rõ gắn note. Không nên tự gửi toàn bộ thư viện cho NORA.

Hướng nâng cấp: “Hỏi NORA về ghi chú này”, scope rõ note/version, chỉ gửi nội dung đã chọn; trả thao tác có cấu trúc và cho xem trước thay đổi. Giữ chung contract cho provenance/clarification thay vì hai bộ quy tắc ngày giờ tách biệt.

**N12 — P2: test chưa bao phủ các điểm lỗi quan trọng nhất về trải nghiệm.**

Điểm tốt: test hiện có kiểm tra xác nhận đổi note, lưu phong cách, retry, không tạo trùng, chọn giờ rảnh, không thay đổi lịch khi chỉ mô phỏng, bỏ kết quả mô phỏng cũ. Backend có test owner/version/import atomicity/needsReview/planner, nhưng chưa chạy lại trong lượt này.

Thiếu cần bổ sung: reload/điều hướng khi dirty; khôi phục draft và review edits; hai tab sửa cùng note; apply thất bại sau khi đã lưu PlanId; manual apply đồng bộ trạng thái Note; CSS production trên desktop/mobile; test chất lượng AI thật có bộ dữ liệu chuẩn, có chi phí kiểm soát và tách khỏi CI mặc định.

**4. Điểm mạnh nên giữ**

1. Luồng xem trước công việc và xem trước lịch có ranh giới rõ; phân tích đơn thuần chưa tạo việc/lịch.
2. Import trong cùng note chống tạo trùng; version và transaction bảo vệ việc sửa đồng thời. Đây không phải bảo đảm chống trùng ngữ nghĩa giữa hai note khác nhau.
3. AI không khả dụng thì báo rõ, không giả kết quả trích xuất bằng rule rồi tự nhập như AI.
4. Planner kiểm tra ràng buộc thật, không để LLM tự quyết mọi thời điểm lưu.
5. Simulation xét cả các việc được chọn, tải trước/sau và tác động tới việc đang có; đây là giá trị nổi bật của Note trong PLANORA.
6. Tìm tiếng Việt không dấu, focus mode, label/focus/44px controls và CSS lazy là nền UX tốt; appearance được phân theo account/note và lỗi storage không làm hỏng luồng.
7. API giới hạn owner, giới hạn đầu vào, quota và error-code; usage không cố bịa token/cost khi provider không cung cấp.

**5. Ý tưởng phiên bản nâng cấp**

Mục tiêu: **một ghi chú luôn sửa được, các việc/lịch được đề xuất và cập nhật có kiểm soát**.

- Vùng trái: thư viện, tìm kiếm, ghim, dự án/tag và archive.
- Vùng giữa: editor gọn; autosave rõ ràng; lịch sử phiên bản; checklist thực có ID; note không bị khóa sau khi chuyển thành việc.
- Vùng phải: chỉ mở khi cần AI/kiểm tra; mỗi đề xuất chỉ ra đoạn nguồn, điều đang thiếu và tác động lên lịch.
- Mobile: editor là màn chính, thư viện và AI là sheet/tab; một hành động chính theo trạng thái, tránh hiển thị ba cột cùng lúc.
- Sau khi tạo việc: mỗi dòng hiển thị liên kết task thực, tiến độ, lịch tiếp theo; thay đổi note sinh đề xuất diff, không tạo lại mọi việc.
- Animation tập trung vào lưu, chuyển viết/review và thêm/sửa item; giữ reduced-motion, không thêm loading kéo dài hoặc “thinking” giả vào tác vụ lưu bản nháp.

Không cần đưa editor cộng tác, RAG hoặc nhiều agent vào đợt đầu. Những đầu tư đó chưa giải quyết các lỗi mất dữ liệu và trạng thái hiện tại.

**6. Thứ tự triển khai và điều kiện hoàn thành**

| Đợt | Công việc | Điều kiện kiểm chứng |
|---|---|---|
| A — độ tin cậy | N01, N02, N03; trạng thái N07 | Reload không mất bản nháp; thẻ preview nằm đúng panel; apply fail không báo done; manual/auto thống nhất trạng thái |
| B — vòng đời dữ liệu | N04, N05, N08 | Note sửa được sau import; diff không nhân đôi task; conflict không ghi đè âm thầm; trash khôi phục được |
| C — AI hữu ích | N06, N11, bộ eval Note | Phân biệt dữ liệu rõ/default; hỏi đúng trường thiếu; note-chat giữ scope; dữ liệu eval chấm đủ/bớt việc và ngày giờ |
| D — mở rộng | N09, N10, đo lường N12 | List summary/cursor; request cũ được hủy; báo cáo p50/p95, lỗi, quota và số lần AI phải sửa lại |

Tiêu chí AI đề xuất: precision/recall trích xuất việc, đúng deadline/múi giờ, bảo toàn điều kiện, tỷ lệ phải sửa tay, tỷ lệ clarification hữu ích và chi phí/lần phân tích thành công. Chốt bộ dữ liệu và baseline trước khi đưa ra tỷ lệ mục tiêu; không suy ra “AI chính xác 95%” từ test double.

Về dữ liệu, nên chuyển từ `AnalysisJson`/`CreatedTaskIdsJson` đơn lẻ sang các thực thể có version và liên kết như NoteRevision, NoteAnalysisRun, NoteTaskLink khi thực hiện đợt B/C. Lưu snapshot để truy vết, nhưng hiển thị tiến độ từ task thật. Migration cần giữ các note/task lịch sử, có kế hoạch backfill và kiểm tra owner xuyên liên kết.

**Việc nên làm đầu tiên:** sửa preview và trạng thái plan, sau đó bảo vệ bản nháp. Tiếp theo mới mở rộng AI và editor. Chưa có căn cứ để xem phần Note hiện tại là một hệ thống ghi chú hoàn chỉnh hoặc tự theo dõi nội dung ở nền.
