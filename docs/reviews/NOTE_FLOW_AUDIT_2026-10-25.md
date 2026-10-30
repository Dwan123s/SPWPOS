# Kiểm thử và đánh giá luồng Note — 25/10/2026

## 1. Kết luận

Note hiện làm được chuỗi **viết → autosave → AI phân tích → kiểm tra → xem lịch → xác nhận → tick hoàn thành**. Những phần đã có kiểm thử tốt gồm lưu nháp nội dung, chống ghi đè bản cũ, xác nhận lịch trước khi ghi dữ liệu, bảo vệ task đang chạy, tick vào task thật và giữ lịch sử làm việc.

Điểm chưa hợp lý lớn nhất: **tạo công việc vẫn gắn chặt với việc xếp lịch thành công**, trong khi người dùng mong có checklist để ghi nhanh và tick trước. Đồng thời, cách nhận diện việc mới bằng tên chưa biểu diễn được từng lần thực hiện, từng mục đã chọn/bỏ chọn hoặc đã đổi tên.

Đợt kiểm tra ghi nhận **11 nhóm vấn đề/cải tiến**, dựa trên **13 ca chẩn đoán tái hiện hạn chế**. Các kết quả này không bị phủ định bởi việc bộ regression hiện tại chạy xanh: hai bộ đang kiểm tra những kỳ vọng khác nhau.

- **188/188 regression test đạt**, không fail/skip trong bộ đã chọn.
- **15 ca chẩn đoán**: 13 ca tái hiện hạn chế/rủi ro, 2 ca xác nhận bảo vệ đúng.
- **5/5 ca AI thật đạt tiêu chí cấu trúc đã đặt**, provider VertexAI.
- Frontend production build thành công, xuất vào thư mục artifact riêng.
- Tổng danh mục bằng chứng: **208 bản ghi ca kiểm thử**. Không phải 208 tình huống người dùng độc lập; có test dạng tham số và test liên quan ngoài Note.

Xem [luồng chi tiết hiện tại và luồng đề xuất](../features/NOTE_FLOW_2026-10-25.md), [danh mục từng test](../evidence/note-audit-2026-10-25/cases.csv), [bằng chứng và cách đọc](../evidence/note-audit-2026-10-25/README.md).

## 2. Phạm vi và cách kiểm tra

Kiểm tra bản làm việc đang có, gồm cả các thay đổi chưa commit; không chỉ bản HEAD. [Manifest](../evidence/note-audit-2026-10-25/manifest.json) ghi HEAD và SHA-256 của 68 tệp nguồn/cấu hình kiểm thử liên quan. Không sửa mã sản phẩm trong đợt audit này.

| Lớp | Cách chạy | Kết quả | Giới hạn diễn giải |
| --- | --- | --- | --- |
| Unit/service | Note, execution correctness, task version và các test khớp filter | 65 đạt | Phần lớn dùng interpreter kiểm soát và DB test; không đo độ hiểu ngôn ngữ của AI thật |
| Integration | NotesApiTests, NoteImpactTests, TaskVersionTests | 16 đạt | API chạy bằng test host; không phải trình duyệt người dùng thật |
| Frontend regression | 8 tệp Vitest | 107 đạt | React/JSDOM, HTTP giả lập; gồm 42 test workflows rộng hơn Note |
| Chẩn đoán backend | B01–B11 | 11 ca hoàn tất | B01–B10 kiểm soát đầu ra AI; B11 ghi đồng thời qua HTTP trên SQL Server |
| Chẩn đoán giao diện | U01–U04 | 4 ca hoàn tất | Kiểm tra state, request và unmount/remount; không đánh giá pixel/animation |
| AI thật | L01–L05 | 5 đạt | Một lượt mỗi prompt; không phải cam kết mọi cách viết đều hiểu đúng |
| Build | Vite production build | Đạt | Không thay bản frontend đang phục vụ |

SQL Server dùng database tạm `SPWPOS_Tests_<GUID>` do test factory tạo và kiểm tra tên trước khi dọn. Ca AI thật dùng nội dung giả lập, gọi interpreter trực tiếp, không ghi task/note/lịch vào tài khoản người dùng. Không thay đổi dữ liệu tài khoản thật.

