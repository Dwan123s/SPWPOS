# PROMPT TRIỂN KHAI TOÀN BỘ PLANORA / SPWPOS

Bạn là kỹ sư chịu trách nhiệm trực tiếp triển khai và kiểm chứng hệ thống PLANORA / SPWPOS. Hãy sửa toàn bộ vấn đề đã xác nhận và xây dựng đầy đủ các tính năng được mô tả dưới đây. Công việc bao gồm backend, frontend, database, background jobs, kiểm thử, tài liệu và chuẩn bị vận hành.

Đây là yêu cầu thực hiện công việc. Sau khi đọc hiện trạng và lập baseline, bắt đầu sửa code; không chỉ đưa thêm một kế hoạch hoặc dừng ở việc tạo giao diện minh họa.

## 1. Bối cảnh và tài liệu

Repository hiện có ASP.NET Core/C#, EF Core/SQL Server, React/Vite/Tailwind. AI đang dùng Gemini qua Google Cloud Vertex AI; trợ lý tên NORA, có mascot cú xanh. Xác minh phiên bản và cấu trúc thực tế trước khi sửa.

Nếu có trong repository, đọc:
- `docs/reviews/SYSTEM_REVIEW_2026-10-15.md`.
- `docs/plans/PLANORA_REMEDIATION_PLAN_2026-10-15.md`.
- README, AGENTS.md áp dụng, tài liệu auth/AI/planning/voice và CI hiện tại.

Bản mô tả này tự chứa phạm vi cần thực hiện. Tài liệu cũ là nguồn bằng chứng, không thay thế việc đọc code hiện tại. Một số lỗi có thể đã được sửa sau đánh giá: xác nhận lại để không sửa trùng hoặc làm mất thay đổi mới.

## 2. Nguyên tắc xuyên suốt

1. Giữ dữ liệu, thay đổi chưa commit và các tính năng đang hoạt động. Không reset/revert/xóa database để làm việc dễ hơn.
2. Giữ thương hiệu PLANORA, tên NORA và mascot. Giữ Enter gửi, Shift+Enter xuống dòng, IME, SSE thật, voice preview, preview/apply/undo và responsive.
3. Giữ kết nối Vertex AI đang hoạt động. Nếu đang dùng API key, tiếp tục hỗ trợ; không tự chuyển sang ADC hoặc Gemini free. Credential nằm phía server trong secret store phù hợp; không yêu cầu dán vào chat, không log hoặc đưa vào frontend.
4. Planner và validation server quyết định tính hợp lệ. Model không được tự nới deadline cứng, sửa giới hạn hoặc bỏ qua phiên được bảo vệ để trả lịch thành công.
5. Mọi mutation phải kiểm tra ownership, quyền, trạng thái, revision và idempotency thích hợp. Giữ auth/OTP/refresh/revocation, transaction, CSRF/CSP và bảo vệ session đang chạy/khóa.
6. Không giả AI bằng câu trả lời cố định, giả streaming bằng chia chuỗi final, giả thanh toán hoặc dùng dataset chưa kiểm chứng như dữ liệu chính thức.
7. Giữ một ứng dụng có module rõ. Không rewrite toàn bộ hoặc thêm microservices, vector DB, multi-agent, solver hay framework mới khi chưa có lý do đo được.
8. Giao diện tiếng Việt, thuật ngữ nhất quán, thông báo cho người dùng dễ hiểu. Chi tiết kỹ thuật dành cho log/admin được phép, không đẩy vào luồng người dùng.
9. Quyết định triển khai nhỏ, reversible được tự xử lý. Chỉ hỏi khi thiếu thông tin có ảnh hưởng thật; tiếp tục phần độc lập trong lúc chờ.
10. Xác minh tài liệu chính thức trước thay đổi SDK/API/provider/framework. Không đoán endpoint, model, giá, quota hoặc lịch nghỉ chính thức.

## 3. Kiểm kê, baseline và tiến độ

Tạo `docs/plans/PLANORA_FIX_PROGRESS.md`, liệt kê mọi yêu cầu trong prompt và F01–F17. Mỗi mục có: trạng thái, reproduction, file liên quan, acceptance, migration, kết quả kiểm thử và phần còn thiếu.

