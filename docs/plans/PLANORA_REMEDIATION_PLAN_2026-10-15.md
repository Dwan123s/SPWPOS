# PLANORA — Kế hoạch khắc phục toàn hệ thống

Ngày lập: 15/10/2026. Nguồn: `docs/reviews/SYSTEM_REVIEW_2026-10-15.md` và yêu cầu sản phẩm trong cuộc trao đổi. Đây là kế hoạch triển khai; chưa phải báo cáo các lỗi đã được sửa.

## 1. Mục tiêu và phạm vi

Hoàn thiện PLANORA theo thứ tự: tính đúng đắn → độ tin cậy AI/dữ liệu → phục hồi kế hoạch → trải nghiệm/hiệu năng → pet → thanh toán → vận hành.

Phạm vi bao gồm toàn bộ F01–F17 trong báo cáo và các phần mở rộng đã yêu cầu: lịch lễ Việt Nam, nhắc trước một ngày, chuỗi tiến trình, pet có XP và các mốc 1/5/10/15/20/50/100, skin, payment và admin tài chính. Calendar sync, MFA admin và privacy lifecycle là hạng mục nâng cấp riêng có điều kiện hạ tầng/tài khoản bên ngoài.

Không coi ý tưởng chưa triển khai là bug. Không coi một tính năng đã có là cần viết lại chỉ vì báo cáo chấm điểm thấp. Mọi phát hiện phải đối chiếu lại code hiện tại trước khi sửa: working tree có thể đã thay đổi sau ngày đánh giá.

## 2. Các nguyên tắc triển khai

- Giữ dữ liệu và thay đổi chưa commit; đọc AGENTS.md áp dụng trước khi làm. Không reset/revert hàng loạt.
- Giữ thương hiệu PLANORA, mascot NORA, Enter gửi/Shift+Enter xuống dòng, IME, SSE thật, voice preview, preview/apply/undo, quyền sở hữu và bảo vệ phiên khóa/đang chạy.
- Giữ kết nối Vertex AI hiện đang hoạt động, bao gồm API key nếu đó là cấu hình được người dùng chọn. Không tự đổi sang ADC, Gemini free hoặc thay credential để né lỗi.
- Planner phía server quyết định tính hợp lệ. AI chỉ hiểu/diễn đạt yêu cầu, không tự cấp quyền, thưởng, thanh toán hoặc bỏ hard constraints.
- Mỗi thay đổi hành vi có kiểm thử phù hợp; chỉnh CSS nhỏ chỉ cần build/kiểm tra trực quan thích hợp. Không tạo test chỉ sao chép implementation.
- Mỗi migration có hướng triển khai, dữ liệu backfill và cách phục hồi. Thử trên database riêng/bản sao, không tự apply vào dữ liệu đang phục vụ.
- Mỗi giai đoạn cung cấp chức năng dùng được, evidence và trạng thái. Không dùng mock/stub làm kết nối production rồi ghi hoàn thành.
- Hoàn thành các phần code/test/local/sandbox có thể làm trước; chỉ hỏi khi cần quyết định thật sự hoặc quyền truy cập ngoài. Vẫn tiếp tục các nhánh không phụ thuộc.
- Production deployment, đổi IAM/org policy, gửi email/push cho người thật, giao dịch/hoàn tiền thật là các bước riêng cần phạm vi và phê duyệt cụ thể. Các bước đọc, sửa code, migration test, synthetic test và sandbox thuộc công việc chuẩn bị.

## 3. Thứ tự giai đoạn

