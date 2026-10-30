# SPWPOS: đối chiếu tài liệu và ERD v2

Ngày cập nhật: 04/10/2026.

## Nguồn yêu cầu

- Yêu cầu Authentication & Account trong cuộc hội thoại: Identity, Argon2id, email OTP bắt buộc, refresh rotation, security stamp, audit, rate limiting.
- Báo cáo dự án 72 use case đã cung cấp: 8 nhóm Account, Task, Time, Planning, Execution, Intelligence, Notification, Administration. Bản trích đọc tại `artifacts/requirements.txt`.
- Bộ UML mới: bản sao nguyên nội dung tại `docs/reference/UML-v2/` gồm Database, Sources, UseCase và Tools; `manifest.json` ghi SHA-256. Tools là mã tham khảo, không được chạy để sửa database.

Use case mới giữ nguyên UC01–UC72. Xem `USE_CASE_COVERAGE.md` để tra giao diện/API cho từng UC. ERD bổ sung yêu cầu lưu lịch sử chi tiết; migration `20261004153305_AlignWithUmlV2` bổ sung 16 bảng vào hệ thống hiện có.

## Ánh xạ toàn bộ bảng ERD

Tên bảng Identity và các bảng đã triển khai được giữ để nâng cấp dữ liệu tại chỗ. Các bảng mới dùng tên trong ERD. Đây là ánh xạ mô hình nghiệp vụ sang SQL Server; file ANSI SQL được gửi không được chạy trực tiếp trên SQL Server (`TIMESTAMP` trong SQL Server là rowversion).

| Bảng UML | Bảng thực tế | Nội dung / khác biệt biểu diễn |
|---|---|---|
| users | AspNetUsers + AspNetRoles + AspNetUserRoles | Guid Identity, email unique; role qua Identity; admin lock qua IsActive, lockout đăng nhập qua LockoutEnd; thời điểm khóa quản trị nằm trong account_status_logs |
| user_profiles | user_profiles | Một profile/tài khoản, DisplayName, TimeZoneId, UpdatedAt; giữ FirstName/LastName trong Identity |
| password_reset_tokens | PasswordResetTickets | Hash vé reset dùng một lần; thêm OTP theo purpose trước khi cấp vé |
| account_status_logs | account_status_logs | Target UserId, AdminId, Action, Reason, CreatedAt; độc lập SecurityAuditLogs |
| weekly_availability | WeeklyAvailability | Ngày API: 0=CN, 1=T2,…6=T7; SQL UML: 1=T2,…7=CN. Phút 0–1440 thay TIME, hỗ trợ 24:00 |
| availability_exceptions | AvailabilityExceptions | Date, StartMinute/EndMinute, IsAvailable, Note; cả ngày = 0–1440, tương đương IsAllDay; không lưu NULL time |
| fixed_events | FixedEvents | Name=title, Description, EventType MEETING/CLASS/APPOINTMENT/OTHER, Start/End |
| work_preferences | WorkPreferences | Unique UserId, min/max, PreferredPeriod, PlanningHorizonDays 1–30; thêm BreakMinutes, giờ ưu tiên, Revision chống đề xuất cũ |
| notification_settings | notification_settings | Bật/tắt in-app, lead deadline 1–168h, lead phiên 1–120min, ngưỡng MODERATE/HIGH/OVERLOAD |
| task_types | task_types | 6 mã PROGRAMMING, WRITING, STUDY, MEETING_PREP, ADMIN, OTHER; FK thật; mã cũ được chuyển tương ứng hoặc giữ trong lookup |
| task_categories | TaskCategories | Unique (UserId,Name), thêm Color cho UI |
| tasks | WorkTasks | Raw estimate, adjusted snapshot/factor, hard/soft deadline, creation source, completed/deleted timestamps; enum trạng thái lưu int, DTO trả tên |
| task_dependencies | TaskDependencies | Unique (UserId,TaskId,PredecessorId), CreatedAt; FK ghép đảm bảo cùng chủ sở hữu |
| work_plans | PlanProposals | Kind, TriggerEvent, Status PROPOSED/APPLIED/DISCARDED/SUPERSEDED, previous plan, horizon, applied time; thêm expiry 30m/revision, PayloadJson snapshot tương thích API |
| plan_items | plan_items | ADD/KEEP/MOVE/RESIZE/REMOVE/UNSCHEDULED; task, previous session, start/end, duration |
| plan_item_reasons | plan_item_reasons | Reason code + JSON dữ kiện; tra `GET /api/plans/{id}/items` |
| plan_warnings | plan_warnings | Cảnh báo gắn proposal/task, giữ cả cảnh báo deadline mềm và thiếu thời gian |
| work_sessions | WorkSessions | Chỉ phiên đã áp dụng; SourceItemId nullable unique, task, thời gian, lock/status; thêm timer aggregate tương thích UI và timestamps |
| session_executions | session_executions | Unique SessionId; started/ended, timer seconds, actual seconds, IsUserAdjusted |
| execution_segments | execution_segments | Mỗi lần start/resume mở một đoạn, pause/complete/skip đóng đoạn; unique một đoạn mở/execution |
| estimation_observations | estimation_observations | Unique TaskId, raw estimate/type snapshot, actual, observed time; tồn tại sau xóa mềm task |
| personal_capacity_profiles | personal_capacity_profiles | Global bias, capacity factor, usable observation count, updated time |
| task_type_biases | task_type_biases | PK UserId+TaskType, factor, average error, sample count |
| capacity_snapshots | capacity_snapshots | Unique UserId+WeekStart (T2 theo timezone); AvailableMinutes = thời gian **đã lên lịch và đến hạn** của completed/skipped/missed, CompletedPlannedMinutes = phần completed |
| risk_assessments | risk_assessments | Task/overload scope, score/ratio, level, computed time; filtered unique một current assessment/user/scope/task |
| risk_reasons | risk_reasons | Reason code + dữ kiện horizon/capacity/workload để giải thích |
| notifications | InAppNotifications | TaskId/SessionId/PlanId FK theo owner, scheduled/created/read timestamps; unique user+dedupe key; SCHEDULE_CHANGE theo ERD |
| system_job_runs | system_job_runs | Notification scan, plan generation, risk recalculation, AI request; SUCCESS/FAILED/RUNNING; lỗi chỉ lưu code/type an toàn |