Trạng thái gồm NOT_STARTED, IN_PROGRESS, DONE, IMPLEMENTED_PENDING_EXTERNAL_VERIFICATION, BLOCKED_EXTERNAL. DONE chỉ dùng khi đã đạt acceptance bằng bằng chứng mới.

Thực hiện:
- Đọc working tree và hướng dẫn repo; xác định các service/frontend/migration liên quan.
- Build và chạy nhóm kiểm thử phù hợp; ghi lỗi baseline riêng với lỗi phát sinh do sửa.
- SQL tests dùng database riêng; không coi skipped là passed.
- Tái hiện các probe trong `artifacts/system-review-*` nếu còn phù hợp; không tin tuyệt đối log cũ.
- Với lỗi nghiệp vụ xác nhận được, thêm regression có ý nghĩa rồi sửa nguyên nhân.
- Thay đổi style nhỏ dùng build/kiểm tra trực quan phù hợp, không tạo test chỉ sao chép implementation.

Chia thành các giai đoạn có thể review, triển khai hoàn chỉnh theo luồng dọc backend → dữ liệu → UI → kiểm chứng. Cập nhật tiến độ sau mỗi nhóm, không chỉ khi kết thúc toàn bộ.

## 4. Giai đoạn A — Tính đúng đắn của lịch và số liệu

### A1. Capacity thống nhất — F01

Thống nhất cách tính giữa dashboard, insights, generate và apply. Định nghĩa rõ:
- Giờ rảnh trên calendar sau khi trừ sự kiện và ngoại lệ bận.
- Ngân sách ngày sau khi áp maximum daily minutes và capacity policy.
- Phần đã thực hiện, đã giữ chỗ và còn có thể xếp.
- Khoảng ngày, timezone và revision đang được phân tích.

Không trừ một phiên hai lần; không coi capacity toàn ngày là phần còn lại. Ưu tiên backend/read model chung thay vì frontend tự viết công thức khác.

Acceptance: rảnh 600 phút, factor 0,8, giới hạn 60 phút/ngày không hiện 480 phút khả dụng để xếp. Cùng phạm vi cho cùng số liệu trên mọi trang. Kiểm tra ngày đã làm một phần, hết giờ, không có giờ rảnh, phiên khóa, qua nửa đêm và timezone.

### A2. Risk dựa trên khả năng xếp thực tế — F02

Task risk phải phản ánh daily/session limits, thời gian nghỉ, contiguous slot, dependency và deadline. Phân biệt rõ thiếu ngân sách ngày, thiếu khoảng liên tục, tiền nhiệm chưa xong và đã quá hạn. Không tạo phần trăm xác suất khi chưa có mô hình được hiệu chỉnh.

Acceptance: task 120 phút, ngân sách chỉ 60 phút trước hard deadline không báo Low trong khi planner thiếu 60 phút. Lý do và số phút chưa xếp phải giải thích được bằng dữ liệu.

### A3. Task cha, việc con và dependency — F03

Làm rõ task thực hiện, container và milestone. Container hoàn thành khi các con cần thiết hoàn thành; predicted finish của cha được tính từ các con để downstream có thể xếp sau. Nếu có bước duyệt riêng, biểu diễn rõ thay vì ẩn trong container.

Giữ chống chu kỳ gồm quan hệ cha/con, không double-count workload; tính closure của tác động khi sửa/xếp lại một việc con.

Acceptance: P có con A; B phụ thuộc P; A và B mỗi việc 60 phút, đủ giờ thì B được xếp sau A. Thiếu một con vẫn chặn đúng. Reschedule một con cập nhật downstream liên quan nhưng không kéo việc không liên quan vô cớ.

### A4. Execution, resume và timer — F04

Tách planned interval khỏi actual execution. Có policy rõ cho start/resume/overrun, pause qua giờ/ngày, sự kiện đang diễn ra, deadline và timer bỏ quên. Dùng validation chung khi phù hợp; không mất dữ liệu actual đã ghi.

Acceptance: pause phiên 14–15h rồi resume 16h30 khi có sự kiện 16–17h phải cho kết quả đúng policy, thông báo rõ. Chỉ một phiên active/account. Reload, hai tab, retry complete/skip và sửa actual không làm sai segment hoặc nhân đôi kết quả.