| Giai đoạn | Kết quả cần bàn giao | Phụ thuộc | Điều kiện qua |
|---|---|---|---|
| 0 | Baseline, bản đồ bằng chứng, backlog đang chạy | Không | Biết test hiện tại, ranh giới dữ liệu, phát hiện còn hiệu lực |
| 1 | Lịch và số liệu cùng một cách hiểu | 0 | Capacity/risk/dependency/execution có regression đúng |
| 2 | Nền tảng .NET LTS được kiểm chứng | 0; giữ tách change set với 1 | Build, migration, auth và SQL concurrency qua trên môi trường riêng |
| 3 | NORA/voice dùng được lâu và có quota | 1 | Hội thoại có vòng đời, context có phạm vi, chi phí có kiểm soát |
| 4 | Recovery, insights thực tế, notes sống | 1, 3 theo từng ticket | Trễ lịch có phương án; thống kê dùng actual; note không tạo trùng |
| 5 | Calendar/giờ rảnh/lịch lễ và kết nối ngoài | 1 | Chuỗi sự kiện đúng; lễ/reminder không trùng; sync không tự xung đột |
| 6 | UX, query, cache, cấu trúc và CI ổn định | 1–5 theo từng ticket | Hành vi giữ nguyên, đo được cải thiện, browser checks vào CI |
| 7 | Pet, XP, streak và inventory | 1, 4 | Thưởng server-side đúng một lần, ngày nghỉ có quy tắc |
| 8 | Shop, payment sandbox và admin tài chính | 3, 7 | Xác thực giao dịch, quyền lợi, refund, reconcile có kiểm chứng |
| 9 | Security/production readiness và phát hành | Từng module đã nghiệm thu | Restore/rollback/monitoring, migration và release gates được chứng minh |

Các phần CI, bảo mật, logging và đo lường bắt đầu từ giai đoạn 0, không chờ đến cuối. Có thể xen kẽ các ticket độc lập nhưng không trộn một đợt rewrite framework + toàn frontend + payment.

.NET 8 có mốc hết hỗ trợ 10/11/2026 theo báo cáo đã đối chiếu Microsoft. Giai đoạn 2 cần bắt đầu sớm; xác minh lại chính sách hỗ trợ và breaking changes chính thức lúc triển khai. Không diễn giải thành .NET 8 đã hết hỗ trợ vào ngày lập kế hoạch.

## 4. Backlog triển khai và acceptance

### Giai đoạn 0 — Xác định trạng thái thật

**B00.1 — Baseline và traceability**

Đọc báo cáo, chạy probes F01/F02/F03 nếu còn phù hợp; kiểm tra source thay vì chỉ tin ảnh/log cũ. Ghi commit/working-tree fingerprint phù hợp, cấu hình không bí mật, baseline tests và blockers vào `docs/plans/PLANORA_FIX_PROGRESS.md`.

Acceptance: mỗi F01–F17 có trạng thái còn lỗi/đã sửa/chưa xác nhận, file liên quan, reproduction và ticket. Không in secret hoặc đưa dữ liệu user thật vào artifact.

**B00.2 — Quality gates hiện tại**

Chạy build frontend/backend và nhóm test thích hợp theo hướng dẫn repo, sau đó toàn bộ suite trước release. SQL concurrency/migration cần database riêng. Kiểm tra test F16 không ổn định; lưu cả kết quả batch và chạy lại, không biến rerun pass thành lần đầu đã pass.

Acceptance: lỗi baseline tách khỏi lỗi mới; nếu chưa có SQL test thì ghi chưa kiểm chứng, không tính skipped là passed. CI audit hiện có được giữ.

### Giai đoạn 1 — Tính đúng đắn nghiệp vụ

**B01.1 — Capacity dùng chung (F01)**

Định nghĩa các trường calendarFreeMinutes, dailyLimitRemaining, effectiveCapacity, reservedMinutes và schedulableRemaining hoặc tên tương đương. Dùng cùng timezone, khoảng thời gian, factor, daily cap và completed work. Frontend không tự dựng công thức khác backend.

Acceptance: 600 phút rảnh × 0,8, daily limit 60 không hiện 480 phút khả dụng để xếp; Hôm nay/Tiến độ/Xem trước cùng giá trị khi cùng phạm vi. Xử lý hết giờ, không có giờ rảnh, qua nửa đêm và ngày đã làm một phần.

**B01.2 — Risk và feasibility dùng chung (F02)**

