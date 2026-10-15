# PLANORA — Tiến độ triển khai

Bắt đầu: 15/10/2026. Phạm vi: toàn bộ yêu cầu đã gửi, theo báo cáo SYSTEM_REVIEW_2026-10-15 và backlog PLANORA_REMEDIATION_PLAN_2026-10-15.

## Nguyên tắc trạng thái

DONE chỉ khi đạt acceptance với kết quả kiểm chứng mới. IMPLEMENTED_PENDING_EXTERNAL_VERIFICATION khác BLOCKED_EXTERNAL; không coi skipped/mock là xác minh live. Dữ liệu người dùng và các thay đổi có sẵn trong working tree được giữ.

## Baseline

- Không tìm thấy AGENTS.md trong repository hoặc các thư mục cha đã kiểm tra.
- Working tree có nhiều thay đổi từ trước; không reset/revert.
- .NET 8 / EF Core 8 hiện tại; frontend React/Vite/Tailwind.
- Chưa sửa credential Vertex AI hoặc dữ liệu đang phục vụ.
- Baseline: .NET 8 Release build 0 warnings/errors; 209 unit backend và 207 frontend đạt. Evidence: artifacts/remediation/baseline-*.

## Backlog

| Ticket | Phạm vi | Trạng thái | Bằng chứng / còn thiếu |
|---|---|---|---|
| B00.1 | Baseline và traceability | DONE | Working tree/hash/baseline ghi trong artifacts/remediation |
| B00.2 | Quality gates hiện tại | IN_PROGRESS | .NET 8: 231 unit + 111 integration pass; frontend 215 pass tại checkpoint 4; còn E2E toàn phạm vi |
| B01.1 | Capacity dùng chung (F01) | IN_PROGRESS | Xem checkpoint bên dưới |
| B01.2 | Risk và feasibility dùng chung (F02) | IN_PROGRESS | Xem checkpoint bên dưới |
| B01.3 | Container/milestone dependency (F03) | IN_PROGRESS | Xem checkpoint bên dưới |
| B01.4 | Execution policy và timer (F04) | IN_PROGRESS | Xem checkpoint bên dưới |
| B01.5 | Policy AI và planner (F07) | IN_PROGRESS | Xem checkpoint 2 |
| B02.1 | Đồng bộ .NET/ASP.NET/EF/Identity (F17) | IN_PROGRESS | Người dùng yêu cầu giữ .NET 8; SDK 8.0.425, EF/ASP.NET 8.0.31; build/test đạt, còn rollout/restore drill |
| B03.1 | Conversation lifecycle (F05) | IN_PROGRESS | API/UI/migration và 223 unit pass trước quota; còn upgrade/backfill/browser gates |
| B03.2 | Context theo mục đích và clarification schema (F06) | IN_PROGRESS | Xem checkpoint 2 |
| B03.3 | Quota/cost/concurrency (F10) | IN_PROGRESS | Xem checkpoint 2 |
| B03.4 | Voice chính xác và tiết kiệm (F11) | NOT_STARTED | Chưa kiểm chứng |
| B03.5 | Eval và quan sát AI | NOT_STARTED | Chưa kiểm chứng |
| B04.1 | Recovery Mode (F08) | IN_PROGRESS | Recovery regression và SQL concurrency đạt; xem checkpoint 4–5, còn toàn bộ acceptance/worker batching |
| B04.2 | Actual analytics (F09) | IN_PROGRESS | Xem checkpoint 2 |
| B04.3 | Living notes, version và import diff (F15) | NOT_STARTED | Chưa kiểm chứng |
| B05.1 | Availability và event series | NOT_STARTED | Chưa kiểm chứng |
| B05.2 | Calendar lễ VN và nhắc hôm trước | NOT_STARTED | Chưa kiểm chứng |
| B05.3 | Kết nối calendar ngoài | NOT_STARTED | Chưa kiểm chứng |
| B06.1 | API/query/worker (F12) | NOT_STARTED | Chưa kiểm chứng |
| B06.2 | Cache/static assets (F13) | IN_PROGRESS | Xem checkpoint bên dưới |
| B06.3 | Bootstrap, offline và UX thống nhất (F14) | IN_PROGRESS | Xem checkpoint bên dưới |
| B06.4 | Refactor theo module | NOT_STARTED | Chưa kiểm chứng |
| B06.5 | CI/E2E ổn định (F16) | IN_PROGRESS | Xem checkpoint bên dưới |
| B07.1 | Reward rules và XP ledger | NOT_STARTED | Chưa kiểm chứng |
| B07.2 | NORA pet progression | NOT_STARTED | Chưa kiểm chứng |
| B07.3 | Streak và lịch nghỉ | NOT_STARTED | Chưa kiểm chứng |
| B07.4 | Inventory và shop preview | NOT_STARTED | Chưa kiểm chứng |
| B08.1 | Order và payment adapter | IN_PROGRESS | Người dùng chọn payOS; chỉ mới chuẩn bị protocol/signature, chưa có order/checkout/merchant |
| B08.2 | Webhook/IPN, entitlement và reconciliation | IN_PROGRESS | Verifier/signature tests đạt; chưa có inbox/entitlement/reconciliation |
| B08.3 | Admin theo quyền | NOT_STARTED | Chưa kiểm chứng |
| B08.4 | Dashboard tài chính | NOT_STARTED | Chưa kiểm chứng |
| B09.1 | Quyền truy cập, admin MFA và privacy lifecycle | NOT_STARTED | Chưa kiểm chứng |
| B09.2 | Health, observability và jobs bền vững | NOT_STARTED | Chưa kiểm chứng |
| B09.3 | Backup/restore và deployment | NOT_STARTED | Chưa kiểm chứng |

