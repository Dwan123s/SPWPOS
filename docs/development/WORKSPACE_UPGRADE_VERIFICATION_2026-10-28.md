# Kiểm chứng nâng cấp PLANORA phases 1–4

Ngày 28/10/2026, tại working tree hiện tại. Dùng .NET SDK 8.0.425 và Node 24 đi kèm môi trường phát triển. Các chỉnh sửa Notes có sẵn được giữ lại trong quá trình kiểm tra.

| Kiểm tra | Kết quả cuối |
| --- | --- |
| Release build toàn solution | Thành công, 0 warning, 0 error |
| Unit tests | **635/635**, không fail/skip |
| API/integration tests, bật SQL Server thật | **149/149**, không fail/skip |
| Frontend Vitest | **360/360**, 41 file |
| Browser session refresh tests | **4/4** |
| Frontend standalone production build | Thành công; index, assets mascot và brand có trong output |
| API publish độc lập | Thành công; có API assembly, không có `wwwroot/app` hoặc `wwwroot/index.html` |
| EF migrations has-pending-model-changes | Không có thay đổi model chưa có migration |
| git diff --check | Thành công |

Tổng cộng 1.148 test cases trong các suite trên. Số này không phải tỷ lệ coverage hay chứng nhận chất lượng model AI thực tế.

## Lệnh và bằng chứng

Build:

```powershell
dotnet build SPWPOS.sln -c Release --no-restore -m:1 -p:SkipFrontendBuild=true
```

Trong môi trường này testhost của `dotnet test` bị timeout ở sandbox, nên chạy các assembly bằng runner xUnit v3 có sẵn trong chính test project. Không thay thế assertions hoặc mô phỏng lại service:

```powershell
dotnet tests/SPWPOS.UnitTests/bin/Release/net8.0/SPWPOS.UnitTests.dll -noLogo -maxThreads 2 -trx TestResults/upgrade-final-unit.trx
dotnet tests/SPWPOS.IntegrationTests/bin/Release/net8.0/SPWPOS.IntegrationTests.dll -noLogo -trx TestResults/upgrade-final-api-sql.trx
```

Lần chạy unit cuối bật `SPWPOS_NORA_SANDBOX_OPTIONS` ở chế độ **Offline**, với A01, A04, A07, B01, S04, H01, H02, H03, H09, H10. Cả 10 tình huống đạt, 11 lần gọi interpreter giả lập trên giới hạn 30; không gọi Gemini hoặc provider thật. Vì đã bật conductor offline nên test opt-in không bị skip.

Integration tests dùng `SPWPOS_TEST_SQLSERVER` trỏ vào container SQL Server tạm với mật khẩu sinh ngẫu nhiên. Mỗi ApiFactory SQL tạo database có tên `SPWPOS_Tests_<guid>` và áp migration thật. Container tạm đã được dọn; không dùng database phát triển đang chạy. Kiểm tra cả CAS version, lần ghi đầu tiên đồng thời, apply proposal/recovery, execution uniqueness và migration. Hai test static-file dùng fixture riêng, có assertion kiểm tra đúng fixture; backend CI không cần React build sẵn.

Frontend chạy với Node đúng engine, thay vì Node 20 mặc định trên máy:

```powershell
node frontend/node_modules/vitest/vitest.mjs run --root frontend --pool=threads --maxWorkers=2 --testTimeout=15000 --reporter=dot --reporter=junit --outputFile=../TestResults/upgrade-final-frontend.xml
node --test tests/browser/session-refresh.test.mjs
$env:PLANORA_STANDALONE = "true"
node frontend/node_modules/vite/bin/vite.js build frontend --outDir ../artifacts/upgrade-frontend-dist
```

Một số lần chạy đầu bị timeout với ngân sách mặc định 5 giây khi máy chạy nhiều suite cùng lúc. Lần cuối dùng 15 giây/test, giữ nguyên assertions và không có test bị skip. Console vẫn có cảnh báo React về key/lifecycle đã tồn tại trong các màn hình cũ; build thành công không có nghĩa đã xử lý hết các cảnh báo đó.

Bằng chứng cục bộ, không commit các output sinh ra:

- `TestResults/upgrade-final-unit.trx`
- `TestResults/upgrade-final-api-sql.trx`
- `TestResults/upgrade-final-frontend.xml`
- `artifacts/upgrade-build.log`
- `artifacts/upgrade-final-unit.log`
- `artifacts/upgrade-api-sql.log`
- `artifacts/upgrade-final-frontend.log`
- `artifacts/upgrade-session-refresh.log`
- `artifacts/upgrade-offline-smoke/report.md`
- `artifacts/upgrade-api-publish/` và `artifacts/upgrade-frontend-dist/`

## Thay đổi hợp đồng được kiểm tra lại

- Admin bỏ các thống kê công việc theo từng user khỏi JSON. Test cũ kiểm tra số task cá nhân được cập nhật để xác nhận các trường này không xuất hiện.
- Capacity đổi từ tỷ lệ tổng sang EMA từng ngày. Test vẫn bảo đảm actual vượt giờ không làm tăng capacity, nhưng kỳ vọng số theo công thức mới.
- Concurrent workspace writes có thể trả `409 CONCURRENCY_CONFLICT`; test kiểm tra code và việc retry sau đó không tạo thêm execution/recovery.
- Oracle NORA bỏ qua duy nhất `MutationVersion` nội bộ. Test mới bảo đảm thay đổi `Revision` và thuộc tính lịch thực vẫn bị phát hiện; không bỏ qua thay đổi nghiệp vụ để làm test đạt.

## Phạm vi chưa triển khai ra bên ngoài

Chưa áp migration lên database đang dùng, chưa deploy, chưa chạy GitHub Actions trên remote và chưa push Git trong đợt nâng cấp này. Chưa kiểm chứng live Gemini/SMTP hoặc hạ tầng reverse proxy production. Health AI/SMTP mô tả dữ liệu quan sát gần nhất, không giả vờ là phép đo kết nối hiện tại. `.ics` là export một chiều, không phải đồng bộ calendar hai chiều.

Xem [tính năng và cơ sở thuật toán](../features/WORKSPACE_UPGRADE_PHASES_1_TO_4.md) và [hướng dẫn build riêng, chung origin](INDEPENDENT_BUILD_SAME_ORIGIN.md).