### A5. Policy chat và planner — F07

Phân biệt thời lượng user yêu cầu với estimate cá nhân hóa. Giữ 120 phút nếu user yêu cầu rõ hai tiếng liên tục; nếu muốn điều chỉnh phải giải thích/đề nghị phù hợp. Các hard constraints được giữ ở mọi đường đi.

Giữ preview/apply có revision và expiry, diff trước/sau, cảnh báo phần thiếu và undo trong phạm vi an toàn. Khi dữ liệu đổi giữa generate/apply phải kiểm tra lại hoặc báo stale, không áp bản cũ.

## 5. Giai đoạn B — Nâng framework LTS — F17

Theo yêu cầu người dùng ngày 15/10/2026, giữ .NET 8. Đồng bộ ASP.NET/EF/Identity và dependency tương thích .NET 8, SDK scripts, lockfiles, CI, publish/runtime và tài liệu. Không tự nâng framework lên .NET 10.

Tách thay đổi framework khỏi rewrite nghiệp vụ. Migration thử trên database riêng/bản sao; không tự apply vào dữ liệu đang phục vụ. Có hướng rollback và giới hạn rollback schema rõ ràng.

Acceptance: locked restore, Release build, publish frontend cùng API, auth/OTP/refresh/revocation/Google login, planner, SSE, voice và SQL concurrency/migration được kiểm chứng. Không đổi hàng loạt dữ liệu chỉ vì cập nhật package.

## 6. Giai đoạn C — NORA hoạt động bền vững

### C1. Vòng đời hội thoại — F05

Có conversation entity hoặc mô hình tương đương, danh sách theo cursor, tin nhắn theo conversation, tìm kiếm server, rename/archive/delete/export và retention. Backfill lịch sử cũ; giữ ownership.

Không để API chỉ trả 100 lượt làm mất khả năng truy cập lịch sử; thay giới hạn cứng 2000 bằng quota/retention có thông báo và đường phục hồi. Tách idempotency tombstone khỏi nội dung khi cần xóa chat.

Acceptance: lịch sử vượt 100 lượt vẫn đọc/tìm được; archive/delete không làm replay tạo task trùng; user khác không truy cập được; loading/empty/error rõ ràng.

### C2. Context theo mục đích — F06

Lựa chọn context theo intent/range, có total/included/truncated/time range hoặc tương đương; query thêm khi cần. Không trả lời “tất cả” từ một tập con mà không cho biết giới hạn.

Task ngoài 50 dòng đầu, trùng tên, event tương lai và follow-up được xử lý đúng. Câu hỏi làm rõ/options phải là dữ liệu có cấu trúc; không phụ thuộc regex đoán lựa chọn trong văn bản.

Giữ pipeline đọc dữ liệu → hiểu intent → kiểm tra → đề xuất → áp dụng. Input từ note/task/history là dữ liệu không đáng tin, không thay policy. Read-only không có mutation. Memory nếu thêm phải có consent, phạm vi, xem/sửa/xóa; không lưu suy đoán nhạy cảm như sự thật.

### C3. Streaming và trạng thái xử lý

SSE phải xuất chữ từ provider trước final khi provider hỗ trợ; parse bounded, xử lý UTF-8/chunk, lỗi giữa stream, timeout, cancel và reconnect/retry thích hợp. Chỉ áp dụng sau khi intent đầy đủ được validate.

UI phân biệt đang đọc dữ liệu, hiểu yêu cầu, kiểm tra lịch và lưu kết quả. Không hiển thị hoặc bịa private chain-of-thought. Giữ mascot thinking nhỏ, không nền/khung nặng, chuyển động mượt và có pause/reduced-motion.

Acceptance: Stop không gây mutation lặp; mất mạng sau commit vẫn tra được kết quả bằng request ID; phần chữ chưa xác nhận không hiển thị như lịch đã lưu.

### C4. Quota, usage và chi phí — F10

Giữ IP protection, thêm limiter theo account sau authentication và giới hạn AI concurrency. Có reservation/settlement quota hoặc cơ chế tương đương để call đồng thời không vượt budget.

Lưu usage khi provider trả: model/version, tokens/audio, latency, status, cost ước tính theo bảng giá cấu hình có version. Thiếu metadata thì ghi unknown/ước tính đúng nhãn. Không log nội dung nhạy cảm.