Task risk dùng giới hạn ngày/phiên, khoảng liên tục, nghỉ và dependency thích hợp. Giải thích thiếu bao nhiêu phút và nguyên nhân; không phát minh xác suất.

Acceptance: task 120 phút, giới hạn 60, deadline trong ngày không được báo Low trong khi planner thiếu 60 phút. Risk và planner có regression đối chiếu.

**B01.3 — Container/milestone dependency (F03)**

Chốt semantics: container hoàn thành khi các con hoàn thành; predicted finish bằng thời điểm muộn nhất của các con. Nếu cần duyệt riêng, dùng milestone riêng. Kiểm tra closure khi reschedule một con ảnh hưởng downstream phụ thuộc cha.

Acceptance: A là con P, B phụ thuộc P, đủ giờ thì B nằm sau A; thiếu một con vẫn chặn đúng; chu kỳ qua cha/con bị chặn; không double-count workload; các phiên bảo vệ được giữ.

**B01.4 — Execution policy và timer (F04)**

Validator bắt đầu/tiếp tục có chính sách rõ cho giờ hiện tại, fixed events, thay đổi availability, dependency, deadline và overrun. Actual execution khác planned interval. Không âm thầm xóa thời gian thực đã ghi nhận.

Acceptance: pause 14–15h, resume 16h30 trong sự kiện 16–17h cho kết quả đúng policy và thông báo rõ. Một phiên active/account; multi-tab, retry complete, reload, timer bỏ quên và chỉnh actual có kiểm chứng.

**B01.5 — Policy AI và planner (F07)**

Giữ chính xác thời lượng khi user yêu cầu rõ; cho dùng estimate cá nhân hóa khi phù hợp và hiển thị nguồn. Đồng bộ hard constraints, giải thích capacity policy; không âm thầm đổi settings để vừa yêu cầu.

Acceptance: “làm liên tục 120 phút” không tự biến thành 150 hoặc bị chia nhỏ khi chưa đồng ý; lời đề nghị thay đổi giới hạn xuất hiện nếu cần; cùng policy/context cho kết quả tương đương qua chat/form.

### Giai đoạn 2 — Framework LTS

**B02.1 — Nâng .NET/ASP.NET/EF/Identity lên bản LTS được hỗ trợ (F17)**

Theo yêu cầu người dùng ngày 15/10/2026, giữ .NET 8; dùng ASP.NET Core/EF Core/Identity tương thích .NET 8. Cập nhật target, lockfiles, scripts SDK, CI, publish/runtime và tài liệu nhất quán; không đổi database schema chỉ vì đổi package. Không tự nâng lên .NET 10.

Acceptance: locked restore, Release build, frontend publish, auth/OTP/refresh/revocation/Google login, SSE, voice, planner và SQL migration/concurrency đạt trên môi trường riêng. Có upgrade/rollback runbook, không thay bản đang chạy trước khi bản mới được kiểm chứng.

### Giai đoạn 3 — NORA, AI usage và voice

**B03.1 — Conversation lifecycle (F05)**

Conversation entity/list, cursor pagination, messages theo conversation, server search, rename/archive/delete/export. Backfill lịch sử hiện có, ownership và timezone chính xác. Quota/retention có đường phục hồi thay trần 2000 vĩnh viễn; tách idempotency tombstone khỏi nội dung bị xóa.

Acceptance: hơn 100 lượt vẫn đọc/tìm được; archive/delete không truy cập chéo user; replay request không tạo task trùng kể cả khi chat đã bị archive/xóa theo policy.

**B03.2 — Context theo mục đích và clarification schema (F06)**

Context có count/range/truncated; query đọc dữ liệu theo yêu cầu cụ thể và khả năng nạp thêm. Options trong câu hỏi là dữ liệu có cấu trúc, không phụ thuộc regex văn bản. Giữ parser/schema bounded và không phát private reasoning.

Acceptance: task ngoài 50 dòng đầu vẫn tìm đúng; tên trùng phải hỏi; không trả lời “tất cả” bằng tập con mà không thông báo; input chứa prompt injection không đổi policy/ownership. Read-only không tạo mutation.