## Ma trận phát hiện

| Phát hiện | Trạng thái | Ticket |
|---|---|---|
| F01 | IN_PROGRESS | Có thay đổi; đang kiểm chứng theo checkpoint |
| F02 | IN_PROGRESS | Có thay đổi; đang kiểm chứng theo checkpoint |
| F03 | IN_PROGRESS | Có thay đổi; đang kiểm chứng theo checkpoint |
| F04 | IN_PROGRESS | Có thay đổi; đang kiểm chứng theo checkpoint |
| F05 | IN_PROGRESS | Xem checkpoint 2 |
| F06 | IN_PROGRESS | Xem checkpoint 2 |
| F07 | IN_PROGRESS | Xem checkpoint 2 |
| F08 | IN_PROGRESS | Xem checkpoint 3 |
| F09 | IN_PROGRESS | Xem checkpoint 2 |
| F10 | IN_PROGRESS | Xem checkpoint 2 |
| F11 | NOT_STARTED | Đối chiếu backlog |
| F12 | NOT_STARTED | Đối chiếu backlog |
| F13 | IN_PROGRESS | Có thay đổi; đang kiểm chứng theo checkpoint |
| F14 | IN_PROGRESS | Có thay đổi; đang kiểm chứng theo checkpoint |
| F15 | NOT_STARTED | Đối chiếu backlog |
| F16 | IN_PROGRESS | Có thay đổi; đang kiểm chứng theo checkpoint |
| F17 | IN_PROGRESS | Có thay đổi; đang kiểm chứng theo checkpoint |

## Checkpoint

Đang đối chiếu và dựng baseline trước nhóm capacity/risk/dependency/execution.



### Checkpoint 1 — Lõi lịch và .NET 10

- Đã sửa capacity dùng ngân sách cả ngày, không áp factor lại vào phần ngày còn lại; thêm CapacityDayView/Days/RangeStart/RangeEnd cho frontend dùng dữ liệu server. Ngân sách sử dụng planned occupancy; actual focus sẽ được truy vấn riêng ở B04.2.
- Risk có kiểm tra feasibility cùng planner; dependency container có predicted completion/affected closure, được dùng lại khi apply/conflict detection. Container progress/completion được tổng hợp khi task thay đổi.
- Resume kiểm tra lịch/sự kiện/dependency; hết khung lịch cần kết thúc và xếp lại, actual cũ không bị cắt. Retry start/pause/resume/complete/skip không tạo segment mới. UI báo phiên quá giờ.
- Regression trước sửa: 5 test mới, 4 fail; sau sửa toàn bộ 218 unit .NET 8 đạt. Evidence: regressions-before.*, phase-a-unit.xml.
- Thay đổi có chủ đích trong integration: bài long timer giữ actual 25 giờ nhưng chặn resume đã quá hạn; replay start được acknowledge 200 và kiểm tra chỉ một execution/segment, thay contract cũ lần hai lỗi 400. Không bỏ kiểm tra actual/concurrency.
- SDK .NET 10.0.402 / runtime 10.0.13 được tải từ Microsoft và kiểm SHA-512, nằm artifacts/dotnet10. Đổi target/package/script/CI, thêm Install-Dotnet10.ps1.
- NuGet audit chặn System.Security.Cryptography.Pkcs 10.0.0 transitively; pin 10.0.13 đã vá, không tắt audit. Sources: https://builds.dotnet.microsoft.com/dotnet/release-metadata/10.0/releases.json và https://github.com/advisories/GHSA-846f-f764-76mx.
- EF mới cần xóa IDbContextOptionsConfiguration khi thay provider trong test; đã sửa test infrastructure theo API mới. .NET 10 build 0 warnings/errors; 218 unit đạt; EF model check không có pending changes. Chưa apply migration vào database người dùng.
- Frontend mới: 211 test đạt, production build đạt; nhóm bootstrap/session/NORA 38 đạt. Lỗi mạng/503 có retry riêng, không giả signed-out; selector khung giờ scope trong lịch sử chat.
- Static assets: hash bundles immutable, HTML revalidate, private API no-store, compression chỉ public files; đang chờ HTTP assertions.
- Lượt HTTP/SQL .NET 8 đã dừng khi bị thay thế bởi .NET 10, không có final summary và không được tính pass. Lượt HTTP .NET 10 chỉ ra một ca AgentApiTests chạy lâu; đang chẩn đoán bằng probe có timeout trước khi kết luận.
- API đang chạy vẫn là bản .NET 8 trước đó; chưa đổi credential hoặc cập nhật database thật. Frontend đã build lại, các trường capacity mới có fallback tương thích trong lúc chuyển backend.

