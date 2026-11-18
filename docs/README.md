# Tài liệu PLANORA

## Sử dụng và vận hành

- [Hướng dẫn người dùng](guides/USER_GUIDE.md) · [Kịch bản demo](guides/DEMO_SCRIPT.md) · [Ví dụ đồ án sinh viên](guides/planora-demo-sinh-vien.md)
- [Cấu hình và vận hành](guides/SYSTEM_GUIDE.md) · [Build và triển khai](development/INDEPENDENT_BUILD_SAME_ORIGIN.md) · [Nâng cấp database](development/RUNBOOK_NET8_AND_SCHEMA_UPGRADE.md)
- [Cơ sở dữ liệu, sơ đồ và UML gốc](database/README.md) · [Ví dụ API](api/workspace.http) · [Ví dụ API tài khoản](api/requests.http)

## Tính năng

| Chủ đề | Tài liệu |
| --- | --- |
| Công việc | [Inbox, Projects và Hôm nay](features/TASK_SMART_LISTS.md) · [Bảng Eisenhower](features/EISENHOWER_TASK_BOARD.md) |
| Hoạch định | [Lập lịch, phục hồi và ghi nhận bù](features/WORKSPACE_UPGRADE_PHASES_1_TO_4.md) · [Quy tắc điều chỉnh lịch](development/PLANNER_AND_TASK_CONCURRENCY.md) · [Xử lý khi hết giờ](features/WORK_TIME_FOLLOWUP.md) |
| Thời gian và lịch | [Giờ làm theo tuần](features/TIMETABLE_WEEK_ROUTINE.md) · [Nhập ảnh thời khóa biểu](features/NORA_TIMETABLE_IMAGES.md) · [Kéo thả trên lịch](features/CALENDAR_DRAG_DROP.md) |
| NORA và ghi chú | [Cách NORA gọi công cụ](features/NORA_AGENT_RUNTIME.md) · [Luồng ghi chú](features/NOTES_AGENT_GUIDE.md) · [Giọng nói](features/NORA_VOICE.md) |
| Nhóm | [Chỉ định, nhận việc, quyền riêng tư và duyệt kết quả](features/PRIVATE_TEAM_PLANNING.md) |
| Tài khoản và thông báo | [Đăng nhập](features/USERNAME_LOGIN.md) · [Nhắc nhở NORA](features/NORA_REMINDERS.md) · [Telegram](features/TELEGRAM_NOTIFICATIONS.md) |
| Pet và thương mại | [XP, skin và payOS](integrations/PAYOS_PET_SKINS.md) · [Quản trị doanh thu](features/ADMIN_COMMERCE.md) |

## Tích hợp và kiểm thử

- [Google Calendar](integrations/GOOGLE_CALENDAR_SETUP.md) · [Firebase Google Login](integrations/FIREBASE_GOOGLE_LOGIN.md) · [Gmail SMTP](integrations/GMAIL_SMTP.md) · [Vertex AI](integrations/GOOGLE_CLOUD_AI.md)
- [Bảo mật](development/SECURITY.md) · [Kiểm thử NORA](development/NORA_SANDBOX.md) · [CI](../.github/workflows/ci.yml)
- [Thiết kế Getting Started](design/GETTING_STARTED_RESEARCH.md) · [Ảnh và nguồn minh họa đăng nhập](design/login-sprites/README.md)

## Kế hoạch và tài liệu tham khảo

- [NORA chủ động và báo cáo ngày](plans/NORA_PROACTIVE_DAILY_ASSISTANT_2026-11-04.md) · [Đối chiếu phạm vi với TimeHero](plans/PLANORA_TIMEHERO_SCOPE_REVIEW_2026-11-11.md)
- [Rà soát luồng hệ thống 04/11](reviews/SYSTEM_FLOW_REVIEW_2026-11-04.md) · [Rà soát Ghi chú và bằng chứng kiểm thử](reviews/NOTE_FLOW_AUDIT_2026-10-25.md)

Khi cần xác minh hành vi hiện tại, đối chiếu hướng dẫn tính năng với mã nguồn và kiểm thử. Kế hoạch, báo cáo rà soát và bản chụp database phản ánh thời điểm được ghi trong từng tài liệu.