**B03.3 — Quota/cost/concurrency (F10)**

IP limiter chống abuse cùng limiter account sau auth; quota ledger có reservation/reconciliation để call đồng thời không vượt ngân sách. Lưu usage provider khi có, model/version, latency và cost ước tính theo bảng giá cấu hình có version; không lưu prompt nhạy cảm vào log. Retry bounded, jitter, deadline và circuit breaker nếu cần dựa trên số đo; giữ request ID.

Acceptance: chạm budget bị chặn trước call; nhiều tab không lách; cùng Wi-Fi không dùng chung quota account; thiếu usage metadata hiển thị unknown/ước tính rõ, không bịa token; retry 429/5xx không lặp mutation. Provider khác chỉ dùng khi đã cấu hình và policy cho phép, không tự chuyển qua free key.

**B03.4 — Voice chính xác và tiết kiệm (F11)**

Đo audio seconds processed, số preview và latency. Adaptive preview/VAD hoặc pacing phù hợp; giữ final authoritative và cancel. Không cắt chunk tùy tiện làm hỏng container; dùng STT streaming riêng khi có lý do đo được.

Acceptance: chữ xuất hiện trong ô chat khi nói; không lặp/nhảy mất draft; final/cancel không bị response muộn ghi đè; không tự gửi; release micro; 60 giây/2 MiB và browser hỗ trợ được kiểm tra. Kiểm tra 6h/16h, ngày giờ tiếng Việt, silence, tiếng ồn và tên riêng bằng audio synthetic/test phù hợp.

**B03.5 — Eval và quan sát AI**

Bộ câu version hóa về tiếng Việt, giờ mơ hồ, deadline, event, dependency, history, stale/cancel/retry, prompt injection. Offline/test-double chạy trong CI; provider eval có bật tường minh, hạn mức và báo kết quả riêng.

Acceptance: phân biệt JSON hợp lệ với action đúng; có thống kê clarification, task/date accuracy, false mutation, first-token/final latency; không dùng mock để khẳng định AI thật đã đạt.

### Giai đoạn 4 — Phục hồi, thống kê và notes

**B04.1 — Recovery Mode (F08)**

Mặc định “trễ hai ngày” là hai ngày theo timezone có phiên dự kiến mà không thực hiện; phân biệt với deadline quá hạn hai ngày. Làm policy cấu hình được. Tạo proposal có affected tasks/dependencies, diff, revision, trigger và dedupe. Default đề xuất, auto-apply chỉ khi user bật; giữ locked/running/paused.

Acceptance: deadline còn hiệu lực thì tìm phương án trước hạn; deadline cứng đã qua thì hỏi gia hạn/giảm phạm vi/hủy; thiếu thời gian thì báo thiếu. Worker chạy lại hoặc hai instance không apply hai lần; user đổi lịch giữa chừng thì proposal stale. Quiet hours và opt-out được tôn trọng.

**B04.2 — Actual analytics (F09)**

API 3/7/30 ngày và khoảng tùy chọn hợp lý. Tách planned occupancy, focus segments và task completion. Phân bổ segment qua nửa đêm/timezone; task completion theo CompletedAt. Task list có pagination/filter; bỏ giới hạn 8 dòng mang nghĩa toàn bộ.

Acceptance: làm hôm sau không bị ghi thành làm hôm trước chỉ vì scheduled end; pending/open segment, duration correction, insufficient samples và kỳ so sánh được diễn giải rõ. Dashboard/insights dùng cùng read model.

**B04.3 — Living notes, version và import diff (F15)**

Cho sửa note sau import; lưu document revisions/import batches/task provenance. Re-analysis tạo diff thêm/cập nhật/đã tạo; người dùng xác nhận. Giữ note autosave/concurrency và không tự sửa task đang thực hiện từ text mới.

Acceptance: phân tích lại không nhân đôi task, dữ liệu gợi ý 60 phút/7 ngày có nhãn, lỗi AI không mất note, concurrent edit có giải quyết rõ; note deletion không lén xóa task đã tạo.