### Checkpoint 2 — HTTP/SQL, lịch sử và NORA

- Chẩn đoán hang bằng dotnet-stack: test host bị kẹt ở configuration reload/disposal. ApiFactory nay xóa nguồn cấu hình mặc định và chỉ dùng cấu hình in-memory của test, tránh đọc secret/watcher trên máy. Không tắt worker hay bỏ test. ConfigureAwait(false) ở dispose giữ luồng cleanup bất đồng bộ.
- `net10-http-integration3.xml`: 98 pass, 0 fail/skip. `all-sql-integration.xml`: 108 pass, 0 fail/skip, có SQL Server thật với database sinh trong namespace SPWPOS_Tests_. Đây là evidence trước khi thêm quota ledger; cần chạy lại sau thay đổi tiếp theo.
- Thống kê mới `/api/intelligence/history`: khoảng 3/7/30 hoặc tùy chọn 1–90 ngày; query theo range/owner. Planned occupancy, focus từ segment đã đóng, open timer tạm tính và actual chỉnh tay tách riêng. Segment chia theo nửa đêm timezone; completion theo CompletedAt, không đếm container trùng việc con. Có kỳ trước và trạng thái thiếu mẫu. `/tasks` có tìm kiếm/phân trang thật, bỏ nghĩa toàn bộ từ 8 dòng đầu.
- UI AnalyticsHistory đã thay biểu đồ cũ gán actual theo scheduled end. Ba regression về timezone, open segment/owner và >8 dòng đã đạt trong `lifecycle-unit.xml` (toàn bộ 223 pass).
- NORA có AgentConversation (title/archive/delete/version), cursor cho conversation/turn, server search, export có cờ truncated nếu vượt 10.000 lượt. Bỏ trần 2000 lượt vĩnh viễn. Migration AddAgentConversationLifecycle backfill từ AgentTurns, chưa apply DB ứng dụng.
- Xóa chat xóa text/command/result/undo; giữ request fingerprint/tombstone để replay không lặp mutation. Task/event đã tạo không bị xóa theo. Test 121 lượt/36 hội thoại, cursor, ownership, archive/export/delete/replay và >2000 lượt đạt trong `lifecycle-unit.xml`.
- UI thư viện hội thoại đã được nối endpoint thật. Lượt component đầu `lifecycle-frontend.json` có 2 fail do selector chưa chờ server search và mock voice chưa hỗ trợ endpoint mới. Đã sửa và bổ sung kiểm tra response shape; không coi lượt đầu là pass. Đang chạy full suite mới.
- Chat/form nay có lựa chọn policy tương ứng: giữ duration/toàn bộ giờ rảnh hoặc dùng bias/capacity factor. NORA giữ mặc định explicit-duration như trước và giải thích trong thông tin composer. Fingerprint bao gồm policy, không cho reuse request ID sang policy khác.
- Context có phạm vi/count/truncated; ưu tiên task có tên/ID trong yêu cầu thay chỉ 50 task đầu. Giới hạn context hiện 100 task/event/session, history tối đa 8; giới hạn state tổng thể 5000 task/10000 session vẫn là phần cần cải thiện B06.1. Nếu truncate, cảnh báo được trả về UI. Clarification options đã chuyển sang schema, bỏ regex suy luận lựa chọn.
- Đang thêm AiUsageRecord/atomic reservation qua account lock cho chat/notes/capture/breakdown/voice, daily/concurrent quota theo account, metadata token/model version/latency, UI usage. Chưa có bảng giá/cost reservation/retry policy/eval live nên B03.3 chưa DONE.
- Không gọi provider AI thật, không gửi email/payment thật, không apply migration vào DB đang phục vụ. Các thay đổi API/schema mới chưa được đưa vào instance .NET 8 hiện chạy.