**Phân loại:** `BUG` là hành vi sai/mất trạng thái đã tái hiện; `UX/DESIGN` là hạn chế thiết kế hoặc mâu thuẫn với trải nghiệm checklist mong muốn; `RISK` là khoảng trống phục hồi/API. P1 nên xử lý trước khi mở rộng Note; P2 đưa vào vòng cải tiến tiếp theo. Đây là mức ưu tiên đề xuất, không phải kết luận sự cố production đã xảy ra.

## 3. Những vấn đề đã tái hiện

### F01 — Việc cùng tên bị coi là trùng dù là lần thực hiện khác

**P1 · BUG · B01, B02**

**Cách tái hiện:** lưu và xác nhận “Đọc tài liệu” cho ngày đầu. Viết thêm một lần “Đọc tài liệu” với deadline ở ngày khác rồi phân tích tiếp. Hoặc phân tích hai mục cùng tên nhưng deadline khác ngay trong một lần.

**Thực tế:** B01: AI trả 1 việc mới hợp lệ nhưng kết quả review còn 0. B02: AI trả 2 lần thực hiện nhưng review còn 1. Task cũ không bị sửa, nhưng việc mới không được đưa ra để chọn.

**Nguyên nhân:** `ActivityKey` của TASK chỉ gồm loại và tên chuẩn hóa; bộ lọc còn loại theo `TaskNames`. Ngày, deadline và danh tính lần thực hiện không tham gia. Xem [NoteProcessing.cs](../../src/SPWPOS.Infrastructure/Workspace/NoteProcessing.cs).

**Cải thiện:** cấp `sourceItemId`/`occurrenceId`, liên kết với task đã tạo. Không dùng tên làm định danh. Một lần gửi lại phải không tạo trùng; hai lần thực hiện thật phải cùng tồn tại. Nếu chưa có định danh ổn định, cần hỏi rõ “việc mới hay sửa việc đã có” khi mơ hồ.

**Nghiệm thu:** cùng tên ở hai ngày tạo được 2 task; retry cùng mục vẫn chỉ có 1 task; đổi tên task không làm mất liên kết nguồn.

### F02 — Mục bỏ chọn không còn xuất hiện khi phân tích lại nguyên note

**P2 · UX/state gap · B03**

**Cách tái hiện:** AI nhận diện A và B; chỉ chọn A, xác nhận; phân tích lại note chưa đổi nội dung.

**Thực tế:** chỉ A được tạo. Lần phân tích sau trả 0 gợi ý và không gọi lại provider. B không có trạng thái “để sau” để tiếp tục chọn trong kết quả hiện tại. Nội dung B vẫn nằm trong note, không bị xóa.

**Nguyên nhân:** xác nhận một phần vẫn đánh dấu toàn bộ `processing.Content` là đã xử lý. Khi nội dung bằng snapshot đó, service trả kết quả rỗng. Xem [NoteService.cs](../../src/SPWPOS.Infrastructure/Workspace/NoteService.cs), [NoteProcessing.cs](../../src/SPWPOS.Infrastructure/Workspace/NoteProcessing.cs).

**Cải thiện:** phân biệt từng mục `pending`, `accepted`, `dismissed`; bỏ chọn mặc định là “để sau”, hoặc phải có thao tác “bỏ qua hẳn” rõ ràng. Hiển thị “Đã thêm 1 · Còn 1 chưa thêm”.

**Nghiệm thu:** mở lại note thấy mục B chưa tạo và có thể thêm B mà không sửa giả nội dung hay tạo lại A.

### F03 — Phân tích lại làm mất đường tiếp tục xếp lịch của task đã import

**P1 · BUG · B04**

**Cách tái hiện:** nhánh legacy tạo task từ note nhưng chưa có phiên lịch; gọi analyze lại với nội dung không đổi; thử tiếp tục schedule.

**Thực tế:** `PendingTaskIds` từ 1 thành 0, `AnalysisImported=false`, `PlanId=null`. Schedule trả `NOTE_NO_NEW_ACTIVITIES`, trong khi DB vẫn có 1 task và 0 phiên. Không mất task, nhưng đường tiếp tục từ Note bị đứt.

