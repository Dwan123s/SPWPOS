# Trang cá nhân và bạn bè

Người đã đăng nhập có thể xem trang cá nhân của nhau: tên hiển thị, ảnh đại diện, ảnh bìa, giới thiệu, tháng tham gia, số bạn bè và số bạn chung. Email, thông tin đăng nhập, công việc, lịch và giờ rảnh không xuất hiện trong các API xã hội.

## Giao diện

- `#profile`: hồ sơ của tài khoản hiện tại.
- `#profile/<user-id>`: hồ sơ thành viên khác; có liên kết từ tên thành viên trong nhóm và chat.
- `#friends/people`: tìm theo tên hiển thị.
- `#friends/friends`: bạn bè của tôi.
- `#friends/incoming`, `#friends/outgoing`: lời mời đã nhận và đã gửi.
- Chủ hồ sơ sửa thông tin/ảnh đại diện trong Cài đặt, đổi/gỡ ảnh bìa ngay trên trang cá nhân.
- Danh sách có phân trang 24 người/lần, tìm kiếm có thời gian chờ 350 ms. Có nút làm mới và tải lại khi cửa sổ được focus; không bổ sung vòng polling liên tục.

## Quan hệ bạn bè

Một hàng `Friendships` cho mỗi cặp GUID được chuẩn hóa bằng `Guid.CompareTo`. Trạng thái `Pending` có người gửi, `Accepted` là bạn bè hai chiều. Gửi lại hoặc gửi chéo không tự động chấp nhận. Chỉ người nhận có thể chấp nhận; cả hai có thể xóa lời mời hoặc hủy kết bạn. Các quyết định trên một cặp được tuần tự hóa bằng khóa key-range SQL Server trong transaction SERIALIZABLE; có token concurrency và ràng buộc cơ sở dữ liệu.

Lời mời và chấp nhận tạo thông báo trong ứng dụng theo tùy chọn của người nhận. Thông báo lời mời cũ được ẩn khi lời mời được giải quyết. Không gửi email hoặc Telegram tự động cho thao tác xã hội.

## API

Tất cả endpoint dưới `/api/social` yêu cầu xác thực:

| Method | Path | Mục đích |
| --- | --- | --- |
| GET | `/profiles/{id}` | Hồ sơ giới thiệu |
| GET | `/people?search=&offset=0` | Tìm người theo tên |
| GET | `/connections?kind=friends\|incoming\|outgoing&offset=0` | Quan hệ của người đang đăng nhập |
| GET | `/profiles/{id}/friends?offset=0` | Bạn bè đã chấp nhận của hồ sơ |
| POST | `/friends/{id}` | Gửi lời mời |
| POST | `/friends/{id}/accept` | Chấp nhận lời mời |
| DELETE | `/friends/{id}` | Thu hồi, từ chối hoặc hủy kết bạn |
| PUT | `/profile/cover` | Đổi/gỡ ảnh bìa của chính mình |

Ảnh bìa được nén/căn giữa 1600 × 600 trên trình duyệt. API chỉ nhận data URL PNG/JPEG/WebP tối đa 2 MB, kiểm tra chữ ký định dạng và từ chối URL ngoài, SVG, nội dung thực thi.

## Dữ liệu và kiểm thử

Migration `20261105091227_SocialProfilesAndFriendships` chỉ thêm `user_profiles.CoverUrl` và bảng `Friendships` cùng ràng buộc/index. Không thay đổi mật khẩu, phiên đăng nhập, lịch hay công việc. SQL nâng cấp có thể tạo bằng EF migrations script từ `20261103181537_EisenhowerTaskBoard` tới migration này.

`SocialApiTests` kiểm tra quyền, toàn bộ vòng đời lời mời, riêng tư, phân trang, bạn chung, ảnh bìa và SQL Server gửi chéo đồng thời. Nhánh SQL dùng `ApiFactory(useSqlServer: true)` tạo cơ sở dữ liệu `SPWPOS_Tests_<GUID>` riêng. `frontend/tests/social.test.jsx` kiểm tra thao tác, lỗi mạng, ảnh bìa và phản hồi tìm kiếm đến muộn.
