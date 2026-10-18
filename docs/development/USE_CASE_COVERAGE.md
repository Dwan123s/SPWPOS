# Đối chiếu 72 Use Case

Nguồn: tài liệu SPWPOS phiên bản 72 Use Case do người dùng cung cấp. Bảng mô tả nơi chức năng được triển khai; không đồng nghĩa mỗi dòng đã có một test tự động riêng. Test theo luồng và invariant, xem `tests/SPWPOS.UnitTests/PlanningTests.cs`, `tests/SPWPOS.IntegrationTests/WorkspaceFlowTests.cs`, `WorkspaceConcurrencyTests.cs` và suite Authentication.

| UC | Chức năng | UI / API |
|---|---|---|
| 01 | Register | Đăng ký / `POST /api/auth/register`, verify email OTP |
| 02 | Login | Đăng nhập / `/api/browser-session/login`, `/api/auth/login` |
| 03 | Logout | Sidebar / `/api/browser-session/logout`, `/api/auth/logout` |
| 04 | Request password reset | Quên mật khẩu / `/api/auth/forgot-password` |
| 05 | Reset password | OTP → ticket → form reset / verify-forgot-password-otp, reset-password |
| 06 | Change password | Tài khoản / request OTP + current password + new password |
| 07 | View profile | Tài khoản / `/api/auth/me`, `/api/workspace` |
| 08 | Update profile | Tài khoản / `PUT /api/workspace/profile` |
| 09 | Create task | Thêm công việc / `POST /api/tasks` |
| 10 | View tasks | Công việc / `GET /api/tasks` |
| 11 | Task details | Chi tiết / `GET /api/tasks/{id}` |
| 12 | Update task | Form chỉnh sửa / `PUT /api/tasks/{id}` |
| 13 | Delete task | Xác nhận xóa / `DELETE /api/tasks/{id}`, soft delete |
| 14 | Search tasks | Ô tìm kiếm / query `search` |
| 15 | Filter tasks | Trạng thái, category, priority, deadline / query filters |
| 16 | Change status | Chi tiết / `PATCH /api/tasks/{id}/status` |
| 17 | Create category | Tài khoản & nhóm / `POST /api/categories` |
| 18 | Update category | Sửa nhóm / `PUT /api/categories/{id}` |
| 19 | Delete category | Xóa nhóm / `DELETE /api/categories/{id}` |
| 20 | Create subtask | Chi tiết việc cha / task `parentId` |
| 21 | Update subtask | Form công việc / task update |
| 22 | Delete subtask | Chi tiết công việc / task soft delete |
| 23 | Add dependency | Chi tiết / `POST /api/tasks/{id}/dependencies/{predecessor}` |
| 24 | Remove dependency | Chi tiết / `DELETE /api/tasks/{id}/dependencies/{predecessor}` |
| 25 | Natural language capture | Ghi nhanh, form xác nhận / `POST /api/tasks/capture` |
| 26 | Break down task | Gợi ý chia nhỏ, chọn mục lưu / `POST /api/tasks/{id}/breakdown` |
| 27 | Weekly availability | Thời gian & tùy chọn / `PUT /api/time/availability` |
| 28 | Add exception | Thêm ngoại lệ / `POST /api/time/exceptions` |
| 29 | Update exception | Sửa ngoại lệ / `PUT /api/time/exceptions/{id}` |
| 30 | Remove exception | Xóa ngoại lệ / `DELETE /api/time/exceptions/{id}` |
| 31 | Fixed event create | Thêm sự kiện / `POST /api/time/events` |
| 32 | Fixed event update | Sửa sự kiện / `PUT /api/time/events/{id}` |
| 33 | Fixed event delete | Xóa sự kiện / `DELETE /api/time/events/{id}` |
| 34 | Calendar | Lịch tuần / `GET /api/time/calendar` |
| 35 | Work preferences | Thời gian & tùy chọn / `PUT /api/time/preferences` |
| 36 | Generate plan | Lập kế hoạch / `POST /api/plans` |
| 37 | Review proposal | Hộp thoại xem trước / response plan, `GET /api/plans/{id}` |
| 38 | Apply proposal | Áp dụng / `POST /api/plans/{id}/apply` |
| 39 | Move session | Chi tiết phiên, thời gian / `PUT /api/sessions/{id}` |
| 40 | Change duration | Chi tiết phiên, thời gian kết thúc / session update |
| 41 | Lock session | Chi tiết phiên / `POST /api/sessions/{id}/lock` |
| 42 | Unlock session | Chi tiết phiên / `POST /api/sessions/{id}/unlock` |
| 43 | Split session | Chi tiết phiên / `POST /api/sessions/{id}/split` |
| 44 | Detect conflict | Cảnh báo lịch và validation / `POST /api/plans/conflicts` |
| 45 | Reschedule proposal | Lập lại lịch liên quan / `POST /api/plans/reschedule` |
| 46 | Review reschedule | Hộp thoại đề xuất, phiên giữ nguyên được đánh dấu |
| 47 | Apply reschedule | Dùng apply proposal với revision và transaction |
| 48 | Explain decisions | Reason ở phiên, bảng plan và phần phân tích |
| 49 | What-if scenario | Thử kịch bản, before/after + lịch giả định / `/api/plans/scenario` |
| 50 | Start session | Hôm nay/lịch / `POST /api/sessions/{id}/start` |
| 51 | Pause session | Đồng hồ / `POST /api/sessions/{id}/pause` |
| 52 | Resume session | Đồng hồ / `POST /api/sessions/{id}/resume` |
| 53 | Complete session | Xong phiên / `POST /api/sessions/{id}/complete` |
| 54 | Skip session | Bỏ qua / `POST /api/sessions/{id}/skip`; worker ghi missed |
| 55 | Task progress | Chi tiết task / status + progress hoặc progress endpoint |
| 56 | Actual duration | Chi tiết phiên hoàn thành / `PUT /api/sessions/{id}/actual-duration` |
| 57 | Planned vs actual | Hôm nay / Phân tích cá nhân |
| 58 | Personalized estimate | Chi tiết task adjusted minutes, planner dùng bias |
| 59 | Estimation bias | Phân tích / `POST /api/intelligence`, threshold 3 samples |
| 60 | Effective capacity | Phân tích / threshold 5 elapsed sessions |
| 61 | Workload analysis | Hôm nay và Phân tích / intelligence response |
| 62 | Overload risk | Thống kê, reason và thông báo workload |
| 63 | Task risk | Bảng risk theo task, score và reasons |
| 64 | Personal insight | Phân tích bias, capacity, actual/progress |
| 65 | Deadline notification | In-app / worker + `GET /api/notifications` |
| 66 | Upcoming reminder | In-app, trước phiên 15 phút |
| 67 | Workload warning | In-app, dedupe theo ngày |
| 68 | Schedule changed | In-app, sau apply/edit |
| 69 | Search users | Quản trị / `GET /api/admin/users?search=` |
| 70 | Lock account | Quản trị / `POST /api/admin/users/{id}/lock` |
| 71 | Unlock account | Quản trị / `POST /api/admin/users/{id}/unlock` |
| 72 | System status | Quản trị / `GET /api/admin/status` |

UC25/26 chạy bằng quy tắc khi AI không được cấu hình; adapter OpenAI có opt-in, fallback và schema validation nhưng chưa gọi provider thật với API key. Notifications trong phạm vi này là in-app; không triển khai push/đồng bộ calendar ngoài vì tài liệu không bắt buộc. Planner dùng heuristic có giải thích, không tuyên bố tối ưu toàn cục. Admin UI chỉ hiển thị cho role Admin, API vẫn kiểm tra role độc lập.

## ERD v2
Nguồn UML mới được lưu tại docs/reference/UML-v2. UC01–UC72 giữ nguyên. Xem UML_ALIGNMENT.md: bổ sung 16 bảng history/settings/lookup, plan lifecycle/items/reasons, execution segments, observations/bias/capacity/risk, notification preferences và durable job/admin logs.
