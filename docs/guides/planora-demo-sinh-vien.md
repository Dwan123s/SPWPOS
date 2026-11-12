# Dữ liệu demo Planora

Minh Anh là sinh viên, học trên lớp buổi sáng, làm thêm vài buổi chiều và chuẩn bị demo IoT theo nhóm. Dữ liệu mẫu dùng múi giờ Việt Nam và các ngày 13–19/11/2026. Nếu demo sau khoảng này, dịch toàn bộ ngày cùng số tuần để giữ nguyên thứ tự.

## 1. Lịch bận và giờ làm việc

Dán đoạn sau vào NORA để nhập lịch bận, hoặc dùng từng dòng để tạo sự kiện trong **Lịch → Lịch làm việc**.

```text
Nhập thời khóa biểu và ca làm dưới đây thành sự kiện cố định, mỗi sự kiện chỉ diễn ra một lần. Thiết lập giờ làm việc theo thời khóa biểu cho các tuần đã nhập.

13/11/2026, 08:00–11:00, học Lập trình IoT, phòng A302.
13/11/2026, 14:00–18:00, ca làm thêm tại quán cà phê.
14/11/2026, 09:00–10:00, họp nhóm chuẩn bị demo IoT.
16/11/2026, 08:00–11:00, học Kiểm thử phần mềm, phòng B204.
16/11/2026, 14:00–18:00, ca làm thêm tại quán cà phê.
17/11/2026, 09:00–11:00, thực hành IoT, phòng A302.
18/11/2026, 15:00–19:00, ca làm thêm tại quán cà phê.
19/11/2026, 08:00–10:00, học Quản lý dự án, phòng B204.
```

Xem lại ngày và giờ trước khi lưu. Mở **Lịch → Giờ làm việc → Theo từng tuần**. Ngày 13/11 có hai khoảng bận tách nhau. Dùng ngày này để kiểm tra Planora chia giờ làm việc thành nhiều khung trong ngày.

## 2. Việc cá nhân trong Inbox

Tạo từng việc ở **Việc → Thêm công việc → Một việc**. Dấu **—** nghĩa là để trống trường đó.

| Công việc | Ngày bắt đầu | Thời lượng | Hạn chót |
| --- | --- | ---: | --- |
| Check mail | — | — | — |
| Gửi email xác nhận mượn thiết bị | 13/11 | 15 phút | 13/11 |
| In phiếu mượn cảm biến | 14/11 | 25 phút | 15/11 |
| Ôn bài kiểm thử hộp đen | 16/11 | 60 phút | 18/11 |

**Check mail** dùng để demo tạo việc chỉ với tên và tick hoàn thành. Trước khi tick, việc này có thể xuất hiện trong mục **Thiếu thông tin xếp lịch** của Workload. Ba việc còn lại có thông tin để lập lịch.

## 3. Project cá nhân có checklist

Trong **Việc → Thêm công việc → Nhiều việc**, tải [đề bài demo IoT](./planora-demo-de-bai-iot.txt) hoặc dán nội dung của file. Bấm **Phân tích bằng NORA**, rồi đối chiếu bản nháp với bảng dưới đây. Bạn có thể sửa tên, thời lượng, thứ tự và checklist trước khi lưu.

**Tên project** — Demo IoT theo dõi nhiệt độ phòng

**Hạn project** — 19/11/2026

**Thứ tự** — Bật **Xếp tuần tự theo danh sách** để minh họa quan hệ giữa các việc.

| Thứ tự | Việc con | Thời lượng | Hạn riêng | Checklist mẫu |
| --- | --- | ---: | --- | --- |
| 1 | Kiểm tra cảm biến và ESP32 | 45 phút | — | Kiểm tra thiết bị · Đọc thử ba giá trị · Ghi lỗi kết nối |
| 2 | Ghi dữ liệu nhiệt độ | 60 phút | — | Ghi thời điểm đo · Thu ít nhất 20 mẫu · Xuất CSV |
| 3 | Dựng dashboard | 90 phút | — | Hiện biểu đồ · Hiện giá trị mới nhất · Thử khi chưa có dữ liệu |
| 4 | Chuẩn bị slide và tập demo | 45 phút | — | Chuẩn bị 5 slide · Tập demo trong 5 phút · Chuẩn bị ảnh dự phòng |

Chọn **Dùng hạn chót của dự án** khi tạo việc con. Sau khi lưu, thử tick một mục checklist trong việc **Kiểm tra cảm biến và ESP32**.

## 4. Xem kết quả trên Hôm nay và Workload

1. Trên **Hôm nay**, mở **Công việc sắp đến hạn** để xem việc Inbox và project có hạn trong phạm vi đang chọn.
2. Chọn **Lưu và xem lịch đề xuất** khi lưu project, hoặc mở **Lập kế hoạch** để xếp các việc chưa có phiên. Xem lại đề xuất và áp dụng.
3. Mở **Tiến độ → Workload → Cá nhân**. Các việc độc lập đã xếp nằm trên dải **Inbox**. Các phiên của việc con nằm trên dải **Demo IoT theo dõi nhiệt độ phòng**.
4. Bấm một ô màu để xem danh sách việc, thời lượng và mở chi tiết. Các buổi học, ca làm và cuộc họp xuất hiện trong **Lịch** và làm giảm giờ còn trống.

## 5. Nhóm mẫu nếu demo với nhiều tài khoản

| Nhóm | Thành viên mẫu | Vai trò |
| --- | --- | --- |
| Nhóm Demo IoT | Minh Anh | Trưởng nhóm |
| Nhóm Demo IoT | Gia Huy | Thành viên |
| Nhóm Demo IoT | Bảo Ngọc | Thành viên |

Trưởng nhóm tạo việc **Kiểm thử cảnh báo nhiệt độ**, thời lượng **45 phút**, hạn **18/11/2026**, chỉ định **Gia Huy**. Yêu cầu gồm ba mục: thử ngưỡng 28°C, chụp trạng thái cảnh báo và ghi lại kết quả. Dùng tài khoản Gia Huy để xem việc được chỉ định và xem đề xuất lịch trước khi nhận việc.

Đăng thêm việc **Rà soát tài liệu demo** ở chế độ **Cả nhóm · Tự nhận việc**. Gia Huy hoặc Bảo Ngọc đều có thể xem lịch trước khi nhận. Trưởng nhóm theo dõi người nhận và trạng thái trong Workload nhóm.