**Nguyên nhân:** analyze luôn reset trạng thái import/pending/plan, kể cả nhánh “không có việc mới”. Xem [NoteService.cs](../../src/SPWPOS.Infrastructure/Workspace/NoteService.cs).

**Cải thiện:** phân tích là thao tác tạo/cập nhật ứng viên, không được xóa trạng thái task đã commit. Tách `analysisRevision`, `pendingCandidates`, `unscheduledTaskIds`, `lastAppliedPlanId`.

**Nghiệm thu:** import → analyze không đổi → xếp lịch tiếp vẫn được; không cần tạo lại task.

### F04 — Đổi tên một phần khi review có thể khiến việc cũ được đề xuất lại

**P1 · BUG · B05**

**Cách tái hiện:** AI trả A và B. Đổi tên A thành A′ trong review, chỉ xác nhận A′. Viết thêm C rồi phân tích tiếp; provider trả lại A theo tên trong nội dung gốc.

**Thực tế:** A xuất hiện lại trong gợi ý dù A′ đã được lưu. Đây là **nguy cơ tạo thêm bản trùng nếu người dùng tiếp tục chọn**, chưa phải bằng chứng hệ thống tự tạo trùng ngay khi analyze.

**Nguyên nhân:** tên/danh tính nguồn chỉ được ghi bổ sung khi số mục nguồn bằng số mục xác nhận; xác nhận một phần không có ánh xạ nguồn → task đủ chắc chắn. Xem [NoteActivitiesService.cs](../../src/SPWPOS.Infrastructure/Workspace/NoteActivitiesService.cs).

**Cải thiện:** gửi và lưu ID mục nguồn khi review; tên hiển thị là thuộc tính có thể sửa. Không suy ra mục nào đã commit từ tổng số mục.

**Nghiệm thu:** đổi tên A, bỏ chọn B, thêm C: lần sau chỉ có B chưa xử lý và C; không có bản A thứ hai.

### F05 — Chỉnh thời lượng/ngày giờ trong review chưa có autosave hoặc bảo vệ khi reload

**P1 · BUG/trải nghiệm lưu nháp · U01**

**Cách tái hiện:** mở note đã phân tích, đổi thời lượng 30 → 45 phút; đợi hơn 1 giây; reload/mở lại trang.

**Thực tế:** không có POST/PUT lưu thay đổi review; `beforeunload` không bị chặn trong ca này; mở lại còn 30 phút. Autosave hiện chỉ bảo vệ nội dung note, không bao phủ các chỉnh sửa ở bước kiểm tra.

**Phân biệt:** chuyển sang note khác trong ứng dụng đã có hộp thoại bảo vệ review; quay lại ô viết và trở về review trong cùng phiên cũng đã được test giữ chỉnh sửa. Khoảng trống là refresh/đóng trang và lưu bền vững.

Xem [notes.jsx](../../frontend/src/notes.jsx), [note-activities.jsx](../../frontend/src/note-activities.jsx).

**Cải thiện:** lưu review draft gồm ứng viên đã chỉnh, trạng thái chọn, phiên bản analysis và thời điểm lưu. Có badge riêng “Đã lưu chỉnh sửa đề xuất”; stale draft phải yêu cầu đối chiếu, không tự áp vào nội dung mới.

**Nghiệm thu:** sửa thời lượng, deadline, tên và lựa chọn → reload → khôi phục đúng; ghi hai tab có xử lý version conflict; chưa xác nhận thì chưa tạo task/lịch.

### F06 — Chưa có đường ghi nhanh task chỉ có tên như Google Tasks

**P1 về định hướng sản phẩm · UX/DESIGN · B06, B10**

**Cách tái hiện:** viết “Gọi điện cho mẹ.” và muốn có ngay một checkbox thực sự.

**Thực tế:** AI thật giữ thiếu thời lượng/deadline để hỏi thêm, đây là hành vi tốt. Tuy nhiên đường `Activities` không tạo task nếu thiếu metadata lịch (`NOTE_ACTIVITY_INCOMPLETE`). Dùng endpoint import task cũ với analysis mới cũng bị chặn (`NOTE_NEEDS_REVIEW`).

