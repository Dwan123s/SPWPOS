# Cột phân tích tự xuống hàng sau khi tải Notes

## Nguyên nhân đã tái hiện

`App.jsx` tải trước trang đích khi rê chuột/focus phần tử điều hướng. Khi tải trước Notes, Vite cũng tải `notes-entry.css`, gồm `note-studio.css`. File này chứa các quy tắc toàn cục `.planning-console .planning-dashboard` (ép hai cột) và `.planning-console .intelligence-column` (chiếm toàn hàng). Chúng tác động ngay cả khi route vẫn là Hôm nay.

Độ trễ người dùng quan sát phụ thuộc lúc bắt đầu và hoàn tất tải chunk/CSS, không phải một timer đổi layout sau đúng 10 giây. Tải trước hoặc mở Notes rồi quay về Hôm nay đều có thể gây hiện tượng này.

Browser production trước sửa, viewport 1857 px: ba cột `415.578 / 721.828 / 415.594 px` ở y=134. Focus nút “Nhập việc” để kích hoạt tải trước Notes: chuyển thành hai cột `513.625 / 1059.38 px`, cột phân tích chuyển xuống y=784.781 dù không đổi route/kích thước cửa sổ.

## Sửa

Bỏ các override dashboard cũ khỏi `note-studio.css`, bao gồm override mobile. Quy tắc bố cục responsive thuộc `planning-console.css`: desktop ba cột, màn hình vừa hai cột với phân tích phía dưới, điện thoại một cột. Nội dung và luồng Notes giữ nguyên.

## Kiểm chứng

- `notes.test.jsx` và `route-loading.test.jsx`: **35 passed**; frontend build thành công.
- Browser kiểm tra tải trước Notes rồi chờ 12 giây (qua chu kỳ polling 10 giây), vị trí/số cột không đổi; mở Notes rồi trở lại Hôm nay trong cùng document vẫn giữ bố cục.
- Kiểm tra ở 1857, 1440, 1100 và 390 px: số cột và vị trí trong document giữ nguyên sau tải trước; không tràn ngang, không lỗi JavaScript. Trên mobile, đo tọa độ document để không nhầm thao tác focus tự cuộn với thay đổi layout.
- Script và bằng chứng: `artifacts/dashboard-layout-browser.mjs`, `dashboard-layout-reproduce.log`, `dashboard-layout-measurements.log`, `dashboard-layout-mobile.log`, `dashboard-layout-*.png`.
- Bản local đã phục vụ bundle/CSS mới (HTTP 200); stylesheet Notes không còn selector `.planning-dashboard`. Chỉ cần Ctrl+F5, không cần restart API.