### Checkpoint 3 — quota đã kiểm chứng, Recovery và payOS đang làm

- `quota-unit.xml`: 225 pass, 0 fail/skip. `quota-sql-integration.xml`: 110 pass, 0 fail/skip. Có SQL concurrency cho quota 1 lượt (call thứ hai bị chặn trước provider, replay không debit lại), upgrade/backfill hội thoại và các luồng HTTP hiện có.
- Lượt full frontend `quota-frontend.json` có 6 fail. Nguyên nhân gồm test mock cũ coi GET usage như gửi chat và fixture thống kê thiếu shape. Đã sửa fixture/response validation; lượt targeted `lifecycle-ui-recheck.json` có 57 pass, 0 fail. Chưa có full frontend pass mới sau mọi thay đổi.
- Quota hiện có daily request limit và concurrent limit qua SQL account lock, usage token/model-version/latency, giữ debit khi failure. Cost/pricing/budget reservation/retry/circuit/eval vẫn chưa hoàn thành; UI không quy đổi tiền khi chưa biết giá. Migration AddAiUsageReservations đã sinh, chưa apply vào DB app.
- Recovery đã có settings riêng (mặc định 2 ngày dự kiến bị lỡ, không tính ngày nghỉ; AutoApply=false), dedupe case, revision/stale/rebuild, hard deadline cần quyết định, giữ protected sessions, quiet hours/catch-up và opt-out. UI có xem proposal/apply/rebuild/dismiss/pagination. Worker gọi scanner; chưa có worker batching/lease mới.
- Migration AddRecoveryProposals đã sinh. Recovery tests được thêm; lượt build ban đầu fail xUnit2031 tại Assert.Single(Where(...)), source đã đổi sang overload predicate. Chưa xác minh lại bằng build mới.
- Mutate không tăng planning revision nếu replay thực sự không thay đổi dữ liệu; cần chạy lại execution/SQL concurrency sau thay đổi này.
- Start-Development nhận diện cả build framework cũ và kiểm tra API trước build frontend để tránh migrate/ghi đè UI khi app cũ đang phục vụ. Thêm CLI `--check-schema` chỉ đọc; Restart kiểm schema trước khi dừng bản cũ. Các thay đổi cuối này chưa chạy kiểm thử.
- Thêm frontend regression Analytics/Recovery và cập nhật workflow fixtures; chưa chạy lượt mới. Thống kê lịch sử vẫn hiển thị khi risk endpoint lỗi.
- Người dùng đã chọn payOS, chưa có merchant. Đã đọc tài liệu chính thức và ghi `PAYOS_INTEGRATION_DECISIONS.md`; chỉ xác minh được endpoint production, chưa xác minh sandbox công khai. Không bịa sandbox URL.
- Thêm PayOsPaymentSignatures cho checkout/payment webhook, constant-time HMAC comparison, duplicate-field/size/type guards và signed receipt parsing. Chỉ là phần nền protocol; chưa nối endpoint checkout, chưa tạo order/inventory, chưa gọi payOS. Tests dùng dữ liệu tổng hợp đã viết nhưng chưa chạy. Không được ghi B08 DONE.
- `RUNBOOK_DOTNET10_AND_SCHEMA_UPGRADE.md` ghi gates, migration/backfill, chuẩn bị artifact, backup/restore và giới hạn rollback. Chưa có diễn tập restore thực.
- **Blocker thực thi mới:** bộ xét duyệt tự động từ chối chạy lại build do tài khoản chạm usage limit, thông báo thử lại lúc 18:44. Đây là review failure vì hết hạn mức, không phải kết luận thao tác không an toàn. Không chạy lại cùng thao tác qua đường khác để né xét duyệt. Các source edits/read-only/research độc lập tiếp tục được thực hiện; không ghi kết quả test chưa chạy là pass.
- Không đổi key, không apply migration DB đang phục vụ, không restart/deploy API mới, không gửi email/payment thật. Toàn bộ backlog vẫn còn nhiều ticket NOT_STARTED trong bảng; chưa hoàn tất toàn hệ thống.

