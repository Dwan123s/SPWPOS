# Tham khảo Eisenhower Matrix của TickTick

Khảo sát ngày 04/11/2026. Phạm vi: tài liệu và hình giao diện công khai chính thức; chưa thao tác trên tài khoản TickTick đăng nhập. Bố cục bốn ô, dòng task thu gọn, popup/checklist, hàng đợi phụ, bộ lọc và sắp xếp đã được triển khai; task nháp và view lưu sẵn là hướng tiếp theo. Xem [hướng dẫn hiện tại](EISENHOWER_TASK_BOARD.md).

## Điều xác minh được

1. Matrix là một cách xem cùng task, không sao chép task thành dữ liệu riêng. Có tạo task trực tiếp, kéo giữa các ô, sắp xếp và ẩn/hiện task hoàn thành. Hình desktop dùng bốn ô 2×2; mỗi task là một dòng checkbox, tiêu đề và thông tin phụ. Tiêu đề ô có nút thêm và menu. Tùy chọn hiển thị checklist/chi tiết được tài liệu mô tả cho mobile; không mặc định coi mọi tùy chọn này có trên desktop.

   Nguồn: [How to Use Eisenhower Matrix](https://help.ticktick.com/articles/7055782055577124864), [hình desktop trong bài](https://pull.ticktick.app/hc/all/92e9887dad12f438cbc40f300152a7e5.png).

2. Có hai bộ quy tắc: theo priority; hoặc thời gian kết hợp priority. Bộ thứ hai có thể tự chuyển ô khi ngày đến gần. **Kéo giữa ô có thể đổi ngày và priority**, tùy bộ quy tắc. Bộ lọc hỗ trợ danh sách, nhãn, thời gian, priority và người nhận khi có danh sách chia sẻ. Điều kiện trong một ô kết hợp theo AND; điều kiện giữa các ô có thể trùng.

   Nguồn: [How to Edit the Rules for Eisenhower Matrix](https://help.ticktick.com/articles/7055782040439881728).

3. Có nhiều view với tên và quy tắc riêng, tối đa chín view tùy chỉnh. Xóa view không xóa task. Cùng task có thể xuất hiện khác nhau giữa các view.

   Nguồn: [Create and Manage Eisenhower Matrix Views](https://help.ticktick.com/articles/7519291357756653568).

4. Ở chế độ List, TickTick tách phần chi tiết task khỏi danh sách; chi tiết hỗ trợ ghi chú, ảnh và Markdown. Đây là tham khảo từ List, không phải bằng chứng rằng Matrix có cùng bố cục.

   Nguồn: [TickTick Features](https://ticktick.com/features).

## Đề xuất cho PLANORA

| Khu vực | Hướng chỉnh |
| --- | --- |
| Bảng chính | Bốn ô 2×2 cân bằng trên desktop; cuộn riêng trong ô. Dùng màu làm điểm nhấn, giảm nền trang trí và khoảng trống. |
| Task | Một dòng tiêu đề, một dòng deadline/thời lượng/checklist `2/5`. Giải thích AI và biểu mẫu chuyển ô đưa vào chi tiết/menu. |
| Chi tiết | Bấm task mở popup như yêu cầu đã chốt; chứa ghi chú, checklist có thể kéo, file, thời lượng và đề xuất lịch. |
| Checklist | Mặc định hiện bộ đếm, có thể bung nhanh; không trải sẵn ba bước trên mọi thẻ. Checklist không thay thế trạng thái hoàn thành hoặc duyệt task nhóm. |
| Cần làm rõ / Chờ duyệt | Chuyển thành tab hoặc thanh đếm riêng; không chiếm thêm hai ô lớn dưới ma trận. Vẫn giữ truy cập và luồng duyệt. |
| Điều hướng | Thêm lọc Cá nhân / Nhóm, thời gian và sắp xếp deadline / thủ công. View lưu sẵn là bước sau, tránh bắt người dùng cấu hình ngay. |
| Thêm nhanh | Nút + từng ô, nhập tên trước. Nếu cho lưu khi thiếu deadline/thời lượng, cần hỗ trợ task nháp trong backend; không tự gán số liệu để vượt kiểm tra hiện tại. |
| Điện thoại | Một ô mỗi lần với bốn tab và số lượng; popup toàn màn hình, nút thao tác đủ lớn. |

## Quy tắc phải giữ theo yêu cầu người dùng

- Kéo Q1–Q4 lưu lựa chọn quan trọng/khẩn cấp, **giữ nguyên deadline và Calendar**. Không áp dụng hành vi đổi ngày khi kéo của TickTick.
- Phân loại thủ công không che cảnh báo quá hạn hoặc thiếu thời gian. Có nút trở lại tự tính.
- Q1/Q2 ưu tiên đề xuất lịch; Q3 cân nhắc thời lượng và nhu cầu; Q4 giữ backlog. Chỉ lưu phiên khi người dùng áp dụng phương án.
- Kéo thả, tick checklist và lưu task dùng thao tác trực tiếp, không chờ NORA gọi model.
- Task nhóm vẫn tuân theo quyền leader/member; không có checkbox hoàn thành bỏ qua duyệt.

## Thứ tự thực hiện đề xuất

1. Thu gọn task và bố cục bốn ô; đưa hai hàng đợi phụ vào tab; giữ popup hiện có.
2. Thêm bộ lọc và sắp xếp; cải thiện thao tác checklist và bàn phím.
3. Thiết kế task nháp để thêm nhanh khi chưa rõ thời lượng/deadline, rồi mới làm view lưu sẵn nếu cần.

Kiểm chứng khi triển khai: nhiều task không kéo dài toàn trang; không tràn ngang ở 390px; kéo/tick hoạt động bằng chuột, cảm ứng và bàn phím; lựa chọn còn sau tải lại; kéo không đổi ngày/lịch; luồng báo cáo và leader duyệt giữ nguyên.