Retry bounded/backoff/jitter cho lỗi tạm thời ở bước gọi model; giữ deadline/request ID và không lặp side effect. Có admin xem usage, quota và ngắt AI khi cần. Không tự chuyển provider chưa được cấu hình hoặc hứa credit Cloud loại bỏ mọi giới hạn.

Acceptance: chạm budget bị chặn trước call; hai tab không vượt quota nhờ race; user chung Wi-Fi có quota account riêng; 429/5xx không tạo task/order/phiên trùng.

### C5. AI eval

Bộ câu tiếng Việt version hóa, tối thiểu bao phủ: “6h” mơ hồ, chiều mai, thứ Sáu tuần sau, qua nửa đêm, tên task trùng, deadline cứng/mềm, dependency, lịch khóa, follow-up, stale/cancel/retry và prompt injection.

Đo JSON hợp lệ và action đúng riêng. Offline/test-double chạy trong CI; live provider eval có bật tường minh, timeout, giới hạn lượt/chi phí và dữ liệu synthetic phù hợp. Không dùng mock để tuyên bố AI thật đã đạt.

## 7. Giai đoạn D — Voice-to-text — F11

Giữ nhận chữ trực tiếp trong ô chat khi nói, bản cuối từ Cloud khi cần, hủy/khôi phục draft, không tự gửi, release micro và xử lý response muộn.

Đo số preview, audio seconds xử lý và latency. Giảm gửi lại clip tích lũy bằng pacing/adaptive debounce/VAD phù hợp; không cắt tùy tiện làm hỏng audio container. Chỉ thêm STT streaming riêng khi có lý do đo được.

Kiểm tra quyền micro, browser không hỗ trợ, thiết bị ngắt, silence/noise, tên riêng, 6h/16h và các cách đọc ngày giờ tiếng Việt. Giữ giới hạn 60 giây/2 MiB hoặc thay đổi có chủ đích, đồng bộ frontend/backend.

Acceptance: preview không lặp chữ/ghi đè draft; final/cancel không bị response cũ ghi đè; không giữ micro sau khi dừng/rời trang; chi phí preview đo được, không tuyên bố tương thích browser chưa thử.

## 8. Giai đoạn E — Recovery Mode — F08

Định nghĩa mặc định: hai ngày có lịch làm việc liên tiếp mà có phiên bị bỏ lỡ; ngày nghỉ hợp lệ không tính. Cho cấu hình policy và hiển thị rõ, tách với tình huống task đã quá deadline hai ngày.

Worker xác định affected tasks/dependencies, tạo recovery proposal có trigger, revision, diff và dedupe. Default đề xuất; tự áp dụng chỉ khi user đã bật policy tương ứng.

Quy tắc:
- Giữ running/paused/locked sessions và lịch cố định.
- Deadline còn hiệu lực: tìm phương án trước hạn trong giờ rảnh thật.
- Hard deadline đã qua: hỏi user chọn gia hạn/giảm phạm vi/hủy hoặc xử lý khác; không tự nới.
- Không đủ thời gian: báo rõ thiếu bao nhiêu và các lựa chọn có thể thực hiện.
- Quiet hours, opt-out, expiry và undo trong phạm vi an toàn.
- Hai worker, hai instance, retry hoặc outage không tạo/apply kế hoạch trùng.

Acceptance: tình huống user đổi lịch trong lúc recovery đang tính phải được kiểm tra revision; đề xuất không khả thi không được tự lưu như đã giải quyết.

## 9. Giai đoạn F — Insights và living notes

### F1. Thống kê thực tế — F09

Xây truy vấn 3/7/30 ngày và custom range hợp lý, so kỳ trước, drill-down ngày và lọc loại việc/trạng thái.

Phân biệt:
- Lịch đã lên theo planned interval.
- Thời gian tập trung thực theo execution segments.
- Task hoàn thành theo CompletedAt.
- Bỏ lỡ/skip/cancel theo policy rõ.

Phân bổ segment qua nửa đêm theo timezone; xử lý open segment, chỉnh actual và mẫu nhỏ. Danh sách có pagination/filter thay vì 8 dòng mang nghĩa toàn bộ. Capacity/bias có freshness, sample count và giải thích, không đồng nhất thời gian làm lâu với năng suất cao.