### Giai đoạn 5 — Calendar và ngày lễ

**B05.1 — Availability và event series**

Editor tuần trực quan có form accessible, preset, nhập qua nửa đêm được chuẩn hóa thành các slot đúng ngày. Series có occurrence/exception và các phạm vi “lần này/từ lần này/cả chuỗi”. Định nghĩa buffer/overlap policy và thông báo diff khi dời phiên.

Acceptance: timezone/DST được kiểm tra, không duplicate series, edit một occurrence không phá các lần khác, phiên khóa/đang chạy không bị tự dời; điều kiện tuần tối đa không bị lách qua client.

**B05.2 — Calendar lễ VN và nhắc hôm trước**

Dataset có nguồn/năm/version/timezone, tách ngày lễ/ngày kỷ niệm/lịch nghỉ thực tế; xác minh nguồn trước khi đưa lịch âm/nghỉ bù vào. Default chỉ đánh dấu/nhắc, user tự chọn bận hay rảnh. Người dùng có sự kiện riêng và chọn nhắc trước 24h hoặc hôm trước vào giờ cụ thể.

Acceptance: không dùng AI bịa lịch lễ, cập nhật năm không tạo trùng/ghi đè custom event; quiet hours, timezone, dedupe reminder, chạy bù sau outage đúng policy. Thiếu nguồn được ghi nhận, không seed dữ liệu giả là chính thức.

**B05.3 — Kết nối calendar ngoài**

Kết nối OAuth riêng với scope tối thiểu, revoke/reconnect; đọc busy trước, ghi phiên theo opt-in sau. Lưu external IDs/version/sync cursor, mã hóa credential server và queue/outbox.

Acceptance: login Google không bị coi là quyền calendar; event của PLANORA không nhập ngược tự xung đột; duplicate/out-of-order sync, expired token và chỉnh hai nơi có policy. Provider chưa cấp credential: hoàn thiện adapter/tests/connection UX và ghi rõ chưa live, không fake success.

### Giai đoạn 6 — UX, hiệu năng và bảo trì

**B06.1 — API/query/worker (F12)**

Endpoint/projection theo use case, date range/cursor, tránh full GetAsync cho query nhỏ. Learning/risk incremental hoặc background khi hợp lý; transaction bảo vệ ràng buộc vẫn đồng bộ. Worker batch/due-time, lease/claim và dedupe nhiều instance.

Acceptance: có query/payload/duration baseline và so sánh; lịch sử lớn không biến mất âm thầm do Take; cancellation/timeout và ownership được giữ. Không công bố tải N user khi chưa load test.

**B06.2 — Cache/static assets (F13)**

API dữ liệu riêng giữ no-store; HTML revalidate; hash assets immutable. Compression theo hạ tầng, WOFF2 có tiếng Việt, sprite tối ưu alpha/chất lượng, route styles khi có ích.

Acceptance: header thực tế đúng từng loại resource, bản deploy mới không mắc HTML/asset cũ, không cache dữ liệu chéo user, ảnh/frame đúng và pause/reduced-motion giữ nguyên. Đo network thay vì chỉ so kích thước build.

**B06.3 — Bootstrap, offline và UX thống nhất (F14)**

Phân biệt 401, offline, timeout và 5xx; retry không làm mất draft; landing chỉ cho đúng ngữ cảnh public. Onboarding đến được lịch/phiên đầu tiên; rõ trạng thái draft/preview/applied/failed. Shared tokens/forms/dialogs, bàn phím, focus, contrast, 320px trở lên và giảm chuyển động.

Acceptance: API 500 khi mở app hiển thị retry phù hợp; không giả đăng xuất; Enter/Shift+Enter/IME, voice và NORA compact thinking không regress. Không ép delay loading chỉ để chạy mascot.

**B06.4 — Refactor theo module**

