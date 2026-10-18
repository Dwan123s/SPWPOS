# Frontend React và Tailwind CSS

PLANORA sử dụng React 19.3, Tailwind CSS 4.3 và Vite 8.3. Các màn hình hiện có được chuyển thành JSX, gồm đăng nhập/OTP, dashboard, công việc, lịch tuần/danh sách, thời gian rảnh, phân tích, kịch bản, thông báo, hồ sơ và quản trị.

## Tổ chức và tích hợp

- Source frontend: `frontend/src`; dependency khóa trong `frontend/package-lock.json`.
- React quản lý screen, dialog, toast, danh sách lọc, các hàng lịch rảnh và preview avatar. Timer dùng hook và dọn interval khi unmount; cropper dùng canvas/ref và pointer capture.
- State hiển thị dùng `useSyncExternalStore`; nghiệp vụ gọi API trong `controller.jsx` và `api.js`.
- Tailwind cung cấp utility, theme token và các primitive qua `@apply`; CSS thương hiệu duy trì liquid glass, responsive và bố cục đã chọn. Phạm vi quét Tailwind giới hạn ở source frontend để build ổn định giữa checkout.
- Frontend build thành JS/CSS/font có hash trong `wwwroot/app`, phục vụ cùng API. Không dùng CDN, không thêm HTML parser runtime hoặc `dangerouslySetInnerHTML` để hiển thị màn hình.
- API, cookie HttpOnly, CSRF header, HTTPS và CSP giữ các hợp đồng hiện có. Backend vẫn .NET 8.

## Chạy và chỉnh sửa

```powershell
powershell.exe -NoProfile -ExecutionPolicy Bypass -File .\scripts\Start-Development.ps1
```

Windows script chọn Node.js 24.15+ hoặc tải Node 24 LTS portable riêng cho dự án, kiểm tra SHA-256 trước khi giải nén. Lệnh khởi động build frontend trước khi chạy API.

Để sửa frontend khi API đang chạy, mở terminal thứ hai:

```powershell
powershell.exe -NoProfile -ExecutionPolicy Bypass -File .\scripts\Build-Frontend.ps1 -Watch
```

Vite build watch tạo lại tài nguyên; tải lại `https://localhost:7043/` để xem thay đổi. Đây là chế độ build watch, chưa tích hợp HMR. `dotnet publish src/SPWPOS.Api -c Release` tự build và đóng gói frontend.

## Xác minh

- Component/workflow tests qua Vitest, React Testing Library và jsdom; kiểm tra cả các quy tắc hiển thị đã có trước khi chuyển đổi.
- Luồng đăng nhập, reset mật khẩu/OTP, tạo và capture công việc, giữ giá trị lịch rảnh khi thêm/xóa hàng, lưu hồ sơ, giới hạn thao tác kịch bản, timer và menu mobile.
- Regression Web Locks của refresh session chạy bằng Node test runner.
- Release build và các kiểm thử backend, gồm SQL Server concurrency/migration, đã chạy thành công.
- Publish từ bản sao mã nguồn không có `node_modules` hoặc bundle sẵn đã tạo entry, JS/CSS và các tài nguyên tĩnh.
- Kiểm tra HTTP trực tiếp trên localhost cho entry và bundle; component tests không thay thế kiểm tra hình ảnh trong trình duyệt. Chưa thực hiện visual QA bằng trình duyệt trong lần chuyển đổi này.

Các template JavaScript/CSS frontend cũ trong `wwwroot/assets` đã được thay bằng source React. Logo và ảnh thương hiệu được giữ lại. Output build và `node_modules` không theo dõi bằng Git.

Tham khảo triển khai: [React với build tool](https://react.dev/learn/build-a-react-app-from-scratch), [Tailwind với Vite](https://tailwindcss.com/docs/installation/using-vite), [giới hạn source Tailwind](https://tailwindcss.com/docs/detecting-classes-in-source-files).
