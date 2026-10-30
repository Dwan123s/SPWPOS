# Bằng chứng kiểm thử Note — 25/10/2026

- [Báo cáo phát hiện và ưu tiên](../../reviews/NOTE_FLOW_AUDIT_2026-10-25.md)
- [Luồng chi tiết](../../features/NOTE_FLOW_2026-10-25.md)
- [cases.csv](cases.csv): 208 bản ghi, gồm 188 regression, 15 diagnostic và 5 live-AI.
- [manifest.json](manifest.json): số lượng, thời điểm xuất, HEAD và 68 SHA-256 tệp nguồn/cấu hình. Bản audit có thay đổi chưa commit, không dùng riêng HEAD để nhận diện phiên bản.
- [backend-probes.json](backend-probes.json), [ui-probes.json](ui-probes.json): kết quả B01–B11/U01–U04.
- [live-ai.json](live-ai.json): đầu ra có cấu trúc 5 ca VertexAI dùng dữ liệu giả lập, không ghi database.
- [note-unit.trx](note-unit.trx), [note-integration.trx](note-integration.trx), [frontend.json](frontend.json): kết quả từng test có sẵn.
- Các tệp `.log`: kết quả regression, diagnostic, AI thật và build cuối cùng của từng bộ. Những lần sửa lỗi setup/harness trước đó không được tính thành bug sản phẩm.

## Cách đọc

Trong CSV, `execution=REPRODUCED` nghĩa là ca chẩn đoán đã tái hiện hành vi ghi trong JSON. Đọc thêm `assessment`: GAP/DESIGN_GAP/API_RISK/RECOVERY_RISK là hạn chế; PASS là kiểm tra bảo vệ đúng. Một characterization test chạy xanh có thể đang xác nhận **bug vẫn tồn tại**.

Regression xanh chỉ bảo đảm kỳ vọng hiện có trong phạm vi test. Không dùng tổng số test để kết luận mọi luồng production hoặc mọi đầu vào AI đều đúng. CSV có test rộng hơn Note và test tham số được tách thành nhiều bản ghi.

## Chạy lại tại workspace hiện tại

Các log/JSON đã được sao chép vào tài liệu này để không phụ thuộc tuổi thọ của thư mục `artifacts`. Harness bổ sung nằm tại `artifacts/note-audit-2026-10-25/` trên máy audit; thư mục này bị Git ignore. Mã harness cũng được lưu ở [probe-source](probe-source/) để xem và phục dựng. Các đường dẫn trong harness được thiết kế chạy khi đặt lại vào vị trí artifact gốc.

Các lệnh sau tính từ repository root, sau khi đã cài dependencies của dự án. Dùng .NET 8 theo `scripts/Initialize-Dotnet.ps1` và Node 24 trên máy audit. Chọn output riêng để không ghi đè API đang chạy.

```powershell
. ./scripts/Initialize-Dotnet.ps1
& $SpwposDotnet test tests/SPWPOS.UnitTests -c Release --no-restore -m:1 -nodeReuse:false -p:SkipFrontendBuild=true -p:UseSharedCompilation=false -p:OutputPath="$PWD/artifacts/note-audit-2026-10-25/build-unit/" --filter 'FullyQualifiedName~Note|FullyQualifiedName~ExecutionCorrectnessTests|FullyQualifiedName~TaskVersionTests|FullyQualifiedName~AgentGuardrailTests' --logger 'trx;LogFileName=note-unit.trx' --results-directory artifacts/note-audit-2026-10-25/unit

# Script chỉ chấp nhận SQL host cục bộ; factory tạo DB SPWPOS_Tests_<GUID>.
& ./artifacts/note-audit-2026-10-25/run-integration.ps1
& ./artifacts/note-audit-2026-10-25/run-probes.ps1

node frontend/node_modules/vitest/vitest.mjs run --config artifacts/note-audit-2026-10-25/vitest.probes.config.mjs
```

Frontend regression chạy từ `frontend/`:

```powershell
node node_modules/vitest/vitest.mjs run tests/notes.test.jsx tests/notes-autosave.test.jsx tests/note-activities.test.jsx tests/note-checklist.test.jsx tests/note-plan-preview.test.jsx tests/note-impact.test.jsx tests/note-appearance.test.js tests/workflows.test.jsx --reporter=default --reporter=json --outputFile=../artifacts/note-audit-2026-10-25/frontend.json --maxWorkers=2
node node_modules/vite/bin/vite.js build --outDir ../artifacts/note-audit-2026-10-25/build-frontend/app
```

AI thật chạy console probe với `--live` thay cho `--sql`, dùng cấu hình provider cục bộ. Không cần và không nên đưa secret vào báo cáo hay command line. Đầu ra gồm dữ liệu giả lập; mỗi ca có timeout và dừng sớm nếu provider lỗi. Không tự động gọi lại AI thật chỉ để làm đẹp số liệu.

Lưu ý: project probe tham chiếu TestSupport; compile/restore theo script probe trước khi chạy DLL với `--live`. SQL integration, loopback test host hoặc đọc user-secrets có thể cần quyền môi trường trên máy chạy lại. File output riêng không thay runtime đang phục vụ người dùng.