**Tác động:** người dùng chỉ muốn ghi việc nhưng phải giải quyết yêu cầu lập lịch trước. Nhánh legacy tạo task trước, nhánh mới tạo task cùng lịch, làm trải nghiệm khó đoán.

Xem [NoteActivitiesService.cs](../../src/SPWPOS.Infrastructure/Workspace/NoteActivitiesService.cs), [NoteService.cs](../../src/SPWPOS.Infrastructure/Workspace/NoteService.cs).

**Cải thiện:** tạo task chỉ cần tên; thời lượng/deadline có thể chưa biết. Nút “Xếp lịch” mới yêu cầu dữ liệu lập kế hoạch. Cần thay đổi contract/validation/model nếu task hiện bắt buộc các trường này; chỉ đổi chữ trên UI sẽ chưa đủ.

**Nghiệm thu:** không có giờ rảnh, không có AI hoặc chưa biết deadline vẫn ghi được task và tick hoàn thành; không tự bịa thời lượng/hạn chót.

### F07 — Một task không xếp được lịch chặn lưu cả nhóm sự kiện hợp lệ

**P2 · UX/DESIGN · B07**

**Cách tái hiện:** chọn một sự kiện hợp lệ và một task không có đủ giờ rảnh; preview rồi apply.

**Thực tế:** có `INSUFFICIENT_CAPACITY`, apply trả `NOTE_PLAN_INCOMPLETE`, task=0 và event=0. Tính nguyên tử đang hoạt động đúng: không âm thầm lưu một nửa.

**Điểm cần cải thiện:** nếu mong muốn checklist trước, nên cho lưu task ở trạng thái “Chưa xếp lịch”, hoặc cho người dùng chủ động chọn lưu nhóm khả thi. Phải nói rõ nhóm nào được lưu và nhóm nào chưa; không tự phá tính nguyên tử của lệnh hiện tại.

**Nghiệm thu:** lỗi thiếu giờ không ngăn ghi nhận việc; người dùng thấy “Đã lưu 2 việc · 1 việc chưa có lịch” đúng dữ liệu thực. Với thao tác “Lưu toàn bộ” hiện hành, thất bại vẫn phải rollback toàn bộ.

### F08 — API xóa note còn cho phép bỏ qua kiểm tra version

**P2 · API_RISK · B08**

**Cách tái hiện:** note đã được lưu phiên bản mới, gửi DELETE không có query `version`.

**Thực tế:** xóa thành công. Controller chuyển version thiếu thành `Guid.Empty`; service chỉ kiểm tra version khi khác Empty. UI hiện gửi version, vì vậy đây là khoảng trống của API/khách gọi cũ, không phải bằng chứng checkbox hay UI hiện ghi đè.

Xem [NotesController.cs](../../src/SPWPOS.Api/Controllers/NotesController.cs), [NoteService.cs](../../src/SPWPOS.Infrastructure/Workspace/NoteService.cs).

**Cải thiện:** yêu cầu version khác Empty trên mọi DELETE; bản cũ trả 409, thiếu version trả lỗi validation. Không liên quan vượt quyền tài khoản: B09 xác nhận chặn tài khoản khác.

**Nghiệm thu:** missing/empty/stale version không xóa; current version xóa; task/lịch đã tạo vẫn giữ theo chính sách xóa note.

### F09 — Có hai nghĩa “checklist” dễ gây nhầm

**P2 · UX/DESIGN · U02**

**Cách tái hiện:** bấm “Thêm dòng checklist” ở toolbar rồi đợi autosave.

**Thực tế:** chỉ chèn chữ `☐ ` vào nội dung. Note được lưu, `createdTaskIds=[]`, không có checkbox hoàn thành và không gọi API tạo task. Checklist thật chỉ xuất hiện sau khi có task liên kết.

**Cải thiện:** nếu giữ toolbar này, đổi tên thành “Chèn ký hiệu ô vuông”. Với hướng checklist chính, mỗi dòng phải là một item có ID và trạng thái, không dùng ký tự làm nguồn sự thật. Checkbox chọn ứng viên AI cũng cần phân biệt với checkbox hoàn thành.