Tách trách nhiệm query/command khỏi WorkspaceService khi sửa từng feature; giảm controller global và tách component/hook frontend. Giữ một app triển khai; tránh rewrite hoặc thêm framework/queue/vector DB không có lý do.

Acceptance: public contracts và dữ liệu tương thích/backfill rõ; behavior tests giữ; không đổi hàng loạt ngoài phạm vi để làm đẹp cấu trúc.

**B06.5 — CI/E2E ổn định (F16)**

Scope selector theo vùng/hành vi, sửa ca trùng giờ; đưa browser smoke, CSP, auth refresh, mobile, SSE stop/retry và giảm chuyển động vào repo/CI. Audit/migration/SQL concurrency gates hiện có được giữ.

Acceptance: batch lặp ở cấu hình CI ổn định, không skip/xóa assertions để xanh; từng lỗi test được phân loại; video/screenshot chỉ khi cần chẩn đoán, không thay assertion nghiệp vụ.

### Giai đoạn 7 — Pet, XP, streak, skin inventory

**B07.1 — Reward rules và XP ledger**

Version hóa luật thưởng, nguồn task/session hợp lệ, idempotency key, giới hạn ngày và adjustment ledger. Tách thưởng hoàn thành outcome, giữ lịch và tập trung. Quy tắc đối với actual chỉnh tay, task mở lại và hoàn tác rõ ràng.

Acceptance: replay/2 tab/complete lại không thưởng trùng; chia task siêu ngắn và để timer chạy không farm vô hạn; frontend không gửi tổng XP đáng tin; số dư có thể đối chiếu bằng ledger.

**B07.2 — NORA pet progression**

PetDefinition, evolution, UserPet; các mốc 1/5/10/15/20/50/100, XP curve cấu hình có version. Skin là lớp riêng. Tái sử dụng mascot hiện có đúng vai trò; thêm asset thật có provenance nếu cần, không đưa placeholder vào production.

Acceptance: lên level xác định ở server, refresh không mất, asset/version tương thích, animation không cản focus và có reduced-motion. Nếu chưa có asset evolution, không gắn nhãn hình thái đã hoàn chỉnh chỉ bằng thay số level.

**B07.3 — Streak và lịch nghỉ**

Daily commitment theo timezone, nghỉ hợp lệ, xử lý ghi nhận muộn, thay timezone và reset/khôi phục có policy. Streak gắn thực hiện mục tiêu chứ không chỉ mở app.

Acceptance: user nghỉ theo lịch không mất chuỗi; đổi clock client không tăng streak; duplicate event không đếm thêm ngày; giải thích được vì sao ngày đạt/chưa đạt.

**B07.4 — Inventory và shop preview**

Skin catalog/version, owned/equipped, ownership server, preview, load asset có fallback rõ. Cơ chế grant qua reward tách grant qua order; chưa cấp skin trả tiền chỉ vì client click.

Acceptance: equip skin chưa sở hữu bị chặn; invalid/retired product có policy; user khác không đọc/sửa inventory; pet không có lợi thế thành tích nhờ skin mua tiền.

### Giai đoạn 8 — Commerce và admin

**B08.1 — Order và payment adapter**

Bắt đầu mua skin trực tiếp; chưa xây ví nạp số dư trừ khi có yêu cầu riêng. Giá/discount/currency từ server, order items snapshot bất biến, payment attempts và idempotency. Chọn một provider thực dựa trên tài khoản merchant/nhu cầu và tài liệu chính thức; không mặc định có tài khoản nào.

Acceptance: client sửa giá không ảnh hưởng order, duplicate create/retry có hành vi rõ, trạng thái pending/failed/canceled/paid đầy đủ, checkout sandbox thật khi có credential. Adapter test double chỉ dùng test và có nhãn.

**B08.2 — Webhook/IPN, entitlement và reconciliation**

Xác minh signature/raw payload, merchant, order, amount, currency và unique provider event/transaction; inbox/outbox hoặc transaction phù hợp. Cấp entitlement đúng một lần ở cấp nghiệp vụ. Refund/chargeback/settlement theo tài liệu provider, bảo toàn audit.