Ngoài ERD có các bảng bảo mật cần giữ theo yêu cầu Authentication: EmailOtps, RefreshTokens, SecurityAuditLogs, SecurityRateLimits, PasswordChangeNotifications và các bảng Identity claims/logins/tokens. Database có 39 bảng ứng dụng, cộng bảng `__EFMigrationsHistory`.

## Quy tắc R1–R9 được thực thi

1. **R1:** applock theo account + transaction khi apply/move/split/start; kiểm tra session overlap/fixed event; unique một phiên Running/Paused/user.
2. **R2:** weekly windows cộng availability exceptions, trừ unavailable/fixed events theo timezone; lịch đã khóa không bị phá khi đổi giờ rảnh.
3. **R3:** FK owner ghép và kiểm tra đồ thị dependency không có chu kỳ.
4. **R4:** một cấp subtask, deadline con ≤ cha; không cho rút deadline cha qua deadline con; chỉ leaf tasks được xếp và tính workload.
5. **R5:** generate lưu proposal/items/reasons/warnings nhưng chưa tạo calendar session; apply một transaction, bảo vệ phiên khóa/lịch sử, ghi SourceItemId, supersede plan trước, ghi thông báo nếu user bật in-app.
6. **R6:** complete task có actual > 0 ghi một observation/leaf task, giữ raw estimate ban đầu; chỉnh actual cập nhật observation, không sửa timer; soft delete giữ lịch sử. Bias lấy tối đa 50 observations gần nhất/type, weighted tổng actual/tổng estimate, cold start <3 mẫu =1, clamp 0.5–3. Actual=0 không được dùng học, giữ snapshot để hỗ trợ chỉnh lại.
7. **R7:** current risk được thay trong transaction; index unique chặn hai current assessment cùng scope/task. Scenario/conflict check không ghi risk.
8. **R8:** xóa mềm ghi DeletedAt, hủy session Scheduled, giữ completed session/execution/observations; cấm xóa task có timer active.
9. **R9:** scenario và ad-hoc conflict chạy trên state trong bộ nhớ; không ghi task/plan/session/risk/job. Gợi ý NLP/AI chưa xác nhận cũng không tạo task; riêng yêu cầu AI có job vận hành để UC72 theo dõi lỗi.