Acceptance: làm hôm sau không bị ghi thành làm hôm trước vì scheduled end; không double-count việc cha/con; cùng phạm vi cho số liệu thống nhất với dashboard/planner.

### F2. Note sau import — F15

Cho chỉnh note sau khi tạo task bằng document revisions/import batches/provenance hoặc mô hình tương đương. Re-analysis hiển thị diff việc mới/cập nhật/đã tạo; user xác nhận. Giữ snapshot nguồn và chống trùng.

Thông tin AI gợi ý như 60 phút/7 ngày phải có nhãn. Không tự sửa task đã thực hiện khi note đổi; note deletion không tự xóa task. Giữ autosave/concurrency và nội dung khi AI lỗi.

Acceptance: lưu/sửa/phân tích lại/retry không nhân đôi task; concurrent edits được giải quyết rõ; note vẫn là tài liệu sống, các liên kết nguồn kiểm tra được.

## 10. Giai đoạn G — Giờ rảnh, sự kiện, lễ VN và calendar sync

### G1. Giờ rảnh và event series

Editor tuần trực quan với form nhập chính xác cho bàn phím/mobile, preset, validation và cách biểu diễn rảnh/bận/khóa rõ. Khoảng qua nửa đêm chuẩn hóa thành slot đúng ngày.

Event series có series/occurrence/exception, sửa lần này/từ lần này/cả chuỗi; giữ timezone/DST và policy overlap/buffer. Lưu giờ rảnh cho biết các phiên bị dời và nguyên nhân.

Acceptance: sửa một lần không phá toàn chuỗi; update series không tạo trùng; phiên protected không tự di chuyển; rule server không bị lách qua client.

### G2. Lịch lễ Việt Nam và reminder

Dataset có nguồn đáng tin, năm/version/timezone và thời điểm cập nhật. Phân biệt ngày lễ/ngày kỷ niệm/lịch nghỉ thực tế. Không để LLM tự bịa ngày âm/nghỉ bù.

Lớp lễ riêng, bật/tắt được; default đánh dấu/nhắc, user chọn có bận cả ngày hay không. Giữ khả năng thêm sự kiện cá nhân. Cho chọn nhắc trước 24h hoặc hôm trước vào giờ cụ thể; không coi hai cách này luôn giống nhau.

Notification intent/delivery/dedupe/quiet hours và retry bền vững; trạng thái in-app khác push/email gửi được khi đóng app. Chỉ thêm channel bên ngoài với consent/cấu hình phù hợp.

Acceptance: update dataset không trùng hoặc ghi đè custom event; timezone/worker retry không nhắc hai lần; outage chạy bù theo policy, không spam thông báo quá hạn. Thiếu nguồn chính thức phải được ghi rõ, không seed giả thành thật.

### G3. Google/Outlook Calendar

OAuth riêng theo provider, scope tối thiểu, revoke/reconnect; đọc busy trước, ghi phiên theo opt-in sau. Google login không tự cấp quyền calendar.

Lưu external IDs/version/sync cursor, bảo vệ token server, outbox/queue và retry. Chống nhập ngược phiên PLANORA thành event bận tự xung đột; có conflict policy khi sửa ở cả hai nơi, event bị xóa hoặc token hết hạn.

Acceptance: provider lỗi không làm mất task/không mở được workspace; không sync trùng; chưa có credential thì hoàn thành code/tests/UX nhưng ghi rõ chưa xác minh live.

## 11. Giai đoạn H — Hiệu năng, UX và kiến trúc

### H1. Query và worker — F12

Tách projection/query theo use case, range/cursor, tránh full workspace cho detail/category/notification nhỏ. Lịch sử vượt giới hạn tải không được biến mất âm thầm. Tính learning/risk incremental hoặc background khi hợp lý, giữ transaction cho ràng buộc quan trọng.

Worker xử lý due-time/batch, lease/claim/dedupe cho nhiều instance; bounded retry, cancel và timeout. Đo query count, payload, duration, memory và worker lag trước/sau. Index theo execution plan, không thêm hàng loạt theo phỏng đoán.

### H2. Static assets — F13

