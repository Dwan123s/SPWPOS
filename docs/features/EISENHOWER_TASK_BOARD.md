# Task và checklist trực tiếp trong ma trận

Ngày cập nhật: 04/11/2026.

## Sử dụng

Vào **Công việc → Eisenhower**. Bấm **Thêm task** ở đầu trang hoặc ngay trong Q1–Q4 để mở popup gọn: tên công việc, mô tả tùy chọn, nút chọn/xóa ngày hạn và cờ vị trí ưu tiên. Không hỏi thời lượng, checklist hay cấu hình lịch khi thêm. Cờ lấy sẵn từ ô vừa bấm; nút thêm ở đầu trang mặc định chưa phân loại. Chỉ cần tên là lưu được task nháp vào Inbox và ma trận; deadline được lưu `null` nếu chưa chọn, không tự gán deadline hay thời lượng. Lưu không tạo task thực thi, không gọi AI và không thay đổi Calendar.

Task nháp có nhãn **Inbox · Bản nháp**, có thể sửa bằng popup gọn hoặc chuyển giữa các ô. Chưa sắp thứ tự nháp trong cùng ô. Menu **Hoàn thiện trong Nhập việc** mở đúng bản nháp; thời lượng và điều kiện lên lịch được làm rõ ở bước này. Khi nhập thành công việc, bản nháp rời ma trận, công việc mới giữ phân loại đã chọn và dùng thời lượng đã xác nhận. Ngày hạn đã chọn được gửi làm ngữ cảnh cho phân tích ghi chú. Bộ lọc **Chưa có ngày hạn** tìm riêng các nháp không có deadline.

Bấm tên task mở popup chỉnh sửa. Trong checklist, tích từng bước, thêm/xóa/sửa nội dung, kéo tay nắm để đổi thứ tự hoặc dùng Alt + ↑/↓; bấm **Lưu thay đổi** để giữ các chỉnh sửa trong popup. Dòng task mặc định chỉ hiện bộ đếm checklist; bấm bộ đếm để bung ba bước đầu hoặc mở popup xem đầy đủ. Bộ đếm checklist độc lập với tiến độ và trạng thái task; hoàn thành checklist không tự hoàn thành cả task, không ghi thời gian thực tế hay cộng XP. Nút tròn bên trái task cá nhân hoặc nút hoàn thành trong popup dùng luồng hoàn thành hiện có và giải phóng các phiên tương lai, vẫn kiểm tra phiên đang làm, phụ thuộc và điều kiện hoàn thành. Task nhóm không có nút hoàn thành trực tiếp này.

Kéo tay nắm của task sang ô khác hoặc trước/sau task trong cùng ô. Thả mới lưu; Escape/hủy cử chỉ hoặc thả ngoài bảng không lưu. Kéo không đổi deadline, thời lượng hoặc lịch hiện có. Bốn ô có màu phản hồi vị trí thả. Điện thoại có thể kéo lên tab ô nhận hoặc dùng menu **Di chuyển**; dùng Alt + ↑/↓ để đổi thứ tự trong ô. Bảng desktop có bốn ô 2×2 với chiều cao giới hạn và cuộn riêng trong từng ô, thay cho phân trang. Thứ tự và lựa chọn ô được lưu trên máy chủ theo tài khoản, không chỉ trên trình duyệt.

## Bố cục tham khảo TickTick

Task dùng dòng gọn: tên, deadline, thời lượng, bộ đếm checklist và cảnh báo nếu có. Nút **+** ở tiêu đề từng ô mở form tạo trong đúng ô. Menu **···** chứa mở chi tiết, đề xuất lịch và chuyển ô; không hiển thị biểu mẫu chuyển ô hoặc đoạn giải thích dài trên mọi task. **Cần làm rõ** và **Chờ bạn duyệt** là hai tab riêng có số lượng, không thêm hai ô lớn bên dưới. Review vẫn mở đúng task nhóm để duyệt/báo cáo. Điện thoại dùng bốn tab chọn ô.

Bộ lọc gồm Cá nhân/Nhóm, thời hạn, tìm theo tên; các bộ lọc kết hợp đồng thời và có nút xóa. Khoảng hạn tính theo múi giờ tài khoản, bao gồm việc quá hạn để tránh che rủi ro. Sắp xếp Thủ công/Deadline gần nhất/Thời lượng ngắn nhất chỉ thay đổi cách xem, không gọi AI hoặc lưu task. Khi đang sắp theo deadline/thời lượng, kéo chỉ đổi ô; chọn Thủ công để kéo hoặc dùng bàn phím đổi thứ tự trong cùng ô. Tải lại đánh giá định kỳ giữ bảng hiện có trong lúc chờ khi tài khoản và revision không đổi, tránh làm mất phần checklist đang bung.

Task thực thi vẫn có form chi tiết và hợp đồng kiểm tra deadline/thời lượng hiện có. Nháp dùng ghi chú và metadata trong trạng thái xử lý ghi chú trên máy chủ; không cần migration mới. Các endpoint `/api/eisenhower/drafts` kiểm tra chủ sở hữu và version, dùng mã yêu cầu để thử lại tạo mới không bị trùng. Nguồn và đề xuất tiếp theo: `docs/features/EISENHOWER_TICKTICK_RESEARCH.md`.