Capacity cập nhật 28/10/2026: cold start <5 phiên có kết quả =0.8; sau đó EMA alpha .2 của completed planned / total elapsed planned theo từng ngày địa phương, clamp .25–1. NeedsReview chưa xác nhận không bị coi là thất bại. Đây là heuristic theo execution history, không giả định toàn bộ lịch rảnh đều thực hiện được. Snapshot `AvailableMinutes` ghi mẫu planned time; CalendarFreeMinutes được tính riêng từ giờ rảnh. Xem [nâng cấp phases 1–4](../features/WORKSPACE_UPGRADE_PHASES_1_TO_4.md).

Deadline cứng không được vượt. Deadline mềm ưu tiên trước hạn; khi thiếu chỗ có thể đề xuất phiên sau hạn và **luôn** hiển thị cảnh báo trước apply. Buổi ưu tiên là soft preference, có fallback giờ rảnh khác.

## Endpoint và UI bổ sung

- `GET /api/plans/{id}/items`, `POST /api/plans/{id}/discard`: xem quyết định, bỏ đề xuất trong hộp xem trước.
- `GET /api/sessions/{id}/execution`: lịch sử timer trong Chi tiết phiên.
- `PUT /api/notifications/settings`: Tài khoản & nhóm → Cài đặt thông báo.
- `GET /api/admin/jobs`, `GET /api/admin/users/{id}/status-logs`: Quản trị → job/lịch sử khóa mở.
- Profile thêm DisplayName; sự kiện thêm Description/EventType; task thêm IsHardDeadline/CreationSource; preference thêm PreferredPeriod/PlanningHorizonDays.

## Nâng cấp và giới hạn dữ liệu cũ

Migration bổ sung bảng/cột, chuyển mã task type, backfill profile/settings/soft-delete timestamps, quyết định từ JSON proposal cũ, liên kết source item khi xác định được, execution aggregate và observations (kể cả task đã xóa mềm). Deadline của task cũ được giữ là **cứng** để bảo toàn hành vi trước nâng cấp. Các task mới mặc định deadline mềm theo UML.

Timer trước nâng cấp chỉ lưu tổng thời gian; không thể khôi phục các đoạn pause/resume đã mất. Migration giữ aggregate, dùng scheduled Start làm StartedAt lịch sử khi không có dữ liệu chính xác và chỉ nhập đoạn đang mở có SegmentStartedAt thật. Các đoạn sau nâng cấp có thời điểm thực tế đầy đủ. Không dựng dữ liệu quá khứ giả.

Các bản proposal cũ không lưu thời điểm apply, migration dùng CreatedAt làm mốc thay thế. FK dùng Restrict thay cascade vòng để phù hợp SQL Server và bảo vệ audit/lịch sử; service thực thi soft delete và detach category. Enum/int/phút/giây là biểu diễn vật lý khác sơ đồ; DTO chuẩn hóa và không expose Entity.

Không tạo role Admin tự động cho mọi user. Thực hiện bootstrap theo `SYSTEM_GUIDE.md`. AI cần cấu hình thật và consent; nếu chưa cấu hình, UI dùng rules, job AI ghi FAILED thay vì báo thành công giả.

## Xác minh

- `PlanningTests`: deadline cứng/mềm, observation lịch sử, planner invariants.
- `UmlAlignmentTests`: proposal→apply→supersede/discard, timer segments/correction/soft-delete, deadline cha/con, fields mới, notification opt-out, failed jobs, scenario không ghi dữ liệu.
- `UmlMigrationTests`: nâng cấp database SQL Server có dữ liệu schema cũ, kiểm tra giữ Id/raw estimate/timer/profile/plan/observations. Chỉ sử dụng database `SPWPOS_Tests_<random>`.
- Các test Authentication, WorkspaceFlow và SQL concurrency hiện có phải tiếp tục đạt. Xem kết quả thực tế tại `VERIFICATION.md`.