#### Lượt tiếp theo

1. Khi xét duyệt khả dụng lại: locked restore/build; toàn bộ unit, frontend, SQL integration, model snapshot và package audit. Ưu tiên Recovery, no-op revision, startup/schema guard và signatures.
2. Sửa failures, browser QA mới; không thay instance đang phục vụ trước khi code/schema được kiểm chứng và phê duyệt bước dữ liệu thật.
3. Hoàn thiện B03 context/query/cost/eval và B04 living notes; sau đó availability/event series/holiday/sync, performance/CI, pet/XP/inventory, order/checkout/payOS/inbox/reconcile/admin, privacy/MFA/ops. Giữ acceptance trong backlog thay vì thu hẹp phạm vi thành chỉ phần đã làm.


### Checkpoint 4 — quality gates và UI sau quota/Recovery

- Blocker xét duyệt ở checkpoint 3 đã hết sau thời điểm reset; build được xét duyệt và chạy bình thường, không còn blocker đó.
- Trước khi đổi framework trở lại theo yêu cầu mới: Release build đạt, 231 unit và 111 HTTP/SQL integration đạt; toàn bộ frontend 215 pass (compatibility-frontend.json), isolated Vite build đạt. Đây là các kết quả tại thời điểm kiểm tra, không phải hoàn tất toàn bộ backlog.
- Browser QA trên host riêng dùng SQLite in-memory và dữ liệu tổng hợp: đăng nhập, Recovery đề xuất mặc định, lịch sử thống kê 3 ngày, thư viện hội thoại, Enter gửi và cập nhật quota. AI trong browser QA là test double; kiểm tra Vertex thật được thực hiện riêng bằng CLI. Ảnh tại artifacts/remediation/nora-qa-complete.jpg.
- API/UI dùng feature negotiation để giao diện mới không gọi endpoint lifecycle/history/recovery chưa có trên backend cũ. Các sửa thư viện hội thoại đồng thời trong working tree được giữ.
- Model/locked restore/audit đã kiểm tra; main DB được kiểm tra read-only và đã khớp migration set. Không có thao tác apply migration main DB do lượt thực hiện này.
- Vertex thật: check cấu trúc/meeting/report/note và SSE đạt; không lưu lịch. Lượt stream đầu thiếu Development environment thất bại, sau khi dùng đúng environment đã đạt. Không đổi credential.

### Checkpoint 5 — giữ .NET 8 theo yêu cầu người dùng

- Người dùng xác nhận “su dung .net8 di”. Yêu cầu này thay thế phần nâng framework lên .NET 10 trong prompt trước đó. Các nâng cấp nghiệp vụ/UI được giữ; không reset working tree.
- Target/AnalysisLevel trở về net8.0/8.0; global.json SDK 8.0.425; ASP.NET Core/Identity/EF packages và EF CLI 8.0.31; CI dùng SDK 8.0.x; lockfiles sinh lại và locked restore đạt. Installer/initializer/startup/AI diagnostics dùng đường dẫn .NET 8. README, backlog và prompt đã ghi yêu cầu giữ .NET 8.
- Sửa KnownNetworks, bỏ EF options interface chưa có ở EF 8, dùng Convert.ToHexString(...).ToLowerInvariant() thay API hex mới hơn. Build đầu đã phát hiện các incompatibility này; lượt build cuối đạt 0 warning/error.
- System.Security.Cryptography.Pkcs 10.0.13 vẫn là thư viện hỗ trợ net8.0 đã vá, không phải yêu cầu runtime .NET 10. Không hạ thư viện này về phiên bản có advisory. Package audit bao gồm transitive không báo dependency có lỗ hổng tại thời điểm kiểm tra.
- Evidence mới: net8-release-build.log (Release chuẩn), net8-unit.xml (231 pass), net8-integration.xml (111 pass, 0 skip, có SQL thử nghiệm sinh riêng), net8-locked-restore.log, net8-model-check.log (không pending model changes), net8-package-audit.json, net8-schema-readonly.log (schema khớp, không đổi dữ liệu), net8-cloud-script-tests.log (PowerShell 5.1 đạt).
- net8-live-stream.log: VertexAI thật 3 reply chunks, first delta 1589ms, hoàn tất 2536ms; schema/exact times đạt, không lưu lịch. Đây là một mẫu kiểm tra, không phải p95 hay SLA.
- Runbook hiện hành: RUNBOOK_NET8_AND_SCHEMA_UPGRADE.md; runbook .NET 10 đã đánh dấu lịch sử. Không xóa SDK/binary .NET 10 đã cài trên máy, không downgrade hoặc migrate database ứng dụng, không tự restart instance/worker.
- Phần còn lại của backlog chưa hoàn tất, gồm living notes, calendar/holiday, pet/XP/shop/order/payment/admin và các gate vận hành. payOS vẫn chỉ là nền signature, chưa có merchant/checkout thực.

