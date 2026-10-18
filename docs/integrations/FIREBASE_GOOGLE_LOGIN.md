# Đăng nhập Google qua Firebase

Luồng đã tích hợp: chọn Google ở trang login → Firebase mở cửa sổ chọn tài khoản → frontend gửi **Firebase ID token** qua HTTPS → backend dùng Firebase Admin SDK xác minh chữ ký, project, thời hạn, trạng thái thu hồi và nhà cung cấp Google → tạo phiên PLANORA bằng cookie HttpOnly. Công việc, ghi chú và lịch tiếp tục lưu trong database hiện tại.

## 1. Tạo project và bật Google

1. Mở [Firebase Console](https://console.firebase.google.com/), tạo hoặc chọn project.
2. Trong **Project settings → General → Your apps**, thêm ứng dụng Web. Lấy `projectId`, `apiKey`, `appId` trong `firebaseConfig`. Không cần chuyển hosting sang Firebase.
3. Vào **Authentication → Sign-in method**, bật **Google**, chọn email hỗ trợ và lưu.
4. Trong **Authentication → Settings → Authorized domains**, thêm `localhost`. Khi triển khai, thêm tên miền thực tế của ứng dụng. Mở ứng dụng bằng `https://localhost:7043`; tránh dùng địa chỉ IP thay cho hostname đã cho phép.

Firebase quản lý luồng Google; cấu hình này không dùng Google OAuth Client Secret trong code PLANORA.

## 2. Cấu hình Firebase Admin ở server

Trong **Project settings → Service accounts → Firebase Admin SDK**, tạo khóa riêng mới và tải file JSON. Lưu ngoài repository, ví dụ:

```text
C:\Users\Lenovo\.config\PLANORA\firebase-service-account.json
```

Đây là khóa riêng của server. Không đặt trong `frontend`, `wwwroot` hay commit vào Git. Trong môi trường triển khai có Application Default Credentials, có thể dùng danh tính của môi trường thay cho file JSON.

## 3. Cấu hình môi trường phát triển

Chạy trong PowerShell tại thư mục dự án. Thay ba giá trị mẫu bằng thông tin từ ứng dụng Web đã đăng ký trong Firebase:

```powershell
Set-ExecutionPolicy -Scope Process Bypass
. .\scripts\Initialize-Dotnet.ps1

& $SpwposDotnet user-secrets set "Firebase:ProjectId" "YOUR_PROJECT_ID" --project src/SPWPOS.Api
& $SpwposDotnet user-secrets set "Firebase:ApiKey" "YOUR_WEB_API_KEY" --project src/SPWPOS.Api
& $SpwposDotnet user-secrets set "Firebase:AppId" "YOUR_WEB_APP_ID" --project src/SPWPOS.Api
& $SpwposDotnet user-secrets set "Firebase:Enabled" "true" --project src/SPWPOS.Api
& $SpwposDotnet user-secrets set "Firebase:ServiceAccountPath" "C:\Users\Lenovo\.config\PLANORA\firebase-service-account.json" --project src/SPWPOS.Api

powershell.exe -NoProfile -ExecutionPolicy Bypass -File .\scripts\Start-Development.ps1
```

Nếu API đang chạy, dừng tiến trình đó trước khi khởi động lại. `Firebase:ServiceAccountPath` lưu đường dẫn khóa trong User Secrets, nên không cần đặt lại biến môi trường khi mở terminal mới. Thay đường dẫn mẫu bằng vị trí file JSON thực tế của bạn. File khóa phải thuộc **cùng project** với cấu hình Web. Không đặt `FIREBASE_AUTH_EMULATOR_HOST` cho luồng đăng nhập thật này.

Nếu không cấu hình `Firebase:ServiceAccountPath`, server tiếp tục dùng Application Default Credentials, bao gồm biến `GOOGLE_APPLICATION_CREDENTIALS` nếu được đặt trong terminal khởi chạy API. Đường dẫn và nội dung khóa không được trả về trong Web config.

`authDomain` được suy ra thành `YOUR_PROJECT_ID.firebaseapp.com`. Web config được lấy từ server lúc chạy nên không cần rebuild frontend khi thay project. Các giá trị Web config được Firebase thiết kế để dùng ở client; file khóa riêng của Firebase Admin luôn ở server.

## 4. Sử dụng

- **Tài khoản mới:** bấm **Đăng nhập với Google**, chọn tài khoản và vào workspace. PLANORA tạo hồ sơ, dữ liệu tùy chọn ban đầu và phiên đăng nhập.
- **Đã có tài khoản email/mật khẩu và đã xác minh email:** bấm Google với cùng Gmail. Hệ thống tự liên kết vào tài khoản hiện có, giữ nguyên mật khẩu, hồ sơ, công việc, ghi chú và lịch. Có thể đăng nhập bằng cả hai cách. Nếu chưa xác minh email của tài khoản cũ, hoàn tất bước xác minh trước.
- **Đặt mật khẩu cho tài khoản Google:** vào **Tài khoản & nhóm → Bảo mật → Thiết lập đăng nhập bằng mật khẩu**, gửi OTP về email, nhập OTP và mật khẩu mới. Sau khi thiết lập, đăng nhập lại bằng email/mật khẩu hoặc Google; dữ liệu vẫn thuộc cùng tài khoản. Tài khoản đã có mật khẩu dùng form đổi mật khẩu, có yêu cầu mật khẩu hiện tại.
- **Đăng xuất:** thu hồi phiên PLANORA như bình thường. Firebase Web SDK chỉ giữ phiên tạm trong bộ nhớ và xóa sau khi đổi token hoặc liên kết.

## 5. Khi chưa đăng nhập được

| Biểu hiện | Cách xử lý |
| --- | --- |
| Nút Google chưa khả dụng | Kiểm tra `Firebase:Enabled` và ba giá trị Web config; khởi động lại API. |
| Popup bị chặn | Cho phép popup trên địa chỉ ứng dụng rồi bấm lại. |
| Địa chỉ chưa được cho phép | Thêm hostname vào Authorized domains trong Firebase Authentication. |
| Google chưa được bật | Bật Google trong Sign-in method và chọn email hỗ trợ. |
| Firebase Admin không xác minh được | Kiểm tra `Firebase:ServiceAccountPath` (hoặc Application Default Credentials), quyền đọc file, project và quyền Firebase Authentication của service account; khởi động lại API. |
| Email đã có tài khoản nhưng chưa xác minh | Xác minh email của tài khoản cũ trước; Google cùng email sẽ tự liên kết sau khi đã xác minh. |
| Tài khoản tạm khóa hoặc vô hiệu hóa | Google cũng phải tuân theo trạng thái tài khoản trong PLANORA. |

Chưa thể kiểm tra đăng nhập Google thật nếu chưa cấu hình project và khóa server. Các bài kiểm tra tự động dùng danh tính giả ở ranh giới xác minh token, không dùng tài khoản Google của bạn.

Tài liệu chính thức: [Google sign-in với Firebase Web](https://firebase.google.com/docs/auth/web/google-signin), [xác minh ID token ở server](https://firebase.google.com/docs/auth/admin/verify-id-tokens), [thiết lập Firebase Admin SDK](https://firebase.google.com/docs/admin/setup).
