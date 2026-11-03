# Duyệt tham gia nhóm và loại thành viên

## Hành vi

Link hoặc mã mời tạo yêu cầu `Pending`, không cấp quyền ngay. Membership có `Active=false`, lưu thời điểm và lời mời gốc; chỉ leader thấy danh sách chờ duyệt với tên và email. Người gửi chỉ xem trạng thái yêu cầu của chính mình. Thành viên đang hoạt động trước cập nhật được giữ nguyên quyền.

Leader duyệt hoặc từ chối bằng version của yêu cầu. Chỉ duyệt khi tài khoản còn hoạt động, đã xác minh và lời mời chưa hết hạn/thu hồi. Gửi lặp yêu cầu đang chờ không tạo thêm membership hoặc tiêu thêm lượt. Cùng lời mời đã bị từ chối không dùng lại; lời mời mới cho phép gửi yêu cầu mới. Mỗi tài khoản có tối đa 20 yêu cầu chờ duyệt.

Leader loại member bằng hộp xác nhận. Backend thu hồi quyền nhóm, đổi cả version và mutation token, vô hiệu hóa đề xuất nhận việc còn chờ, bỏ chỉ định và đưa việc chưa hoàn tất về `Open`. Assignment ngừng hoạt động. Bản sao task, lịch cá nhân và lịch sử thực hiện được giữ nguyên, không đưa lịch riêng vào phản hồi của leader. Task nhóm đã hoàn tất và file báo cáo cũ được giữ làm lịch sử. Tài khoản bị loại không thể dùng mã/link mới hoặc cũ để tự vào lại. Việc tự rời nhóm vẫn yêu cầu trả việc đang nhận và bỏ chỉ định trước.

Giao diện tự cập nhật danh sách nhóm/yêu cầu mỗi 7 giây khi tab đang hiển thị. API chặn ngay từng request sau khi loại; khi nhận `TEAM_FORBIDDEN`, giao diện đóng dữ liệu nhóm và các popup liên quan. Nội dung đã được tải về trước khi loại không thể thu hồi từ thiết bị người nhận.

Thông báo yêu cầu tham gia có nút **Mở nhóm để duyệt** trong hộp thông báo và popup mới. Nút mở đúng nhóm, cuộn và chuyển focus tới **Chờ duyệt tham gia** để trưởng nhóm chọn duyệt hoặc từ chối. API bổ sung `teamId` bằng cách tra membership gốc từ khóa thông báo, áp dụng cả thông báo đã lưu, không cần migration. API nhóm tiếp tục kiểm tra quyền truy cập.
Nút chỉ hiện khi `joinRequestPending` còn đúng: membership vẫn chờ duyệt và version trùng yêu cầu gốc. Sau khi duyệt hoặc từ chối, nút tự ẩn cả trong hộp thông báo lẫn popup đang mở khi dữ liệu cập nhật. Thông báo cũ không hiện lại nút nếu người dùng gửi yêu cầu mới; trạng thái đã đọc độc lập với trạng thái duyệt.

## API và dữ liệu

- `POST /api/teams/join` trả `{id, status}` với `Pending` hoặc `Approved`.
- `GET /api/teams/join-requests` chỉ trả các yêu cầu/trạng thái của tài khoản gọi.
- `GET /api/teams/{id}` trả `joinRequests` cho leader, mảng rỗng cho member.
- `POST /api/teams/{id}/join-requests/{memberId}/review` nhận `{version, approved}`.
- `POST /api/teams/{id}/members/{memberId}/remove` giữ endpoint cũ, bổ sung xử lý loại member đang nhận việc.
- Migration `20261102192617_TeamMembershipApproval` thêm `ApprovalStatus`, `JoinInviteId`, `RequestedAt` và index trên membership; không tạo lại/xóa bảng người dùng hoặc task. Membership cũ không hoạt động được đánh dấu `Left`.

## Kiểm chứng

- 107 kiểm thử nghiệp vụ nhóm ở `PrivateTeamPlanningTests` đạt, gồm chờ duyệt, giới hạn truy cập, từ chối, lời mời bị thu hồi, loại khi đang nhận việc/chờ duyệt hoàn thành, bảo toàn lịch riêng và vô hiệu hóa claim cũ.
- 529 kiểm thử frontend trên 67 file đạt khi chạy với `--maxWorkers=2 --testTimeout=15000`. Lần chạy mặc định trước đó bị timeout ở một số kiểm thử không thuộc nhóm; hai kiểm thử link mời đã được cập nhật theo hành vi gửi yêu cầu.
- 3 kiểm thử API/SQL liên quan đạt, không bỏ qua: API truy cập trước/sau duyệt, tải tài liệu trước/sau loại, chặn quay lại qua link, hai lần duyệt đồng thời chỉ một lần thành công, và loại khi đang gửi chat. SQL dùng DB tạm `SPWPOS_Tests_*` và tự dọn sau chạy.
- Kiểm tra trình duyệt ở 1440px, 390px, 320px đạt: duyệt yêu cầu, hộp xác nhận loại, không gửi lệnh loại trước xác nhận, không tràn trang và không có lỗi JavaScript. API giả lập, không tạo tài khoản/dữ liệu demo thật.
- Build Release không cảnh báo/lỗi; EF không còn model change chưa có migration.

Lần chạy SQL mở rộng thêm `PrivateTeamSqlTests` phát hiện deadlock ở kiểm thử hai leader duyệt hoàn thành task, tại `TeamCompletionService.cs`. Đây là luồng duyệt hoàn thành có sẵn, tách biệt với duyệt tham gia; chưa sửa trong thay đổi này. Không coi kết quả kiểm thử tính năng mới là toàn bộ bộ SQL đã đạt.

Chứng cứ local được lưu trong `artifacts/team-approval-unit.xml`, `artifacts/team-approval-sql.xml`, ảnh `artifacts/team-membership-*.png` và metadata triển khai `artifacts/team-approval-deployment.json`. Các artifact local không chứa mật khẩu/token và không được đưa vào source.
