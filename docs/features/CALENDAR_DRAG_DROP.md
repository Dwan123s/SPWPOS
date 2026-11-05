# Kéo thả lịch cá nhân

Vào **Lịch làm việc → Tuần**. Lịch tuần có lưới 24 giờ và bảy ngày, cuộn bên trong khung; ban đầu mở từ khoảng 07:00.

- Kéo thẻ sự kiện/phiên lên xuống để đổi giờ, sang cột khác để đổi ngày. Giữ nguyên thời lượng khi di chuyển, căn theo bước 15 phút.
- Kéo mép dưới để đổi giờ kết thúc của mục trong một ngày. Không tự sửa thời lượng ước lượng gốc của task hay deadline.
- Trên điện thoại, mở chế độ Tuần, kéo bằng nút nhỏ trên thẻ; kéo bên ngoài nút vẫn cuộn lịch bình thường. Dùng mép dưới để đổi thời lượng.
- Có hình xem trước trong khi kéo; chỉ gửi yêu cầu khi thả ở vị trí hợp lệ. Thả ngoài khung hoặc Esc/pointer-cancel không lưu. Tự cuộn khi kéo gần mép khung.
- Khi lưu thành công có **Hoàn tác** cho lần chỉnh mới nhất. Hoàn tác vẫn kiểm tra revision/xung đột; lịch đã thay đổi ở nơi khác thì cần tải lại.
- Bấm bình thường vẫn mở popup chi tiết; kéo không mở popup. Bàn phím: focus nút kéo, **Alt + ↑/↓** đổi 15 phút, **Alt + ←/→** đổi ngày. Focus nút đổi thời lượng, Alt + ↑/↓ đổi giờ kết thúc.
- Chế độ Danh sách giữ nguyên. Không tạo lịch mới bằng việc kéo task từ sidebar và không tự chuyển tuần trong lúc kéo; dùng điều hướng tuần để xem các ngày khác.

## Thêm lịch thủ công bằng ô trống

Trong chế độ **Tuần**, bấm vùng trống để mở form sự kiện với khoảng một giờ đã chọn; gần cuối ngày khoảng này dừng ở 00:00 hôm sau. Bấm giữ và kéo lên/xuống hoặc qua cột ngày khác để chọn khoảng bắt đầu/kết thúc theo nấc 15 phút. Khoảng chọn có nền xanh xem trước, tự cuộn khi kéo gần mép. Thả chuột mở popup điền tên, loại và mô tả; chỉ **Lưu sự kiện** mới gửi yêu cầu tạo lịch. Chọn giờ không tạo task hoặc gọi AI, kể cả khi bật chế độ NORA.

Esc, pointer-cancel, thả ngoài khung, đổi tài khoản/tuần, mất focus hoặc rời trang hủy lựa chọn đang kéo. Chạm và cuộn trên điện thoại vẫn cuộn bình thường; bật **Chọn khoảng giờ** trước nếu muốn kéo bằng cảm ứng. Nút **+** ở đầu mỗi ngày mở form tại 09:00 của ngày đó, hỗ trợ bàn phím. Nút **Sự kiện** trên thanh công cụ mở form nhập tay ở cả chế độ Tuần và Danh sách.

Ngày giờ dùng múi giờ tài khoản. Form vẫn kiểm tra thời gian và xung đột qua API sự kiện hiện có; báo lỗi ngay trong popup và giữ nội dung để sửa. Thao tác trên thẻ lịch đang có vẫn mở chi tiết hoặc di chuyển/đổi thời lượng theo cơ chế cũ.

Không tạo được lịch bắt đầu trong quá khứ hoặc đúng thời điểm hiện tại: chặn từ lúc bấm/kéo, kiểm tra lại khi thả và khi lưu, đồng thời máy chủ từ chối `EVENT_IN_PAST`. Nút cộng ở ngày cũ bị vô hiệu; ngày hôm nay ưu tiên 09:00 hoặc mốc 15 phút sắp tới nếu đã qua 09:00. Phần giờ đã qua có nền gạch nhạt. Lịch sử vẫn xem được và sửa tên/mô tả khi giữ nguyên giờ bắt đầu; không chuyển giờ bắt đầu về quá khứ.

Kiểm chứng chặn quá khứ: 30 kiểm thử frontend, 18 unit backend và 2 API integration đạt. Trình duyệt Edge với đồng hồ giả lập kiểm tra ngày cũ bị khóa, bấm/kéo ngược vào giờ đã qua không mở popup, nhập giờ quá khứ không gửi yêu cầu, popup hết hiệu lực bị chặn khi lưu và sửa sang giờ tương lai lưu được. Script local: `artifacts/calendar-past-browser-test.mjs`.

### Sửa lỗi kéo tạo ngày 04/11/2026