**Nghiệm thu:** người dùng nhìn thấy rõ “Chọn để thêm” khác “Đánh dấu hoàn thành”; gõ/nhấp vào item thật cập nhật cùng một task ở các màn hình.

### F10 — Bản nháp khi mất mạng chưa có khôi phục bền vững

**P2 · RECOVERY_RISK · U03**

**Cách tái hiện:** mất mạng khi viết nội dung mới; đợi autosave lỗi; rời/reload trang.

**Thực tế:** nội dung **vẫn còn trong tab đang mở**, có thông báo lỗi và có `beforeunload` cảnh báo. Nếu vẫn rời trang, mở lại không khôi phục được; server chưa có note.

**Cải thiện:** lưu recovery draft cục bộ theo user/note, có timestamp/version và thông báo khôi phục. Đánh giá chính sách dữ liệu cục bộ, xóa khi đăng xuất hoặc khi người dùng yêu cầu; không gọi bản cục bộ là “đã lưu lên server”.

**Nghiệm thu:** offline → viết → reload → đề nghị khôi phục; reconnect → đối chiếu version → lưu; không trộn nháp giữa tài khoản.

### F11 — Nháp chỉ có tiêu đề chưa được lưu và không cảnh báo reload

**P2 · RECOVERY_RISK · U04**

**Cách tái hiện:** note mới chỉ nhập tiêu đề, để nội dung trống; đợi >1 giây rồi reload.

**Thực tế:** 0 request lưu, không có `beforeunload` bảo vệ, tiêu đề trở về trống. Code autosave/bảo vệ đều đòi `content.trim()` khác rỗng; API cũng không chấp nhận nội dung trắng.

**Cải thiện:** thống nhất hợp đồng nháp: cho lưu title-only hoặc bảo vệ/khôi phục trạng thái chưa hợp lệ. Trường hợp xóa hết nội dung note cũ cần được thiết kế riêng: lưu note rỗng, giữ nháp hay yêu cầu xóa note; tránh UI trông như đã lưu.

**Nghiệm thu:** title-only không mất im lặng; xóa sạch nội dung có trạng thái rõ ràng. Nhánh xóa sạch nội dung được ghi nhận qua code review, chưa chạy ca runtime riêng trong đợt này.

## 4. Phần đang hoạt động đúng trong phạm vi kiểm tra

| Nhóm luồng | Bằng chứng | Kết quả |
| --- | --- | --- |
| Note mới, autosave, sửa tiếp | notes-autosave.test.jsx | Tạo/sửa cùng note; giữ chữ gõ trong lúc request chậm; flush trước analyze |
| Ghi bản cũ | NoteWorkflowTests; B11 | Không ghi đè im lặng; SQL Server concurrent PUT cho đúng 1 HTTP 200 và 1 HTTP 409 `NOTE_STALE` |
| AI lỗi/thiếu cấu hình/quota | NoteWorkflowTests; NotesApiTests; notes.test.jsx | Giữ note, không bịa task bằng rules; có retry/giải thích |
| Trộn task và event | NoteActivitiesTests; NotesApiTests | Giữ hoạt động để review, thiếu dữ liệu phải hỏi; preview chưa tạo task/event/session |
| Loại task không hỗ trợ | NoteActivitiesTests; note-activities.test.jsx | Chuẩn hóa theo catalog hoạt động, fallback OTHER theo điều kiện |
| Preview và xác nhận | NoteActivitiesTests; note-plan-preview.test.jsx | Hiện việc mới, ngày địa phương, lịch cũ; chỉ apply mới ghi lịch |
| Note/lịch đổi sau preview | NoteActivitiesTests | Chặn áp dụng đề xuất cũ; phải preview lại |
| Deadline trễ/thiếu giờ | NoteActivitiesTests; note-activities.test.jsx | Giải thích thời điểm trễ; không xác nhận lịch chưa đủ |
| Giờ ngoài availability | NoteActivitiesTests; note-activities.test.jsx | Cần xác nhận giờ một lần; không vượt giờ đã đánh dấu bận, sự kiện hoặc giới hạn ngày |
| Lưu nhóm và retry | NoteActivitiesTests; NoteWorkflowTests | Lệnh xác nhận nhóm nguyên tử; retry không nhân đôi trong các ca đã test |
| Sửa note sau khi lưu việc | NoteWorkflowTests | Ca thêm việc khác tên giữ task/phiên cũ; không tự sửa task đã tạo |
| Tick/untick | NoteChecklistTests; NotesApiTests; note-checklist.test.jsx | Đổi task thật; hoàn thành hủy phiên Scheduled, giữ lịch sử thực tế; untick về Todo |
| Phiên đang chạy/tạm dừng | NoteChecklistTests | Không kết thúc lén bằng checkbox |
| Phụ thuộc/công việc con | NoteChecklistTests | Chặn hoàn thành khi điều kiện chưa xong |
| Task stale/khác note/đã hủy | NoteChecklistTests; frontend checklist | Chặn thao tác sai; request lỗi không làm UI tự nhận đã hoàn thành |
| Đồng bộ màn hình | notes-autosave.test.jsx | Nhận workspace update/focus; không reset nội dung đang gõ |
| Một phiên xong ≠ toàn task xong | NoteChecklistTests; note-checklist.test.jsx | Không suy ra hoàn thành toàn task từ thời lượng/phiên riêng |
| Quyền truy cập | NotesApiTests; B09 | Cần đăng nhập; user khác không đọc/sửa/xóa/analyze/preview/tick note này |
| Xóa note | NoteWorkflowTests; notes.test.jsx | Có xác nhận UI; hủy/lỗi giữ nội dung; xóa note giữ task/lịch đã tạo |
| Thư viện/tìm kiếm/giao diện viết | notes.test.jsx; note-appearance.test.js | Lọc trạng thái, tìm tiếng Việt không dấu, cấu hình giấy/font theo tài khoản/note |
| Impact của nhiều việc | NoteImpactTests; note-impact.test.jsx | Kiểm tra nhóm, không ghi task/lịch; không hiển thị kết luận response cũ sau khi sửa |