Acceptance: callback giả, sai amount, replay, out-of-order, mất mạng sau commit, provider retry và worker chạy hai lần không cấp trùng. Return URL không tự biến order thành PAID. Có đối soát pending và thử refund sandbox; live payment tắt trước khi người dùng phê duyệt mở.

**B08.3 — Admin theo quyền**

User/support, AI operations, commerce/finance, content/skin, audit. User search có cursor/tổng số phù hợp và username; job logs lọc theo thời gian/kết quả. Thêm quyền read/action, lý do thao tác và correlation ID; giữ khóa self-protection và revoke sessions.

Acceptance: support không đổi giá/hoàn tiền tùy ý, finance không mặc nhiên đọc note/chat, refund/grant/manual adjustment có reason/audit. Không cung cấp generic editor sửa trực tiếp tiền/XP.

**B08.4 — Dashboard tài chính**

Phân biệt order value, confirmed payment, refund, provider fees, settlement và cost AI/hạ tầng. Multi-currency không cộng thẳng; chưa có dữ liệu thì hiển thị chưa xác định, không biến ước tính thành số tiền đã thu.

Acceptance: số liệu trace về giao dịch, export có quyền, filter khoảng ngày/timezone/currency rõ, reconcile mismatch hiển thị để xử lý và không bị tự sửa mất dấu vết.

### Giai đoạn 9 — Security và vận hành

**B09.1 — Quyền truy cập, admin MFA và privacy lifecycle**

Rà soát auth/session/CSRF/CSP/ownership, per-device revoke, step-up/MFA admin và recovery, account export/delete/retention theo policy cần chốt. Bảo vệ token OAuth/calendar, AI key, payment secret; secret rotation có runbook.

Acceptance: cross-user, stale token, revoked session, role escalation, recovery-code replay và admin action có tests. Retention/xóa nội dung không làm hỏng idempotency/ledger hoặc xóa lịch sử kinh doanh trái policy. Quyền mới không tự mở rộng cho role cũ không chủ đích.

**B09.2 — Health, observability và jobs bền vững**

Liveness/readiness không lộ secret; structured logs/correlation/tracing phù hợp, metric API/DB/AI/worker/outbox, alert và runbook. Lease/dedupe worker nhiều instance và bounded retries; nguyên nhân AI bị từ chối được diễn giải an toàn.

Acceptance: mô phỏng DB/AI/SMTP/provider outage có trạng thái/alert phù hợp; task/request đang xử lý không bị treo mãi; health không gọi model tốn phí mỗi lần probe.

**B09.3 — Backup/restore và deployment**

Staging tách production, artifact có version, secrets theo môi trường, migration job quyền riêng, backup/restore thực nghiệm và rollback. Cấu hình reverse proxy, trusted proxies, TLS, security headers và rate limiter thực tế.

Acceptance: restore vào database riêng thành công, kiểm tra tính toàn vẹn, đo RPO/RTO thực nghiệm; rollback app/schema có giới hạn được ghi rõ. Không tự nhận SLA hoặc “sẵn sàng production” chỉ vì dev build pass.

## 5. Bản đồ phủ hết phát hiện

| Phát hiện | Ticket chính | Kết quả nghiệm thu |
|---|---|---|
| F01 capacity dashboard | B01.1 | Cùng phạm vi cho cùng số liệu |
| F02 task risk | B01.2 | Feasibility và risk không mâu thuẫn |
| F03 parent dependency | B01.3 | Completion time container truyền sang downstream |
| F04 resume | B01.4 | Policy tiếp tục phiên được thực thi/test |
| F05 chat limit | B03.1 | Phân trang/lifecycle, không chặn vĩnh viễn |
| F06 context giới hạn | B03.2 | Query đúng phạm vi, báo truncation |
| F07 policy AI/planner | B01.5 | Giữ thời lượng rõ nguồn và cùng hard constraints |
| F08 lỡ phiên hai ngày | B04.1 | Recovery idempotent, có opt-in và revision |
| F09 thống kê lịch sử | B04.2 | Actual/CompletedAt và filter ngày đúng |
| F10 chi phí AI | B03.3 | Quota/account, usage và concurrency |
| F11 voice cumulative | B03.4 | Đo và giảm xử lý lặp, giữ final/cancel |
| F12 query/worker | B06.1 | Projection/range/batch có số đo |
| F13 asset cache | B06.2 | Headers đúng, không cache private data |
| F14 bootstrap catch | B06.3 | Phân biệt auth/offline/server failure |
| F15 note sau import | B04.3 | Sửa note, import diff không trùng |
| F16 test không ổn định | B06.5 | Selector theo vùng và CI browser ổn định |
| F17 framework lifecycle | B02.1 | LTS hỗ trợ, regression/rollback verified |

