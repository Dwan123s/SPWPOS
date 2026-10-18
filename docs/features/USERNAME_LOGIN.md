# Đăng nhập bằng tên đăng nhập hoặc email

Trang đăng ký có trường **Tên đăng nhập** bắt buộc. Tên cần 3–32 ký tự, bắt đầu bằng chữ hoặc số, chỉ dùng chữ không dấu, số, dấu chấm, gạch dưới và gạch ngang. Tên không phân biệt chữ hoa/thường và không được trùng tài khoản khác.

Sau khi xác minh email, người dùng đăng nhập bằng tên đã chọn hoặc địa chỉ email, cùng mật khẩu. Trang đăng nhập hiển thị **Tên đăng nhập hoặc tài khoản email**. Xác minh OTP và quên mật khẩu vẫn dùng email đã đăng ký.

Tên được lưu trong `AspNetUsers.UserName` và `NormalizedUserName`, sử dụng unique index có sẵn của ASP.NET Identity; không cần migration mới. Tài khoản cũ và tài khoản Google vẫn đăng nhập bằng email. Khi Google liên kết vào tài khoản đã đăng ký, tên đăng nhập được giữ nguyên.

Hai cách đăng nhập dùng cùng khóa tài khoản, giới hạn số lần yêu cầu và bộ đếm sai mật khẩu. Đổi giữa tên đăng nhập và email không bỏ qua khóa tài khoản. Mật khẩu đăng ký không được chứa tên đăng nhập hoặc phần trước `@` của email.

API đăng ký bổ sung trường `userName` bắt buộc:

```json
{
  "userName": "quan.nhat",
  "email": "person@example.com",
  "password": "YOUR_PASSWORD",
  "confirmPassword": "YOUR_PASSWORD",
  "firstName": "Nhật",
  "lastName": "Nguyễn"
}
```

Tên trùng trả lỗi `USERNAME_TAKEN` (409). Format không hợp lệ bị từ chối trước khi tạo tài khoản hoặc gửi OTP.

`POST /api/auth/login` và `POST /api/browser-session/login` giữ tên trường JSON `email` để tương thích client cũ; giá trị có thể là tên đăng nhập hoặc email:

```json
{ "email": "quan.nhat", "password": "YOUR_PASSWORD" }
```

Sau khi cập nhật, build frontend và khởi động lại API. Không thay đổi dữ liệu các tài khoản hiện có.
