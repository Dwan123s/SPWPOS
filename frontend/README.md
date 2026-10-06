# PLANORA frontend

React 19, Tailwind CSS 4 và Vite 8; backend ASP.NET Core 8 phục vụ SPA trên cùng origin.

```sh
npm ci
npm test
npm run build
```

Yêu cầu Node.js 24.15+. Trên Windows, `scripts/Build-Frontend.ps1` tự chọn Node phù hợp hoặc cài bản portable đã kiểm tra SHA-256.

`npm run dev` chạy Vite build watch. Mở ứng dụng qua API tại `https://localhost:7043/`, tải lại sau khi chỉnh source. Cách này giữ HTTPS, CSP và cookie HttpOnly của hệ thống.

- `src/App.jsx`: cây React, vòng đời ứng dụng, dialog và toast.
- `src/controller.jsx`: điều hướng hash và các thao tác nghiệp vụ gọi API.
- `src/store.js`: trạng thái hiển thị qua `useSyncExternalStore`.
- `src/auth.jsx`, `tasks.jsx`, `calendar.jsx`, `schedule.jsx`, `pages.jsx`, `dashboard.jsx`: các màn hình và form JSX.
- `src/interactive.jsx`: lịch rảnh, ảnh hồ sơ và cropper canvas.
- `src/react-ui.jsx`, `ui.jsx`: các thành phần và helper dùng chung.
- `src/styles/index.css`: Tailwind, token thương hiệu và style liquid glass/responsive.
- `tests/`: kiểm thử component, luồng form và các quy tắc hiển thị.

Vite xuất bundle có hash vào `../src/SPWPOS.Api/wwwroot/app/` và entry HTML vào `wwwroot/index.html`. Output và `node_modules` không đưa lên Git; pipeline build/publish tạo lại từ lockfile. Ảnh/logo dùng chung nằm trong `wwwroot/assets`; font trong `src/styles/fonts` được Vite đóng gói.