Các nâng cấp bổ sung có ticket B03.5, B05.*, B06.4, B07.*, B08.*, B09.*; không bị bỏ qua chỉ vì không có mã F trong báo cáo.

## 6. Definition of Done

Một ticket chỉ `DONE` khi:

1. Hành vi được triển khai đầy đủ qua API/UI nếu tính năng yêu cầu cả hai, với validation, ownership và failure states.
2. Database/migration/backfill tương thích và đã thử trên môi trường riêng nếu có đổi schema.
3. Test thích hợp chạy và có kết quả mới; mock/live, passed/skipped/blocked được phân biệt.
4. Hành vi trước đó được giữ hoặc thay đổi có chủ đích và tài liệu hóa.
5. Có evidence và tài liệu sử dụng/vận hành nếu cần.
6. Không còn placeholder, TODO hoặc feature flag được dùng để che phần nghiệp vụ chưa làm.
7. Nếu phụ thuộc credential/merchant/dataset/production approval còn thiếu: `IMPLEMENTED_PENDING_EXTERNAL_VERIFICATION` hoặc `BLOCKED_EXTERNAL`, không ghi `DONE` cho kết nối thật.

Không lấy số lượng test làm thước đo duy nhất. Không hứa điểm 9/10, không lỗ hổng hoặc tải vô hạn khi chưa đo.

## 7. Theo dõi tiến độ và checkpoint

File tiến độ được tạo khi **bắt đầu triển khai**, không tự ghi các ticket đã hoàn thành ngay từ tài liệu kế hoạch. Trạng thái: `NOT_STARTED`, `IN_PROGRESS`, `DONE`, `IMPLEMENTED_PENDING_EXTERNAL_VERIFICATION`, `BLOCKED_EXTERNAL`.

Mỗi checkpoint ghi: ticket, hành vi trước/sau, file, migration, tests/commands đã chạy, evidence, rủi ro còn lại và ticket tiếp theo. Bảng tổng có toàn bộ F01–F17 và tính năng mở rộng. Nếu giới hạn phiên/ngữ cảnh ngắt công việc, ghi exact next steps để tiếp tục; không thay trạng thái incomplete thành complete.

Dừng để chốt quyết định chỉ khi có tác động thật: chọn provider chưa có thông tin, policy retention không thể suy ra, thay đổi dữ liệu không phục hồi, credentials hoặc mở production. Chuẩn bị lựa chọn cụ thể và kết quả có thể review; tiếp tục phần độc lập. Không hỏi lại việc sửa code hoặc chạy kiểm thử đã nằm trong phạm vi.

## 8. Cách sử dụng prompt

Prompt đầy đủ ở `docs/prompts/PLANORA_FULL_REMEDIATION_PROMPT.md`. Giao prompt trong task có repo, để agent đọc cả kế hoạch này và báo cáo nguồn. Prompt bắt đầu bằng baseline rồi triển khai; không yêu cầu người dùng dán API key vào chat.

Thứ tự ưu tiên giữ nguyên khi phải chia nhiều lượt: B01 correctness và B02 LTS → B03 AI reliability → B04 recovery/insights → B05/B06 experience/performance → B07/B08 economy → B09 release gates, trong khi security và CI chạy xuyên suốt.