API private giữ no-store; HTML revalidate; asset có hash cache dài immutable. Compression theo hạ tầng. Tối ưu font hỗ trợ tiếng Việt, sprite alpha/chất lượng và styles theo route khi có lợi.

Acceptance: header thực tế đúng; deploy mới không kẹt HTML/asset cũ; không cache nhầm dữ liệu riêng; mascot không vỡ frame hoặc mất pause/reduced-motion. Báo kích thước thô, truyền mạng và chỉ số tải riêng, không đánh đồng.

### H3. Bootstrap và lỗi mạng — F14

Phân biệt 401, offline, timeout và 5xx. Không đưa mọi lỗi khởi động về landing như user chưa đăng nhập. Có retry, loading/error/empty phù hợp, giữ draft và xử lý session-expired đúng.

### H4. UX thống nhất và refactor

Onboarding: giờ rảnh → nhập việc → xem lịch khả thi → thực hiện phiên đầu. Note/chat/task đi đến một activity/provenance rõ. Dashboard có hành động tiếp theo; thông báo draft/preview/applied/failed nhất quán.

Giữ NORA là điểm nhấn nhưng không dùng chuyển động nền lấn nội dung. Tập trung readability, contrast, focus, bàn phím, screen-reader semantics và reduced-motion. Kiểm tra tối thiểu 320/390/768/1024/1440px, zoom và nội dung dài.

Tách component/hook/query/command theo feature khi sửa. Giảm global state/controller nhiều trách nhiệm có lộ trình, giữ hợp đồng và behavior. Không rewrite toàn frontend chỉ vì file dài.

Acceptance: người mới làm được luồng cơ bản; Enter/IME/voice/SSE không regress; lỗi mạng không mất draft; không cố giữ loading để khoe animation; không tuyên bố đạt WCAG khi chưa audit.

## 12. Giai đoạn I — Pet, XP, level, streak và inventory

Xây pet engine thật; mascot UI hiện có chưa phải hệ thống kinh tế.

### I1. XP ledger

Server quyết định thưởng; ledger bất biến có nguồn, rule version, idempotency và các dòng adjustment. Có trần/policy với việc ngắn, timer bỏ quên, actual chỉnh tay, mở lại task và undo.

Acceptance: cùng nguồn chỉ thưởng một lần dù retry/hai tab; không farm bằng complete/reopen/complete; không nhận XP từ tổng client gửi; đối chiếu được balance từ ledger.

### I2. Pet progression

PetDefinition/evolution/UserPet hoặc tương đương. Các mốc hình thái: 1, 5, 10, 15, 20, 50, 100. XP curve cấu hình/version, không hardcode rải rác. Tách level/evolution khỏi skin.

Asset evolution phải thực sự hiển thị khác và có version/fallback/provenance; không chỉ đổi số level rồi ghi hoàn thiện. Animation mượt, có pause/reduced-motion, không cản phiên tập trung.

### I3. Streak

Gắn với thực hiện mục tiêu hợp lệ, không chỉ mở app. Server xác định ngày theo timezone/policy; ngày nghỉ hợp lệ không phá chuỗi. Có quy tắc ghi nhận muộn và đổi timezone; không dùng pet bệnh/chết để gây áp lực khi nghỉ.

Acceptance: sửa clock client, replay hoặc duplicate event không tăng chuỗi; người dùng hiểu vì sao ngày đạt/chưa đạt.

### I4. Inventory và skin

Skin catalog/version, quyền sở hữu, equip/preview và source grant rõ. Quyền phía server, không tin localStorage. Skin trả tiền không tăng XP hay thành tích.

Acceptance: không equip skin chưa sở hữu; không đọc/sửa inventory người khác; asset retire hoặc mất file có fallback; shop hiển thị đúng owned/equipped/purchasable.

## 13. Giai đoạn J — Shop, thanh toán và admin tài chính

### J1. Order và checkout

Ưu tiên mua skin trực tiếp. Chưa tự xây ví/số dư trung gian nếu không có yêu cầu riêng. Chọn provider theo thị trường/tài khoản merchant/tài liệu chính thức, không giả định đã có credential.

Server quyết định price/discount/currency; order items snapshot bất biến; payment attempts có idempotency. UI có pending/paid/failed/canceled/expired rõ và khôi phục trạng thái sau reload.

