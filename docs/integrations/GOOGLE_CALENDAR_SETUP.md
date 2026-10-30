# Kết nối Google Calendar cho PLANORA

## Tính năng và phạm vi

- Một tài khoản Google cho mỗi người dùng PLANORA; ứng dụng tạo lịch phụ PLANORA.
- Tạo/sửa/xóa lịch có giờ trong PLANORA hoặc trong lịch PLANORA trên Google sẽ được đồng bộ hai chiều.
- Phiên làm việc được gắn với task gốc; xóa sự kiện không xóa task, không tự cộng tiến độ/XP. Phiên đã có ghi nhận thực hiện được bảo vệ trước thay đổi giờ/xóa từ Google và cần giữ PLANORA khi có xung đột.
- Lời nhắc mặc định trước 15 phút, có thể nhắc thêm đúng giờ. Cài đặt mới áp dụng cho sự kiện mới. Khi chỉ sửa tên/giờ, lời nhắc, mô tả và khách mời đang có trên Google được giữ.
- Chỉ xuất các lịch địa phương chưa kết thúc khi kết nối lần đầu. Sự kiện Google đã kết thúc không tự nhập thành lịch mới.
- Sự kiện cả ngày và các buổi của lịch lặp được nhập làm khoảng bận; chỉnh trực tiếp trên Google. Chuỗi và ngoại lệ được giữ bằng ID Google; không xuất ngược các buổi thành sự kiện độc lập. Cửa sổ nhập chuỗi tối thiểu 90 ngày và được mở rộng theo planning horizon.
- Không chuyển một phiên/task đã liên kết thành cả chuỗi trên Google. Nếu đã đổi cấu trúc, khôi phục sự kiện đơn hoặc tạo chuỗi Google mới riêng; giao diện báo xung đột để giữ mapping của task gốc.
- Chưa đọc các lịch Google khác hoặc lịch chính. Khi tạo bằng điện thoại, chọn **lịch PLANORA**.
- Frontend/API vẫn chung địa chỉ. Đăng nhập Firebase/Google hiện tại và quyền Calendar là hai kết nối riêng.

## 1. Tạo cấu hình trong Google Cloud

