# Planner repair và chống ghi đè task

## Lập lịch

Planner vẫn tạo phương án đầu theo ưu tiên/deadline. Nếu còn công việc chưa xếp đủ, engine thử tối đa 8 thứ tự bổ sung, ưu tiên công việc bị thiếu giờ và các tiền nhiệm cần thiết của nó.

Mỗi lần thử dùng lại cùng bộ kiểm tra giờ rảnh, thời gian nghỉ, giới hạn ngày, work window, deadline, dependency, phạm vi được phép xếp lại và chính sách việc khẩn. Phiên khóa/đang chạy vẫn được giữ.

Chỉ chọn phương án mới khi tổng thời gian chưa xếp giảm, không tăng phần thiếu của bất kỳ task nào và không làm task nào vượt deadline nhiều hơn. Đây là bước sửa có giới hạn, không bảo đảm tìm được nghiệm cho mọi bài toán lập lịch.

Regression bao gồm trường hợp 09:00–10:00 và 11:00–11:30: B cần 60 phút liên tục được xếp 09:00–10:00, A cần 30 phút với deadline sớm hơn được xếp 11:00–11:30. Các ca work window, tiền nhiệm, giới hạn ngày, phiên khóa và deadline mềm cũng được kiểm tra.

## Phiên bản task

`TaskView` trả thêm `version` là GUID không rỗng. Client phải giữ token của bản đã mở để chỉnh sửa, rồi gửi lại trong `TaskInput` khi gọi `PUT /api/tasks/{id}`.

Ví dụ phần thay đổi trong request:

```json
{
  "name": "Viết báo cáo",
  "description": "",
  "deadline": "2026-11-01T17:00:00+07:00",
  "estimatedMinutes": 60,
  "taskType": "WRITING",
  "version": "c6251f86-7364-4f20-8f99-8bc1d982f812"
}
```

- `POST /api/tasks` không cần version; server tạo token.
- `PUT` có token cũ, thiếu token hoặc token rỗng trả HTTP 409, mã `TASK_STALE`.
- Token được kiểm tra trong account transaction, trước khi cập nhật task hoặc hủy session liên quan đến parent.
- DbContext đổi token khi một task được sửa bởi bất kỳ writer nào, gồm status, execution, agent, note và container. Không dùng timestamp làm token, nên hai lần sửa trong cùng thời điểm vẫn phân biệt được.
- EF concurrency token bảo vệ thêm các writer dùng DbContext bên ngoài luồng command. Xung đột tại lớp persistence trả HTTP 409 `DATA_STALE`.
- Cơ chế này yêu cầu version cho form sửa đầy đủ bằng PUT. Các endpoint hành động status/progress/delete vẫn giữ contract hiện có; thay đổi của chúng sẽ làm token của form PUT cũ hết hiệu lực.

Frontend giữ version trong form tại thời điểm mở, không lấy token mới từ workspace ngay trước khi lưu. Khi gặp xung đột, nội dung chưa lưu được giữ, nút lưu bị vô hiệu hóa và người dùng có thể chọn **Tải bản mới** để thay nội dung form bằng bản mới nhất. Không tự retry bằng token mới vì điều đó sẽ ghi đè thay đổi của tab khác.

## Database và triển khai

Migration `20261023171814_AddTaskVersions` thêm cột `WorkTasks.Version`, rồi gán GUID riêng cho dữ liệu đã tồn tại. Không xóa hoặc thay nội dung công việc.

Áp dụng migration qua quy trình nâng cấp database trước khi chạy backend mới. Cần triển khai frontend cùng phiên bản, vì client cũ gửi PUT thiếu version sẽ được từ chối. Không chạy hai phiên bản backend trước/sau thay đổi song song: backend cũ không cập nhật token khi ghi dữ liệu.

```powershell
. .\scripts\Initialize-Dotnet.ps1
dotnet ef database update --project src/SPWPOS.Infrastructure --startup-project src/SPWPOS.Api
```

Lệnh cần cấu hình kết nối database đúng môi trường và EF tool đã được khôi phục theo hướng dẫn dự án. Trong lượt sửa code này không tự áp dụng migration vào database đang phục vụ.

## Kiểm chứng

### Bảo vệ deadline khi xếp riêng task mới