### J2. Webhook/IPN và entitlement

Xác minh signature trên payload đúng chuẩn, merchant, order, amount, currency và unique provider event/transaction. Xử lý duplicate/out-of-order/retry bằng transaction/inbox/outbox phù hợp; entitlement được cấp đúng một lần ở cấp nghiệp vụ.

Return URL không phải bằng chứng thanh toán. Có reconciliation job, refund/chargeback theo provider, điều chỉnh có audit và quyền lợi được xử lý theo policy rõ.

Acceptance bắt buộc:
- Callback giả/sai chữ ký/sai amount hoặc currency không cấp skin.
- Replay, hai webhook và mất mạng sau commit không cấp hai lần.
- Event cũ không làm trạng thái PAID lùi thành PENDING.
- Refund/reconcile có kiểm thử sandbox và không xóa lịch sử.
- Khi thiếu credential, test double chỉ ở test; không giả live thành công.
- Live payment tắt cho đến khi có cấu hình và phê duyệt mở cụ thể.

### J3. Admin theo quyền

Các nhóm quyền: user/support, AI operations, content/skin, commerce/finance và audit. Search có pagination/filter/username phù hợp; thao tác nhạy cảm có reason, actor, target, timestamp và correlation ID. Giữ chống tự khóa và revoke sessions.

Support không tùy ý sửa giá/hoàn tiền; finance không mặc nhiên đọc note/chat; không tạo generic editor sửa trực tiếp tiền/XP. Cấp quyền mới phải có policy tương thích role hiện tại, không tự mở rộng đặc quyền âm thầm.

### J4. Số liệu tài chính

Tách order value, payment confirmed, refund, provider fees, settlement và AI/infrastructure cost. Filter khoảng ngày/timezone/currency; không cộng thẳng các currency. Ước tính khác số tiền đã xác nhận.

Acceptance: số liệu truy được nguồn giao dịch; export theo quyền; discrepancy được hiển thị/đối soát, không tự sửa mất dấu vết.

## 14. Giai đoạn K — Bảo mật, dữ liệu và vận hành

Rà soát ownership, auth/session, CSRF/CSP, rate limits và secret handling. Bổ sung admin step-up/MFA/recovery, per-device session revoke, export/delete/retention theo policy phù hợp. Recovery code/token/credentials được bảo vệ và chống reuse đúng.

Data retention/xóa nội dung không phá idempotency, audit hoặc ledger cần giữ theo policy; không tự đặt quy định pháp lý chưa xác minh. Token calendar/payment/AI chỉ phía server; có rotation runbook, không xuất vào artifact.

Có liveness/readiness không gọi model tốn phí; structured logs/correlation, tracing/metrics phù hợp, cảnh báo API/DB/AI/worker/outbox và runbook. Thử outage, bounded retry, cleanup PROCESSING/lease và nhiều instance.

Chuẩn bị staging tách production, versioned artifact, secrets theo môi trường, migration job quyền riêng, TLS/trusted proxies và rollout/rollback. Backup phải thử restore vào database riêng, kiểm tra toàn vẹn và đo RPO/RTO thực tế.

Acceptance: không công bố production-ready/SLA hoặc capacity N user nếu chưa kiểm chứng; restore/rollback có evidence; health/log/admin không lộ dữ liệu nhạy cảm.

## 15. Kiểm thử và CI — bao gồm F16

Sửa test selector mơ hồ theo vùng/hành vi, đặc biệt cùng khung giờ xuất hiện ở message và plan panel. Không dùng bỏ assertion/skip để làm xanh; không lấy rerun pass thay cho báo cáo lần đầu fail.

Bộ kiểm chứng cần có theo thay đổi:
- Unit: interval/capacity/dependency/risk, timer state, reward/streak và order state.
- HTTP integration: auth/ownership, revision, apply/undo, import, conversations/quota.
- SQL thực: migrations/backfill, constraints, concurrency, reservation, worker claim và payment/reward dedupe.
- Browser: onboarding/task/time/calendar/NORA/notes/insights, mobile, bàn phím, IME, focus, CSP, reduced-motion, offline/timeout và SSE stop/retry.
- Provider: live AI/voice có hạn mức; calendar/payment sandbox thật khi có cấu hình. Không gửi audio riêng tư hoặc giao dịch thật chỉ để test.
- Performance: baseline/sau sửa cho query, payload, static caching, worker và AI latency.