1. Mở [Google Cloud Console](https://console.cloud.google.com/), chọn project đang dùng hoặc tạo project cho PLANORA.
2. Vào **APIs & Services → Library**, tìm **Google Calendar API** và bấm **Enable**.
3. Vào **Google Auth Platform** (hoặc **OAuth consent screen** tùy giao diện), điền thông tin ứng dụng/địa chỉ hỗ trợ trong **Branding**.
4. Trong **Audience**, chọn loại phù hợp. Nếu dùng Gmail cá nhân, thường chọn **External**. Trong giai đoạn **Testing**, thêm chính Gmail dùng kiểm thử vào **Test users**.
5. Trong **Data Access**, thêm các quyền: `openid`, `https://www.googleapis.com/auth/userinfo.email`, `https://www.googleapis.com/auth/calendar.app.created`.
6. Trong **Clients**, tạo OAuth client loại **Web application**. Đây không phải API key, service account JSON hoặc OAuth Desktop app.
7. Trong **Authorized redirect URIs**, thêm chính xác URL local:

   ```text
   https://localhost:7043/api/integrations/google-calendar/callback
   ```

8. Lưu, tải JSON của OAuth client, hoặc giữ Client ID/Client Secret để nhập bằng script. Client Secret chỉ đưa vào backend.

Không cần thay đổi Firebase config hay bật CORS có credentials. Luồng này chuyển trang tới Google rồi quay lại callback trên backend.

Tài liệu chính thức: [OAuth web server](https://developers.google.com/identity/protocols/oauth2/web-server), [Calendar scopes](https://developers.google.com/workspace/calendar/api/auth).

## 2. Lưu cấu hình local

Mở PowerShell ở thư mục gốc dự án. Cách dễ nhất là nhập thông tin bằng lời nhắc:

```powershell
.\scripts\Configure-GoogleCalendar.ps1
```

Script hỏi Client ID và Client Secret; phần Secret được nhập ẩn. Cấu hình được lưu trong .NET development User Secrets, không ghi vào Git.

Hoặc chọn JSON đã tải từ Google:

```powershell
.\scripts\Configure-GoogleCalendar.ps1 -OAuthClientJson 'C:\duong-dan\oauth-client.json'
```

Script kiểm tra redirect URI trong JSON, không in secret và không tự kết nối tài khoản. Không cần gửi Client Secret vào cuộc chat.

Kho khóa mã hóa mặc định: `%LOCALAPPDATA%\PLANORA\CalendarKeys`. Giữ thư mục này bền vững qua các lần restart. Trên Windows, khóa được bảo vệ bằng DPAPI theo tài khoản chạy API. Đổi Windows user hoặc xóa kho khóa có thể yêu cầu người dùng kết nối Google lại.

## 3. Cập nhật database và khởi động lại

Migration `GoogleCalendarSync` chỉ thêm các bảng kết nối, OAuth state, liên kết sự kiện, hàng đợi và webhook; không xóa lịch hiện có.

Bản local trong workspace này đã áp dụng migration và cập nhật API ngày 30/10/2026. Sau khi chạy script cấu hình ở bước 2, chỉ cần khởi động lại bản đã build:

```powershell
.\scripts\Restart-PlanoraApi.ps1
```

Các bước còn lại trong mục này dành cho máy khác hoặc database chưa có migration.

Sau khi dừng API đang chạy của dự án, dùng luồng khởi động hiện có:

```powershell
.\scripts\Start-Development.ps1 -SkipCertificateTrust
```

Script chuẩn bị môi trường, áp dụng migration và chạy API. Nếu API vẫn chạy, script sẽ báo đang chạy và không áp dụng cập nhật; cần dừng đúng tiến trình PLANORA trước. Không cần xoay lại các khóa JWT/OTP.

Nếu môi trường đã được chuẩn bị và chỉ cần migrate thủ công:

```powershell
. .\scripts\Initialize-Development.ps1
& $SpwposDotnet ef database update --project src/SPWPOS.Infrastructure
```

Trong IDE, khởi động lại API bằng profile HTTPS. Sau đó mở `https://localhost:7043`, bấm Ctrl+F5.

## 4. Kết nối và thử hai chiều

1. Vào **Cài đặt → Kết nối → Google Calendar**.
2. Chọn số phút nhắc, bấm **Kết nối Google Calendar**, chọn Gmail đã thêm ở Test users và cấp quyền Calendar. Nếu Google hiển thị các ô quyền, cần chọn ô cho phép PLANORA tạo/quản lý lịch trước khi bấm Tiếp tục; bật API và thêm scope trong Cloud Console không tự cấp quyền thay người dùng.
3. Google đưa bạn về PLANORA. Chờ trạng thái **Đã đồng bộ**; lịch PLANORA sẽ xuất hiện trong tài khoản Google.
4. Tạo một cuộc hẹn sau khoảng một giờ trong PLANORA, xác nhận có trên Google.
5. Đổi giờ cuộc hẹn đó trong Google, chờ khoảng 60–90 giây khi chạy local. Trang lịch PLANORA tự lấy lại dữ liệu khi đang mở và không có form nhập dở.
6. Tạo một sự kiện mới bằng Google, chọn đúng lịch PLANORA, kiểm tra xuất hiện ở PLANORA.
7. Thử xóa sự kiện. Với WorkSession, task gốc phải còn và phiên bị hủy.
8. Nếu hai bên cùng sửa cùng giờ, vào Kết nối để chọn **Giữ PLANORA** hoặc **Giữ Google**. Một lựa chọn cũ sẽ không âm thầm ghi đè một bản Google mới hơn.

Thay đổi địa phương được ghi vào Outbox cùng transaction lưu lịch; mất mạng vẫn giữ việc chờ gửi. Trạng thái đã lưu trong PLANORA và đã đồng bộ Google là hai thời điểm khác nhau.

## 5. Bật thông báo trên điện thoại

1. Mở ứng dụng **Google Calendar**, đăng nhập đúng Gmail đã kết nối.
2. Bật hiển thị/đồng bộ lịch **PLANORA** trong danh sách lịch.
3. Trong cài đặt điện thoại, cho phép Google Calendar gửi thông báo; kiểm tra thêm cài đặt thông báo của lịch.
4. Thử tạo một buổi sau 10 phút và đặt nhắc trước 5 phút **trước khi tạo buổi đó**.
5. Chờ sự kiện đến Google, đóng website PLANORA và đợi lời nhắc trên điện thoại.

Lời nhắc của Calendar tính theo giờ bắt đầu và do Google/thiết bị hiển thị. Chế độ không làm phiền, quyền thông báo hoặc đồng bộ tắt có thể ngăn popup. Webhook chỉ báo dữ liệu thay đổi cho backend; không gửi một thông điệp tùy ý của NORA vào ứng dụng Google Calendar. [Reminders](https://developers.google.com/workspace/calendar/api/concepts/reminders), [cài đặt Android](https://support.google.com/calendar/answer/37242?co=GENIE.Platform%3DAndroid&hl=vi).

## 6. Webhook khi triển khai có domain

Localhost đã dùng được hai chiều bằng polling, mặc định mỗi 60 giây cộng jitter. Khi triển khai có domain, cấu hình thêm:

```text
GoogleCalendar__Enabled=true
GoogleCalendar__ClientId=<OAuth Web Client ID>
GoogleCalendar__ClientSecret=<lưu trong secret manager>
GoogleCalendar__RedirectUri=https://planora.example/api/integrations/google-calendar/callback
GoogleCalendar__WebhookUrl=https://planora.example/api/integrations/google-calendar/webhook
GoogleCalendar__KeyRingPath=/var/lib/planora/calendar-keys
GoogleCalendar__PollSeconds=60
```

Đổi `planora.example` thành domain thật. Đăng ký chính xác RedirectUri production trong OAuth client. Webhook phải là HTTPS công khai, không phải localhost hoặc chứng chỉ tự ký. Nếu có reverse proxy, cấu hình TrustedProxyAddresses của hệ thống để backend nhận đúng HTTPS. Không đổi CORS nếu vẫn cùng domain.

Trên Linux/container, mount kho khóa bền vững, giới hạn quyền chỉ cho tài khoản chạy API và bảo vệ volume/secret store; nhiều replica phải dùng chung kho khóa. Windows DPAPI phù hợp máy đơn; khi chạy nhiều máy cần thống nhất cơ chế bảo vệ khóa dùng chung trước khi triển khai.

Worker tự đăng ký/gia hạn watch; webhook xác minh channel, resource và token, sau đó lấy delta. Polling vẫn chạy để phục hồi callback bỏ lỡ. Sync token hết hiệu lực sẽ full re-sync và đối soát, không xóa task hoặc hàng đợi địa phương. [Push notifications](https://developers.google.com/workspace/calendar/api/guides/push), [incremental sync](https://developers.google.com/workspace/calendar/api/guides/sync).

## 7. Lỗi thường gặp

| Hiện tượng | Cách kiểm tra |
| --- | --- |
| Chưa cấu hình máy chủ | Chạy Configure-GoogleCalendar.ps1, restart đúng môi trường Development để đọc User Secrets. Production dùng secret manager/env. |
| `redirect_uri_mismatch` | URI trong Google Cloud phải trùng hoàn toàn scheme, host, port và path callback. |
| `access_denied` / app đang Testing | Thêm Gmail vào Test users, dùng đúng OAuth Web client và cấp quyền Calendar. |
| Google chưa cấp quyền Calendar | Kết nối lại và chọn ô quyền Calendar trên màn hình Google. Khi token chỉ có quyền đăng nhập/email hoặc chỉ đọc lịch, PLANORA báo lỗi riêng và không tạo kết nối thiếu quyền. |
| Kết nối lại sau vài ngày | App External ở Testing có refresh token thường hết hạn sau 7 ngày khi có quyền Calendar. Chuẩn bị publishing/verification phù hợp trước khi dùng production. |
| Chưa có lịch phụ sau kết nối | Chờ worker, xem trạng thái/lỗi ở Cài đặt. Nếu bước tạo lịch bị timeout, kiểm tra Google trước khi kết nối lại để tránh lịch phụ trùng. |
| Lịch PLANORA đã bị xóa trên Google | Cần phục hồi lịch đó trên Google nếu còn khả năng; không tự tạo lịch mới rồi phát lại toàn bộ dữ liệu. |
| Không thấy sự kiện từ Google | Kiểm tra sự kiện nằm trong lịch PLANORA, chưa kết thúc, tài khoản kết nối đúng và backend đang chạy. |
| Đã có sự kiện nhưng không có popup | Kiểm tra giờ nhắc của chính sự kiện, tài khoản/lịch đang đồng bộ, quyền thông báo và chế độ không làm phiền. |
| Worker ngừng sau khi đổi máy/xóa khóa | Khôi phục kho khóa đúng tài khoản hoặc kết nối Google lại. |
| Muốn đổi sang Gmail khác | Bản đầu tiên giữ ràng buộc với Gmail đã kết nối để bảo toàn mapping; chuyển tài khoản chưa được hỗ trợ tự động. |

Ngắt kết nối dừng đồng bộ, xóa refresh token khỏi PLANORA và cố gắng thu hồi quyền ở Google. Giữ các sự kiện đã có trên Google; nếu Google không phản hồi, có thể tự thu hồi quyền trong trang bảo mật tài khoản Google. Không tự xóa dữ liệu lịch.

## Kiểm chứng

Kiểm thử tự động dùng Google gateway giả lập và HTTP handler, không gọi tài khoản thật. Bao gồm cấp quyền một lần, mã hóa token, tạo/sửa/xóa hai chiều, giữ task, xử lý đồng thời, chống trùng sau timeout, phục hồi sync token, quyền sở hữu và giữ reminders khi patch.

Kết quả ngày 30/10/2026: 65 unit tests, 16 API tests và 75 frontend tests liên quan đạt; Release build không warning/error. Migration đã được kiểm tra không có model drift và áp dụng thành công trên database SPWPOS local. HTTP bản chạy thực trả 200 cho trang chính và mã giao diện Calendar, có 9 route Calendar, và trả 401 cho truy cập trạng thái chưa đăng nhập. Script cấu hình đã được kiểm tra cú pháp; chưa chạy với credentials thật.

OAuth thực và lời nhắc trên điện thoại chỉ được xác nhận sau khi cấu hình Client ID/Secret và hoàn thành thử nghiệm trên tài khoản/thiết bị thật. Tài liệu này không khẳng định Google Cloud hoặc điện thoại đã được cấu hình.