### Checkpoint 6 — rà soát UI, lazy loading và chuyển trang

- Theo yêu cầu mới, tập trung sửa giao diện toàn workspace; giữ .NET 8. Báo cáo chi tiết UI_REVIEW_2026-10-15.md ghi phát hiện, phạm vi kiểm tra và giới hạn.
- Shell giữ DOM khi cập nhật cùng trang; route có native View Transition và WAAPI fallback 240ms, focus/scroll sau đổi trang, chặn callback cũ sau logout. Không chồng reveal từng card lên page transition; giữ pause/reduced-motion.
- 11 workspace route được tách chunk thật. Notes/NORA CSS lazy, intent prefetch bỏ qua Save-Data/2G, skeleton/loading/error retry. Tách pages và page-actions giữ helper controller; load admin sau mount. Initial entry JS 493550 → 351990 byte, CSS 323537 → 251738 byte; không phải benchmark network/FPS.
- Recovery/analytics chỉnh phân cấp, controls, form mobile, trạng thái tải/trống/lỗi và pagination chỉ khi cần. Tắt phục hồi buộc autoApply=false; mô tả ngưỡng ngày dùng cấu hình. Lịch có panel công việc và CSS đầy đủ. Sửa khoảng trống lớn ở tiêu đề mobile và skip-link làm đổi hash/route.
- Gate cuối: 224 frontend test pass (27 file), 4 session-refresh pass, Vite production build đạt, manifest có 11 lazy route. Evidence ui-review-verified-tests.log, ui-review-session-tests.log, ui-review-vite.log, ui-review-chunks.json. Các lượt timeout và node_modules bị npm ci thay giữa chừng được ghi trong báo cáo, không tính pass; lượt cuối chạy sau khi dependency ổn định, không nới timeout/bỏ assertion.
- Browser QA dùng host riêng SQLite in-memory, account/dữ liệu tổng hợp và AI test double. Đã kiểm tra desktop/mobile; ảnh ui-review-dashboard.jpg, ui-review-mobile.jpg. Không coi browser QA này là Vertex live, payment hoặc kiểm thử user thật.
- Đã copy artifact kiểm chứng vào static wwwroot/app và root index, giữ asset hash cũ cho tab đang mở, backup entry trước trong artifacts/remediation/ui-review-previous-entry. Không restart API, không apply schema, không đổi credential. Publish evidence ui-review-publish.json. Kiểm tra HTTP static của instance thực được ghi riêng nếu khả dụng.
- Phạm vi backlog backend/payment/pet/ops trước đó vẫn chưa hoàn tất; checkpoint UI không đánh dấu toàn hệ thống DONE.

- Gate bổ sung sau thay đổi agent.jsx đồng thời: 38 NORA/stream/voice/lazy tests đạt, giữ source mới; rebuild và xác minh HTTP static 200 khớp artifact cuối. QA host riêng được dọn, instance người dùng không bị dừng.
### Checkpoint 7 — chuẩn bị push và kiểm tra CI

- CI trước đó của commit 710d126 thất bại tại dependency vulnerability check; frontend và Windows scripts đạt.
- Cập nhật System.Security.Cryptography.Pkcs lên 8.0.2 để vá GHSA-846f-f764-76mx và GHSA-2fmv-rv2c-59x7, sinh lại lockfiles và bỏ suppress NU1902/NU1903/NU1904. Giữ target .NET 8. NuGet audit mới không báo package có lỗ hổng.
- Bổ sung CloudAiConfiguration.Tests.ps1 vào Windows CI; test sử dụng CLI giả, không cần credential Google Cloud.
- Build tại output thông thường bị khóa DLL bởi API đang chạy; dùng output kiểm tra riêng, không dừng instance người dùng. Kết quả GitHub Actions của commit được push là gate xác nhận tiếp theo.