Chuyển browser checks cần thiết từ artifacts sang cấu trúc test/CI được quản lý. Giữ locked restore, migration consistency và dependency audit. Test không chạy/skipped/blocked phải ghi rõ, không coi như đã đạt.

## 16. Dữ liệu, API và migration

Tái sử dụng entity/contract đã có; chỉ thêm hoặc tách khi có nhu cầu. Nhóm mới có thể gồm Conversation/Message, AiUsage/Quota, RecoveryProposal, note revisions/import batches, event series/occurrences, notification delivery, XpLedger/UserPet/Streak/Inventory, Order/PaymentAttempt/ProviderEvent/Entitlement/Refund và audit.

Tên có thể theo convention repo, không bắt buộc tạo mỗi tên thành một bảng. Mọi thay đổi cần validation, ownership, indexes/unique keys thích hợp, pagination và error contract. Migrations theo giai đoạn, có backfill/version tương thích; thử database mới lẫn bản sao schema cũ.

Không thêm endpoint UI không gọi hoặc UI gọi endpoint placeholder. Hoàn thiện cả luồng thành công, lỗi, trạng thái rỗng, retry và giới hạn.

## 17. Quyền thực hiện và phụ thuộc bên ngoài

Được thực hiện sửa code, viết migration, build/test, tạo artifact/tài liệu và kiểm tra local/sandbox phù hợp phạm vi. Không hỏi lại xác nhận cho các bước reversible đã được yêu cầu.

Khi thiếu merchant, OAuth credential, nguồn lễ đáng tin, push/email provider, staging hoặc policy quan trọng: hoàn thành phần độc lập trước, nêu chính xác thiếu gì và hỏi ngắn gọn khi cần. Tiếp tục các ticket không phụ thuộc, không bỏ toàn bộ công việc.

Không tự deploy production, thay IAM/org policy, gửi thông báo/email cho người thật hoặc giao dịch/refund thật. Chuẩn bị kết quả cụ thể có thể review; xin phê duyệt riêng ở bước tác động bên ngoài nếu chưa được cho phép.

Không hardcode success, dùng credential giả hoặc tắt tính năng rồi ghi DONE. Phân biệt IMPLEMENTED_PENDING_EXTERNAL_VERIFICATION và BLOCKED_EXTERNAL với hoàn thành.

## 18. Definition of Done và bàn giao

Một hạng mục DONE cần:
1. Hành vi/API/UI được triển khai đầy đủ với validation, ownership và failure states.
2. Migration/backfill và tương thích được kiểm chứng nếu có đổi dữ liệu.
3. Kiểm thử phù hợp có kết quả mới, phân biệt mock/sandbox/live và skipped.
4. Không còn placeholder hoặc feature flag che phần nghiệp vụ chưa thực hiện.
5. Có tài liệu sử dụng/cấu hình/vận hành và evidence cần thiết.
6. Không làm suy giảm các tính năng được yêu cầu giữ.

Sau mỗi giai đoạn, cập nhật progress: trước/sau, file, tests, migration, bằng chứng, rủi ro và bước tiếp theo. Tiếp tục các giai đoạn còn khả thi, không tự dừng sau giai đoạn đầu vì phạm vi lớn. Nếu giới hạn phiên/ngữ cảnh ngắt, ghi checkpoint chính xác, không tuyên bố toàn bộ hoàn thành.

Bàn giao cuối:
- Ma trận F01–F17 và toàn bộ tính năng mở rộng với trạng thái thực.
- Danh sách thay đổi và cách kiểm tra trên ứng dụng.
- Kết quả build/backend/frontend/SQL/browser/AI eval; thất bại, skipped và phần chưa xác minh.
- Migration/backfill, cấu hình không bí mật, rollout/rollback và backup/restore.
- Rủi ro còn lại và thông tin/phê duyệt thật sự cần từ tôi.

Bắt đầu ngay bằng kiểm tra working tree, đọc tài liệu, tạo baseline/progress và sửa nhóm capacity → risk → dependency → execution. Sau đó tiếp tục theo thứ tự các phần trên.