`PlanningProtection` bổ sung vào phạm vi tính toán các task chưa có đủ lịch hợp lệ, có priority 4 hoặc deadline trong 48 giờ tính từ đầu kỳ lập lịch. Các task đã xong, bị hủy, đã quá hạn trước kỳ lập lịch hoặc đã có đủ phiên trước deadline không được bổ sung. Tiền nhiệm của các task được bảo vệ cũng được xét. Các phiên cũ ngoài yêu cầu gốc được giữ nguyên; planner chỉ điền thời lượng còn thiếu và không vượt deadline của nhóm này, kể cả deadline mềm.

Nếu thứ tự ưu tiên khẩn làm bỏ lỡ một deadline sớm hơn nhưng xếp theo deadline có thể hoàn thành cả hai, planner chọn phương án hoàn thành đủ. Nếu thiếu giờ, AI trả `BLOCKED` và không lưu task hoặc phiên mới. API apply từ chối kế hoạch bảo vệ chưa khả thi với HTTP 409 `DEADLINE_PROTECTION_INFEASIBLE`.

Phiên đã lưu sau deadline không được coi là đã bảo vệ deadline. AI cũng chặn phương án nếu nó cần bổ sung lịch cho một task mà người dùng nói rõ không xếp; ví dụ không tự xếp “Đọc đề” khi yêu cầu chỉ xếp “Viết bài” phụ thuộc vào việc đó.

Kế hoạch mở rộng có `hasDeadlineProtection: true` và `requiresConfirmation: true`. AI, ghi chú, luồng tự xếp task và recovery không tự áp dụng kế hoạch này; người dùng kiểm tra các phiên rồi chọn **Xác nhận kế hoạch**. Đây là bổ sung vào planner dùng chung, không phụ thuộc provider AI có nhắc đúng task cũ hay không. Không cần migration database cho thay đổi deadline này.

- `DeadlineProtectionTests`: deadline gần, priority khẩn, thời lượng đã xếp một phần, tiền nhiệm, deadline mềm và phương án xếp theo deadline.
- `AgentDeadlineProtectionTests`: streaming, preview trước khi lưu, retry, thiếu giờ, xác nhận và hoàn tác.
- `UrgentPlanApiTests`: tạo kế hoạch riêng task mới vẫn bảo vệ task cũ và chặn apply khi không đủ giờ.
- Frontend workflows: chỉ gửi apply sau khi người dùng xác nhận kế hoạch bảo vệ deadline.

### Cuộc họp khẩn trùng lịch mua sắm

NORA giữ đúng giờ cuộc họp khẩn thay vì yêu cầu dời cuộc họp để né một lịch mua sắm. Ví dụ đã có “Đi mua sắm” ngày mai 15:00–16:00, thêm cuộc họp khẩn 15:00–17:00 thì đề xuất mua sắm 17:00–18:00 nếu trống. `AgentUrgentEvents` tính chuyển lịch ở backend; AI chỉ cần trả về cuộc họp mới đúng giờ, không đoán giờ sửa của lịch cũ.

- Chỉ bật khi yêu cầu có từ khẩn/gấp/đột xuất gắn với sự kiện; “gặp khách”, “không khẩn” hoặc tên khẩn do model tự thêm không cấp quyền ưu tiên.
- Sự kiện cá nhân loại `OTHER` có tên thể hiện việc linh hoạt (mua sắm, đi chợ/siêu thị, đi dạo, dọn dẹp, giặt đồ, tập thể dục) được đề xuất chuyển tới khoảng trống gần nhất sau giờ cũ, trong cùng ngày và giữ đủ thời lượng. Dùng lịch bận thực tế; giờ rảnh dành cho làm việc không giới hạn một cuộc hẹn cá nhân.
- Không dời sự kiện loại họp/lớp học/cuộc hẹn, sự kiện không rõ có thể linh hoạt, việc đã ghi khẩn hoặc yêu cầu rõ không dời. Với ca làm `WORK` chưa bắt đầu, có thể tạo **đề xuất có điều kiện**, giữ toàn bộ thời lượng và chờ người dùng xác nhận mình được phép đổi ca; không coi đó là việc nơi làm việc đã chấp thuận. Ví dụ ca 15:00–18:00 và họp khẩn 16:00–18:00: đề xuất dời ca tới 18:00–21:00 nếu trống, không cắt mất một giờ làm. Ca ghi “cố định/không thể dời” vẫn được giữ. Không ghi đè khoảng bận, phiên khóa/đang chạy hoặc task priority 4. Không tìm được chỗ dời cùng ngày thì từ chối và giữ nguyên lịch.
- Phiên task chưa khóa và priority dưới 4 bị trùng được planner xếp lại gần giờ cũ, ưu tiên phía sau; giữ các phiên khác, deadline và work window hiện có. Khi các ràng buộc đó không cho dời, trả cảnh báo và không lưu cuộc họp hay thay đổi một phần.
- Cả sự kiện cũ và phiên task đều có giờ cũ → giờ mới trong kết quả. Dùng `hasUrgentReplan` để chờ **Xác nhận dời lịch**, kể cả khi bật tự lưu. Apply kiểm tra lại revision và xung đột; undo khôi phục giờ cũ và bỏ cuộc họp mới. Không thêm migration hoặc thay API nhập sự kiện thủ công.

