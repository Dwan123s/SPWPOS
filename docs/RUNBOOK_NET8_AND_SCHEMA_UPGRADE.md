# Chuyển PLANORA sang .NET 8 và schema mới

Trạng thái 15/10/2026: source/build/tests đã xác minh .NET 8. Kiểm tra read-only thấy database cấu hình hiện tại đã khớp migration set; lượt triển khai này không apply migration, không xác nhận ai đã apply trước đó. Chưa diễn tập backup/restore hoặc rollout production. Đọc `PLANORA_FIX_PROGRESS.md` để biết checkpoint và lỗi chưa kiểm chứng mới nhất. Không dùng kết quả cũ để kết luận source hiện tại đã đạt.

## Thay đổi cần biết

- Theo yêu cầu người dùng ngày 15/10/2026, SDK pin 8.0.425; ASP.NET Core/EF Core/Identity 8.0.31 và target net8.0. Lockfiles được sinh lại. Giữ migration lịch sử; ProductVersion tại thời điểm sinh migration không bắt buộc bằng runtime hiện tại.
- PKCS được pin 8.0.2 để vá GHSA-846f-f764-76mx và GHSA-2fmv-rv2c-59x7; audit vẫn bật, không suppress NU1902/NU1903/NU1904.
- Test provider dùng EF Core 8; bỏ DbContext/options cũ trước khi thay SQL Server bằng SQLite. Không dùng interface chỉ có ở EF mới hơn.
- ApiFactory dùng cấu hình test in-memory, không đọc secret hoặc watcher của máy. Database SQL thử nghiệm chỉ được tạo/xóa trong namespace `SPWPOS_Tests_` sinh riêng.
- SDK initializer đặt DOTNET_ROOT, DOTNET_ROOT_X64 và PATH nhất quán. Giữ user-secrets store hiện có; không chạy rotate key khi chỉ nâng runtime.

## Schema đang chuẩn bị

1. `20261015094847_AddAgentConversationLifecycle`: thêm request fingerprint, bảng/index conversation; backfill từng cặp owner/conversation từ lịch sử. Không xóa AgentTurns.
2. `20261015095749_AddAiUsageReservations`: ledger giữ chỗ/quota và metadata usage; không lưu prompt/audio hoặc giá tiền giả định.
3. `20261015100639_AddRecoveryProposals`: cài đặt và đề xuất phục hồi, revision, dedupe và liên kết proposal.

Các bảng mới không thay credential hoặc tự đổi trạng thái payment. Chưa có schema commerce/pet trong change set này.

## Gate trước khi phát hành

Chạy trong checkout chứa source đã review, giữ connection thử nghiệm riêng trong biến môi trường của phiên hiện tại; không ghi connection string vào log/tệp báo cáo.

```powershell
. .\scripts\Initialize-Dotnet.ps1
& $SpwposDotnet tool restore
& $SpwposDotnet restore SPWPOS.sln --locked-mode
.\scripts\Test-BeforeMerge.ps1 -RequireSqlServer
```

Script test có quyền tạo/xóa database thử nghiệm sinh ngẫu nhiên, không dùng database người dùng làm test fixture. Nếu SQL không được cấu hình, dừng gate; không tính skipped là pass.

Sau đó kiểm tra thêm các luồng mới bằng trình duyệt: hội thoại cũ hơn 100 lượt, search/rename/archive/delete/export, Enter/IME/voice/stream/Stop/retry, preview/apply/undo, phân trang thống kê 3/7/30 ngày, Recovery mặc định chỉ đề xuất, stale/rebuild và quiet hours. Kiểm tra mobile, bàn phím và reduced motion. Mock browser không thay thế live AI eval.

AI eval bật tường minh với hạn mức riêng, giữ Vertex API key đang cấu hình. Kết quả HTTP dùng test double không chứng minh provider live đã hoạt động trên runtime mới.

## Chuẩn bị artifact và migration để review

```powershell
.\scripts\Build-Frontend.ps1 -Install -Test
& $SpwposDotnet publish src/SPWPOS.Api -c Release --no-restore -p:SkipFrontendBuild=true -o artifacts/release-net8
& $SpwposDotnet ef migrations script --idempotent --project src/SPWPOS.Infrastructure --configuration Release --output artifacts/upgrade-net8.sql
```

Chỉ sinh artifact/script, không tự apply. Kiểm tra log không có secret. Lưu manifest phiên bản/source, hash artifact, số migration trước/sau, test reports và config không bí mật. Bản đang chạy cần được giữ riêng khỏi output sẽ ghi đè.

## Backup và thử phục hồi

- Operator tạo backup COPY_ONLY có checksum ở vị trí SQL Server được phép ghi. Xác định đường dẫn ở phía SQL Server/container, không nhầm với thư mục trên máy chạy API.
- Thử RESTORE vào một database mới có tên rõ ràng, giữ app database nguyên vẹn; kiểm tra checksum, số lượng/ownership task, sessions, execution segments, note, conversation và FK/index.
- Chạy upgrade trên bản phục hồi rồi kiểm tra backfill, truy vấn nghiệp vụ và duplicate/replay. Ghi thời điểm backup, thời gian restore/upgrade và dữ liệu có thể mất; chưa đo thì không công bố RPO/RTO.

Chưa thực hiện diễn tập backup/restore này. Test SQL migration trên database tổng hợp là bằng chứng khác.

## Chuyển bản đang phục vụ — cần phê duyệt riêng

Chỉ thực hiện sau khi gate đạt và bản backup/restore đã được kiểm chứng. Dừng ghi và dừng đúng API của checkout; không chạy song song bản cũ và bản mới trong lúc backfill conversation. Review rồi apply script migration bằng tài khoản triển khai được giới hạn quyền. Chạy kiểm tra schema bằng bản mới trước khi nhận traffic:

```powershell
Push-Location src/SPWPOS.Api
& $SpwposDotnet bin/Release/net8.0/SPWPOS.Api.dll --check-schema
Pop-Location
```

`--check-schema` chỉ đọc migration metadata, không apply. Restart script kiểm tra bước này trước khi dừng API cũ. Start-Development nhận diện cả API net8.0 trong checkout để tránh khởi động/migrate chồng lên bản cũ; script đó vẫn có bước database update khi operator chủ động dùng nó.

Smoke test auth/OTP/refresh/revocation, lịch, NORA, SSE, voice và ownership bằng dữ liệu tổng hợp. Chỉ bật ghi khi checks đạt. Không tự gửi OTP/email cho người thật trong smoke test của agent.

## Phục hồi khi lỗi

- Trước apply schema: giữ bản cũ chạy, sửa artifact hoặc hủy phát hành.
- Sau apply schema nhưng trước mở ghi: có thể chuyển về backup đã thử phục hồi theo quyết định operator.
- Sau mở ghi: dừng ghi và đánh giá dữ liệu mới trước rollback. Không tự chạy migration Down; thao tác đó làm mất title/archive/settings/usage/recovery mới.
- Bản cũ có thể ghi AgentTurn mà không tạo conversation. Vì vậy không bật lại AI writes của bản cũ rồi kỳ vọng conversation mới đầy đủ nếu chưa có quy trình sửa/backfill được review.
- Xóa chat là thao tác privacy có chủ đích; phục hồi backup có thể đưa lại nội dung đã xóa. Cần xử lý theo nhật ký yêu cầu xóa và policy trước khi mở lại dữ liệu.

Giữ log/audit và failure code, không ghi raw provider response/API key. Không tuyên bố production-ready khi backup/restore, live AI và browser gate vẫn chưa được kiểm chứng.