Danh mục CSV giữ tên thật của từng test; bảng trên là nhóm luồng, không thay cho danh sách ca.

## 5. Kết quả AI thật

Thời điểm tham chiếu: **25/10/2026, khoảng 14:26 UTC+7**; “ngày mai” = **26/10/2026**, múi giờ `Asia/Ho_Chi_Minh`. Xem [JSON đầu ra](../evidence/note-audit-2026-10-25/live-ai.json).

| ID | Đầu vào/tiêu chí | Kết quả |
| --- | --- | --- |
| L01 | Ba việc: báo cáo 60p, slide 45p, đọc tài liệu 30p, hạn 22h mai | Đúng 3 TASK, thời lượng và deadline +07:00 đúng |
| L02 | “Gọi điện cho mẹ.” | 1 TASK; duration/deadline null; NeedsReview=true; không tự bịa 60 phút hoặc hạn 7 ngày |
| L03 | Họp mai 15–16h và báo cáo 45p hạn 20h | Đúng 1 EVENT và 1 TASK; không biến họp thành task thiếu thời lượng |
| L04 | Ba việc cũ + thêm rà soát 20p hạn 23h | Chỉ trả 1 việc mới |
| L05 | “Tooi caafn ddocj taif lieeuj duwj asn 30 phuts, hanj chots 22h ngayf mai.” | Nhận đúng đọc tài liệu 30p, hạn 22h mai |

Độ trễ đo được khoảng 1,4–2,6 giây/ca. Đây là thời gian trong lần chạy này, không phải SLA. L02 trả câu hỏi bổ sung tiếng Anh trong payload; tiêu chí cấu trúc vẫn đạt, nhưng cần rà lại quy tắc ngôn ngữ toàn bộ câu hỏi AI trước khi cam kết giao diện thuần tiếng Việt.

L04 chứng minh provider hiểu phần bổ sung trong mẫu này; **không chứng minh bộ lọc backend đúng cho việc cùng tên**, vì B01/B02 đã tái hiện vấn đề ở lớp đó.

## 6. Luồng nên hướng tới

**Viết nhanh → có task thật → tick được → chọn xếp lịch khi cần.** AI là trợ lý tách việc/bổ sung thông tin, không là điều kiện bắt buộc để ghi lại một việc.