Kiểm thử: `AgentUrgentEventTests`, trường hợp cuộc họp khẩn trong `AgentApiTests`, kiểm tra prompt provider và giao diện NORA hiển thị giờ cũ/giờ mới của sự kiện.

`AgentChangeView.reason` chứa lý do/điều kiện theo từng mục; UI hiển thị cạnh giờ cũ và giờ mới. Reply của đề xuất nêu tên cuộc họp cần giữ, từng sự kiện phải dời và giờ cụ thể. Sau apply, reply chuyển sang kết quả đã lưu, không tiếp tục nói “chưa thay đổi lịch”. Khi chỉ có thể đổi giờ yêu cầu mới, thông báo xung đột và tiêu đề các lựa chọn phải nói rõ điều đó.

### Lịch khẩn mới ưu tiên hơn lịch khẩn đã lưu

Khi yêu cầu khẩn mới trùng một sự kiện khẩn đã lưu, NORA giữ giờ của yêu cầu mới và đề xuất dời sự kiện khẩn cũ, không hạ mức ưu tiên của lịch mới. Áp dụng cả phiên task priority 4 chưa khóa và trường hợp task khẩn mới trùng task khẩn cũ. Giữ toàn bộ thời lượng, không xóa lịch cũ, không đổi deadline thật và vẫn chờ xác nhận trước khi ghi.

`FixedEvents.IsUrgent` lưu trạng thái khẩn, để các lần nhập tiếp theo không phụ thuộc tên cuộc họp. Cột nullable giữ tương thích dữ liệu cũ: giá trị null dùng nhận diện từ tên/mô tả; true/false là lựa chọn rõ ràng. Chat ghi cờ từ yêu cầu đã kiểm chứng; form sự kiện có ô **Lịch khẩn**, và sửa nhanh không làm mất cờ này. API `EventInput.IsUrgent` bỏ trống khi sửa sẽ giữ nguyên trạng thái hiện có.

Sự kiện khẩn cũ ưu tiên khoảng trống sau giờ cũ trong cùng ngày; nếu hết chỗ, thử tối đa hai ngày tiếp theo, từ giờ bắt đầu cũ trở đi. Giải thích ghi rõ “Lịch khẩn mới được ưu tiên trước lịch khẩn cũ” kèm tên và giờ dời cụ thể. Phiên đang thực hiện/khóa và chỉ dẫn không dời vẫn được bảo vệ; nếu chưa có phương án đủ thời lượng thì không lưu dở dang.

Migration `20261024034653_AddEventUrgency` chỉ thêm cột `bit NULL`, không thay đổi giờ hay nội dung các sự kiện. Cần áp dụng trước khi chạy backend mới. Kiểm thử bao gồm cờ khẩn với tên bình thường, tên khẩn cũ chưa có cờ, chuỗi ba lịch khẩn liên tiếp, dời sang ngày kế tiếp, undo, API lưu/đọc/sửa và UI giữ cờ khẩn.

### Các kiểm chứng planner/concurrency trước đây

- `PlanningRepairTests`: tìm phương án khả thi và bảo vệ các ràng buộc khi đổi thứ tự.
- Unit `TaskVersionTests`: hai DbContext cùng đọc một phiên bản không thể ghi đè nhau.
- Integration `TaskVersionTests`: hai bản edit, cập nhật sau tải lại, token thiếu/rỗng/sai, thay status trong cùng thời điểm và concurrent PUT trên SQL Server.
- `UmlMigrationTests`: upgrade gán token khác nhau cho task cũ và giữ nguyên dữ liệu nghiệp vụ.
- Frontend workflows: gửi version lúc mở form; giữ bản nháp khi 409; tải lại rõ ràng trước khi lưu tiếp.

Test SQL Server chỉ chạy khi có `SPWPOS_TEST_SQLSERVER` và luôn dùng database test do factory tạo riêng.