Đối chiếu thao tác [bấm giờ trống hoặc kéo để chọn khoảng dài hơn của Google Calendar](https://support.google.com/calendar/answer/72143?hl=en). Tái hiện bằng thao tác chuột trình duyệt thật: `pointerup` mở dialog, nhưng click tương thích phát sinh tiếp theo bị retarget vào backdrop và listener bấm ngoài đóng popup ngay. Giao diện giờ chặn riêng click kết thúc cử chỉ trước listener bấm ngoài; lần pointerdown kế tiếp vẫn tương tác bình thường. Dọn listener khi unmount/đổi phạm vi hoặc hết thời gian chờ. Tiêu đề ngày dùng ba cột riêng cho tên thứ, số ngày và nút cộng, thay vị trí tuyệt đối gây chồng lấp.

Kiểm tra bằng Edge headless độc lập, API giả lập và dữ liệu thử, không ghi dữ liệu tài khoản thật: 1600/1280 px dùng chuột; 390 px dùng touch thật. Đạt chọn giờ sau cuộn, popup còn mở sau click từ pointerup, đúng 18:00–19:00, không ghi trước Lưu, lỗi trùng giữ tên đã nhập và lưu lại hiển thị sự kiện. Desktop kiểm tra thêm click một giờ, kéo ngược và Esc. Đo bounding box xác nhận nút cộng không giao số ngày ở cả bảy cột; không tràn ngang toàn trang hoặc lỗi JavaScript. Script và ảnh: `artifacts/calendar-create-browser-test.mjs`, `artifacts/calendar-create-fixed-*.png`. 34 kiểm thử giao diện liên quan đạt, gồm regression click tương thích và popup hiện có.

## Ràng buộc lịch đã có

Chỉ phiên Scheduled, chưa khóa, chưa bắt đầu/ghi nhận actual được kéo. Sự kiện đã bắt đầu không được kéo. Các mục này vẫn xem được qua popup. Sự kiện cố định trong tương lai được người dùng chủ động đổi giờ; đây không phải NORA tự dời lịch cố định.

Máy chủ kiểm tra quyền sở hữu, account revision, giờ gốc của mục, thời điểm tương lai, xung đột, giờ rảnh, khoảng nghỉ, giới hạn phiên/ngày, phụ thuộc, work window và deadline của task. Task nhóm đang chờ duyệt/thay đổi yêu cầu không được chỉnh phiên qua kéo thả. Không tự dời các mục khác khi drop xung đột; giữ lịch cũ và hiển thị lý do.

Sự kiện chỉ đổi lần xuất hiện được kéo; giữ tên, mô tả, loại và mức khẩn cấp. Phiên vẫn gắn task cũ; giữ deadline, estimate và tiến độ. Mục qua đêm hiển thị theo từng ngày; di chuyển giữ nguyên tổng thời lượng. Resize trực tiếp chỉ cho mục nằm trong một ngày.

## API

`POST /api/calendar/move` (đăng nhập, owner lấy từ token):

```json
{
  "kind": "Event",
  "id": "<id>",
  "start": "2026-11-06T04:15:00Z",
  "end": "2026-11-06T05:15:00Z",
  "revision": "<workspace.preference.revision>",
  "expectedStart": "2026-11-05T03:00:00Z",
  "expectedEnd": "2026-11-05T04:00:00Z"
}
```

`kind` là Event hoặc Session. Response có thời gian mới, thời gian trước và revision đã commit để undo. Chỉ cập nhật giờ trên máy chủ, không nhận lại tên/mô tả từ giao diện để tránh ghi đè metadata cũ. Không migration, không đổi loại sự kiện/phiên.

## Kiểm chứng 03/11/2026

- 13 unit backend tests qua: move/resize/undo, metadata sự kiện, xung đột không ghi một phần, revision và giờ gốc stale, owner, locked/running/completed/past, deadline cứng/mềm và giới hạn thời gian.
- API integration: 401, move Event, stale 409, undo bằng revision, giữ metadata, không sinh task, loại sai 400.
- Frontend: 37 tests gồm calendar hiện có và bảy test mới về snapping/múi giờ, qua đêm/overlap, readonly, keyboard move/resize/undo, rollback và cleanup.
- Browser 1440/390 px: kéo chuột và touch thật (CDP), preview không ghi, drop căn 15 phút, resize, undo, xung đột giữ giờ cũ, khóa phiên và Esc. Log/ảnh tại `artifacts/calendar-drag-*`.

Bản local đã build/restart thành công (0 warnings/0 errors), schema khớp không thay đổi database. Kiểm tra HTTP: frontend/bundle lịch 200, API `/api/calendar/move` có trong Swagger và trả 401 khi chưa đăng nhập. Model AI vẫn `gemini-3.8-flash`; thao tác kéo thả không gọi model.