## Đề xuất Calendar theo nhóm

| Ô | Hành vi |
| --- | --- |
| Q1 | Nêu ưu tiên xử lý/cảnh báo; nút **Đề xuất lịch** để tìm phiên sớm phù hợp |
| Q2 | Nút **Đề xuất lịch**, chủ động phân bổ trước hạn |
| Q3 | Việc ≤15 phút gợi ý làm nhanh rồi hoàn thành, không bắt buộc Calendar; nếu cần vẫn có nút **Xếp giờ nếu cần**. Việc dài dùng **Chọn giờ làm** |
| Q4 | Backlog: không tự tạo đề xuất lịch. Chỉ khi chủ động chọn **Chọn giờ làm** mới thử phương án |

Mở đề xuất chỉ tạo bản xem trước; hiển thị các phiên cũ sẽ thay thế, phiên mới và cảnh báo thiếu phút. Người dùng bấm **Áp dụng vào Calendar** mới lưu lịch. Không cho áp dụng nếu còn thiếu thời gian hoặc có cảnh báo vượt deadline mềm. Bộ lập lịch hiện có vẫn kiểm tra deadline, giờ ăn/ngủ/nghỉ, các phiên được bảo vệ, phụ thuộc, giới hạn ngày/phiên và khả năng nhường giờ hợp lệ cho việc gần hạn. Nếu đã có lịch của task, chỉ phần cần xếp lại trong phạm vi đề xuất bị thay thế. Không đổi deadline để khớp ô người dùng chọn, không tự phân công lại Q3.

Người dùng có thể chọn độ khẩn cấp thủ công khác đánh giá của hệ thống. Thẻ gắn nhãn **Bạn phân loại**; deadline quá hạn, nguy cơ thiếu giờ và các lý do thực tế vẫn được giữ. Chọn lại tự tính để dùng độ gấp từ deadline/capacity. Việc đổi ô là metadata ưu tiên, không phải thay đổi deadline hay bằng chứng đã giải quyết rủi ro.

## Task nhóm và quyền riêng tư

Task đã nhận có checklist cá nhân và vị trí ma trận riêng, không đổi yêu cầu, thời lượng hoặc deadline chung. Popup chỉ cho sửa checklist; có nút mở đúng task trong nhóm để báo cáo/đính kèm file. Checklist của member không trả trong API nhóm, không làm task chuyển sang PendingReview/Completed. Chưa nhận task và task chờ leader duyệt vẫn dùng luồng nhóm hiện có, không có checkbox để bỏ qua leader. Hoàn thành task nhóm vẫn cần member gửi báo cáo rồi leader duyệt.

## Lưu trữ và API

Migration bổ sung `20261103181537_EisenhowerTaskBoard`: `WorkTasks.ChecklistJson`, `MatrixOrder`, `UrgencyOverride`. Dữ liệu cũ mặc định checklist rỗng, thứ tự 0, độ khẩn Auto. Không xóa/sửa task, tài khoản, Notes, lịch cũ hoặc bảng nhóm. Sao lưu/verify trước khi migrate.

- `POST /api/eisenhower/tasks`: tạo task và checklist trong cùng transaction.
- `PUT /api/eisenhower/tasks/{id}`: sửa task cá nhân, kiểm tra version, hợp đồng nhóm và các kiểm tra task sẵn có.
- `PUT /api/eisenhower/tasks/{id}/checklist`: thay checklist tối đa 100 mục; mỗi mục có ID duy nhất, nội dung 1–300 ký tự; có kiểm tra sở hữu/version.
- `POST /api/eisenhower/tasks/{id}/position`: lưu quadrant/vị trí tương đối; kiểm tra task version và workspace revision. Vị trí trước task khác phải thuộc cùng ô/tài khoản và là task cá nhân đã nhận quyền quản lý.
- `POST /api/eisenhower/tasks/{id}/schedule`: tạo PlanProposal, không tự áp dụng; Q3/Q4/Unknown cần `ExplicitRequest=true`.
- Áp dụng dùng API plan hiện có, kiểm tra revision và tình trạng phương án trước khi tạo phiên.

Không ghi đè khi version/revision đã đổi; trả lỗi để tải lại. Giao diện không giả vờ kéo thành công khi máy chủ từ chối. Không gửi dữ liệu thật sang AI khi kéo thả, cập nhật checklist hoặc tạo đề xuất bằng nút ma trận.

## Kiểm chứng

Lần triển khai backend ban đầu: 381 kiểm thử unit liên quan đến Eisenhower, task, lập lịch, nhóm, theo dõi thời gian và kéo lịch đạt; HTTP chạy cả SQLite lẫn SQL Server thật trên database test biệt lập đạt. Lần chỉnh bố cục: kiểm tra frontend và trình duyệt 1440px/390px với hơn 30 task, chuột/cảm ứng, không ghi trước khi thả, popup checklist/sắp thứ tự, tạo task, tab chờ duyệt, cuộn từng ô và bản xem trước/áp dụng Calendar; không tràn ngang hoặc lỗi trang. Kết quả: `artifacts/eisenhower-refinement-tests.log`, `artifacts/eisenhower-refinement-browser.log`; ảnh dữ liệu giả: `artifacts/eisenhower-refined-many-1440.png`, `artifacts/eisenhower-refined-many-390.png`.
