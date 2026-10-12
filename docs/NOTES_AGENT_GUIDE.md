# Ghi chú và agent lập lịch

Mục **Ghi chú & agent** lưu ghi chú theo tài khoản và chuyển nội dung thành công việc để lập lịch bằng bộ lập kế hoạch của PLANORA.

## Sử dụng

1. Thiết lập giờ rảnh, sự kiện bận và thời gian nghỉ trong **Thời gian & tùy chọn**.
2. Vào **Ghi chú & agent**, nhập tiêu đề và nội dung. Ví dụ:

   ```text
   Viết báo cáo SWP trước thứ Sáu, khoảng 3 tiếng
   Ôn chương 4 vào ngày mai, khoảng 90 phút
   ```

3. Chọn số ngày xếp lịch (1–30). Có thể lưu ghi chú để tiếp tục sau.
4. Chọn **Agent tự xếp lịch**. Hệ thống lưu ghi chú, phân tích, tạo công việc và áp dụng lịch ngay; không cần bấm áp dụng thêm.
5. Xem các phiên đã tạo và cảnh báo. Nếu chưa đủ giờ rảnh, phần chưa xếp được vẫn được báo rõ. Vào **Hôm nay** hoặc **Lịch làm việc** để thực hiện phiên.

Nếu muốn kiểm tra trước, chọn **Agent phân tích**, sửa tên, deadline, thời lượng, ưu tiên, loại việc và chọn các công việc cần lưu. **Tạo công việc & đề xuất lịch** mở bản xem trước; lịch chỉ thay đổi khi chọn **Áp dụng kế hoạch**.

## Phân tích nội dung

- Khi chọn cho phép gửi ghi chú đến AI, hệ thống dùng cấu hình AI hiện có trên server (`AiSettings:Enabled`, `AiSettings:Model`, `AiSettings:ApiKey`). Không cần nhập API key trong trình duyệt.
- Khi AI tắt, thiếu cấu hình hoặc phản hồi không hợp lệ, hệ thống báo rõ đang dùng quy tắc: mỗi dòng hoặc dấu chấm phẩy là một việc. Tối đa 12 công việc trong một lần phân tích.
- Thông tin thiếu được gợi ý thời lượng 60 phút và deadline sau 7 ngày. Ngày tương đối được xử lý theo múi giờ tài khoản. Người dùng có thể sửa các gợi ý ở chế độ xem trước.
- Giờ rảnh, sự kiện cố định, phụ thuộc và giới hạn phiên sử dụng thiết lập có sẵn. Các điều kiện này viết trong note chưa tự cập nhật thiết lập; giao diện báo rõ cần khai báo ở màn hình tương ứng.
- AI chỉ diễn giải thành công việc. Bộ lập lịch hiện có kiểm tra giờ rảnh, sự kiện, deadline, phiên khóa và revision khi áp dụng lịch.

## Lưu trữ và thử lại

Ghi chú và kết quả phân tích được lưu trong bảng `PlanningNotes`. Nội dung tối đa 8000 ký tự, tiêu đề 120 ký tự, tối đa 200 ghi chú mỗi tài khoản.

Sửa ghi chú chưa chuyển thành công việc sẽ xóa kết quả phân tích cũ. Version ngăn yêu cầu từ bản cũ ghi đè dữ liệu mới.

Việc tạo công việc từ note diễn ra trong một transaction. Retry trả cùng các ID đã tạo. Note đã tạo công việc giữ nguyên nội dung; người dùng sửa công việc ở mục **Công việc** hoặc tạo note mới cho việc bổ sung.

Luồng tự động lưu ID kế hoạch đã áp dụng. Retry sau thành công trả lại kết quả cũ. Nếu tạo công việc thành công nhưng lập lịch lỗi, giao diện tải lại note và cho phép tiếp tục xếp lịch, không tạo trùng công việc. Các phiên hiện có của những công việc khác được giữ nguyên.

Xóa note không xóa các công việc hoặc phiên đã tạo.

## Triển khai

Bản này thêm migration `AddPlanningNotes`. Mở Docker Desktop; nếu API đang chạy, dừng bằng `Ctrl+C` tại terminal của API. Từ thư mục gốc dự án, chạy:

```powershell
powershell.exe -NoProfile -ExecutionPolicy Bypass -File .\scripts\Start-Development.ps1
```

Lệnh cho phép chạy script trong tiến trình PowerShell mới, chuẩn bị cấu hình development, cập nhật migration và chạy lại API. Mở `https://localhost:7043/` sau khi terminal báo ứng dụng đã chạy.

Nếu chỉ cần cập nhật database trong phiên PowerShell hiện tại:

```powershell
Set-ExecutionPolicy -Scope Process -ExecutionPolicy Bypass -Force
. .\scripts\Initialize-Development.ps1 -StartContainers
& $SpwposDotnet tool restore
if ($LASTEXITCODE -ne 0) { throw 'Không khôi phục được công cụ EF.' }
& $SpwposDotnet ef database update --project src/SPWPOS.Infrastructure
```

`Initialize-Development.ps1` khởi tạo cả SDK và connection string. Chỉ chạy `Initialize-Dotnet.ps1` không khởi tạo connection string cho terminal mới. Không đưa giá trị connection string hoặc AI key vào Git.

Frontend build lại bằng quy trình hiện có. Kiểm thử AI mặc định sử dụng provider giả; không gọi provider thật hay dùng dữ liệu người dùng.