1. **Ghi chép:** autosave nội dung và item; cho phép task chỉ có tên. Phân biệt “Đã lưu trên server”, “Đang lưu”, “Chỉ giữ trên thiết bị”, “Có xung đột”.
2. **Nhận diện:** AI đề xuất từng mục mới với ID nguồn; hiện “Mới”, “Đã thêm”, “Để sau”, “Bỏ qua”. Phân tích lại không reset task đã lưu.
3. **Thêm việc:** nút “Thêm 3 công việc” tạo task không phụ thuộc đủ giờ rảnh. Mọi checkbox hoàn thành đều trỏ vào task thật.
4. **Xếp lịch:** chọn task cần lên lịch; chỉ lúc này yêu cầu duration/deadline/giờ rảnh phù hợp. Có lựa chọn “Chưa cần xếp lịch”.
5. **Xem trước:** nói rõ việc nào ở giờ nào, việc nào bị dời, vì sao, thiếu bao nhiêu phút; task chưa lên lịch vẫn tồn tại.
6. **Hoàn thành:** tick đồng bộ Note/Công việc/Lịch; giải thích tác động tới các phiên tương lai. Untick mở lại task nhưng không tự phục hồi lịch đã hủy.

Không tự suy ra rằng sửa câu trong note là sửa task đã tạo. Nếu hỗ trợ đồng bộ hai chiều sau này, cần thao tác “Cập nhật task liên kết” và hiển thị diff cụ thể.

## 7. Thứ tự triển khai đề xuất

| Đợt | Việc | Điều kiện hoàn tất |
| --- | --- | --- |
| 1 — sửa trạng thái/dữ liệu | F01, F03, F04, F05; thiết kế ledger cho F02 | Test lỗi chuyển sang kỳ vọng đúng; không mất review, không nuốt lần thực hiện mới, retry an toàn |
| 2 — chốt trải nghiệm checklist | F06, F07, F09 | Task chỉ có tên tạo được; lịch tùy chọn; mọi checkbox có nghĩa rõ ràng |
| 3 — phục hồi và nhất quán | F08, F10, F11 | DELETE bắt buộc version; có khôi phục draft; title-only/empty có chính sách thống nhất |
| 4 — mở rộng kiểm thử | E2E trình duyệt, nhiều phiên/tab, AI nhiều cách viết | Chạy lại toàn bộ bộ hiện tại + ca mới trên bản đã sửa và DB migration tương ứng |

Không nên chỉ đổi copywriting hoặc thêm một nút tick nữa: các lỗi F01–F04 nằm ở định danh và trạng thái server, còn F06 liên quan ràng buộc tạo task.

## 8. Những phần chưa được chứng minh bởi đợt này

- Chưa chạy E2E bằng trình duyệt thật cho toàn chuỗi đăng nhập → nhập → apply → reload; UI test dùng JSDOM.
- Chưa kiểm tra trực quan trên nhiều kích thước điện thoại, bàn phím, screen reader hoặc animation.
- Chưa stress test nhiều người dùng, nhiều lần mất kết nối, nhiều tiến trình server; B11 là một ca ghi đồng thời có kiểm soát trên SQL Server.
- Chưa fuzz đủ biên 120/8.000 ký tự, 200 note, 12 ứng viên, 500 task, 1–30 ngày; đây là giới hạn đọc được từ code, không tuyên bố mọi biên đều đã chạy runtime.
- Chưa chạy dedicated case chờ hết hạn proposal 30 phút, lệch đồng hồ, đổi múi giờ, DST, event qua nửa đêm, hoặc task lặp nhiều tuần cùng tên.
- Chưa đánh giá độ đúng AI thống kê qua nhiều lần chạy/đổi provider hoặc prompt dài, mơ hồ, typo nặng. Năm mẫu thật chỉ là smoke test có kiểm tra cấu trúc.
- Chưa kiểm chứng thiết kế khẩn cấp mới chồng khẩn cấp cũ xuyên suốt từ Note UI trong đợt audit này; các test guardrail/lập lịch liên quan không thay thế ca E2E riêng.
- Các vấn đề trong báo cáo **chưa được sửa** trong đợt audit. Cần rerun sau khi thay đổi; không dùng báo cáo này làm chứng nhận bản tương lai.
