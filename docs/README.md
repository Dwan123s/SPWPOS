# Planora documentation / Tài liệu Planora

Start here to find guides, database diagrams, setup instructions, and project records. Paths in commands are relative to the repository root unless stated otherwise.

Bắt đầu tại đây để tìm hướng dẫn, sơ đồ cơ sở dữ liệu, cấu hình và hồ sơ dự án. Đường dẫn trong lệnh được tính từ thư mục gốc repository, trừ khi có ghi chú khác.

## Database diagrams / Sơ đồ cơ sở dữ liệu

The current export contains **45 tables, 356 columns, and 69 declared foreign keys**, captured **18 October 2026 at 15:26 UTC**. It is a snapshot; regenerate after schema changes.

Bản xuất hiện tại có **45 bảng, 356 cột và 69 khóa ngoại**, chụp lúc **22:26 ngày 18/10/2026 (UTC+7)**. Đây là bản chụp; cần tạo lại khi cấu trúc database thay đổi.

| View / Chế độ xem | English | Tiếng Việt |
| --- | --- | --- |
| Grouped map PNG / Ảnh nhóm chức năng | [Open image](database/current/images/map.png) | [Mở ảnh](database/current/images/map.vi.png) |
| Every column PNG / Ảnh đầy đủ cột | [Open image](database/current/images/complete.png) | [Mở ảnh](database/current/images/complete.vi.png) |

- [Interactive explorer / Sơ đồ tương tác](database/current/index.html): select tables, inspect definitions, highlight relationships, search, zoom, and switch language.
- [Function-group images / Ảnh từng nhóm chức năng](database/current/images/README.md): readable detail exports in both languages.
- [Database sources and history / Nguồn và lịch sử database](database/README.md).
- [Regenerate diagrams and images / Tạo lại sơ đồ và ảnh](../scripts/database-diagram/README.md).

The grouped image keeps every table and relationship with eight-column previews. The full-column image preserves all 356 columns. SQL identifiers remain unchanged in both languages.

Ảnh nhóm giữ mọi bảng và quan hệ, xem trước tối đa tám cột mỗi bảng. Ảnh đầy đủ cột giữ toàn bộ 356 cột. Tên đối tượng SQL được giữ nguyên trong cả hai ngôn ngữ.

## Guides / Hướng dẫn

- [User guide / Hướng dẫn người dùng](guides/USER_GUIDE.md)
- [System setup and operation / Cấu hình và vận hành](guides/SYSTEM_GUIDE.md)
- [Workspace interface / Giao diện không gian làm việc](guides/WORKSPACE_UI_GUIDE.md)
- [Username and email login / Đăng nhập](features/USERNAME_LOGIN.md)
- [Notes and scheduling agent / Ghi chú và agent](features/NOTES_AGENT_GUIDE.md)
- [Planning and chat agent / Agent lập kế hoạch](features/PLANNING_AGENT_GUIDE.md)
- [NORA voice / Giọng nói NORA](features/NORA_VOICE.md)
- [Tasks and reminders / Công việc và nhắc nhở](features/PLANORA_TASK_AND_REMINDER_UPDATE.md)

## Integrations / Tích hợp

- [Gmail SMTP](integrations/GMAIL_SMTP.md)
- [Firebase Google login / Đăng nhập Google](integrations/FIREBASE_GOOGLE_LOGIN.md)
- [Google Cloud AI](integrations/GOOGLE_CLOUD_AI.md)
- [PayOS decisions / Quyết định tích hợp PayOS](integrations/PAYOS_INTEGRATION_DECISIONS.md)

## Development / Phát triển

- [Security / Bảo mật](development/SECURITY.md)
- [Team development plan / Kế hoạch nhóm](development/TEAM_DEVELOPMENT_PLAN.md)
- [Verification record / Hồ sơ kiểm chứng](development/VERIFICATION.md)
- [NORA automated sandbox / Chạy và xuất kết quả kiểm thử NORA](development/NORA_SANDBOX.md)
- [Use-case coverage / Phạm vi use case](development/USE_CASE_COVERAGE.md)
- [.NET 8 migration](development/NET8_MIGRATION_2026-10-06.md)
- [React migration](development/REACT_MIGRATION_2026-10-06.md)
- [Runtime and schema upgrade runbook / Quy trình nâng cấp](development/RUNBOOK_NET8_AND_SCHEMA_UPGRADE.md)
- [API examples](api/requests.http) · [Workspace API examples](api/workspace.http)

## Design / Thiết kế

- [Getting Started research](design/GETTING_STARTED_RESEARCH.md)
- [Website research](design/WEBSITE_RESEARCH_2026-10-07.md) · [UI research](design/UI_RESEARCH_2026-10-05.md)
- [Brand](design/BRAND_UI_2026-10-05.md) · [Liquid Glass](design/LIQUID_GLASS_UI_2026-10-06.md)
- [Login](design/LOGIN_UI_2026-10-06.md) · [Mobile](design/MOBILE_UI_2026-10-06.md)
- [NORA chat](design/NORA_CHAT_RESEARCH.md) · [Chatbot UI](design/NORA_CHATBOT_UI_RESEARCH.md)
- [Mascot frames and prompts](design/NORA_MASCOT_FRAMES.md) · [Login artwork](design/login-sprites/README.md)
- [NORA concept](design/nora-assistant-concept-v1.png) · [Design prompt](design/nora-assistant-design-prompt.txt)

## Plans and reviews / Kế hoạch và đánh giá

- [Upgrade plan](plans/PLANORA_UPGRADE_PLAN.md) · [AI agent implementation plan](plans/AI_AGENT_IMPLEMENTATION_PLAN.md)
- [Remediation plan](plans/PLANORA_REMEDIATION_PLAN_2026-10-15.md) · [Fix progress](plans/PLANORA_FIX_PROGRESS.md)
- [Remediation prompt](prompts/PLANORA_FULL_REMEDIATION_PROMPT.md)
- [System review, 15 October](reviews/SYSTEM_REVIEW_2026-10-15.md) · [UI review, 15 October](reviews/UI_REVIEW_2026-10-15.md)
- [NORA response quality, 18 October](reviews/NORA_REQUEST_RESPONSE_QUALITY_REPORT_2026-10-18.md) · [Original screenshots](evidence/nora-quality-2026-10-18/)
- [NORA guardrail and scheduling test prompts, 19 October / Bộ prompt kiểm thử NORA](reviews/NORA_GUARDRAIL_AND_SCHEDULING_TEST_PACK_2026-10-19.md)
- [NORA automated sandbox evaluation, 19 October / Kết quả đánh giá sandbox](reviews/NORA_SANDBOX_EVALUATION_2026-10-19.md)
- [NORA sandbox fixes and verification, 19 October / Bản vá và kiểm chứng](reviews/NORA_SANDBOX_PATCH_VERIFICATION_2026-10-19.md)
- [Notes review, 18 October / Đánh giá phần Ghi chú](reviews/NOTE_REVIEW_2026-10-18.md)
- [Earlier system review](reviews/SYSTEM_REVIEW.md) · [5 October review](reviews/REVIEW_2026-10-05.md) · [5 October fixes](reviews/FIX_2026-10-05.md)

Reviews and plans describe their recorded dates and may include proposed or superseded behavior. Original evidence and the [UML v2 reference bundle](reference/UML-v2/) are retained separately from the current database export.

Báo cáo và kế hoạch phản ánh thời điểm ghi nhận, có thể chứa đề xuất hoặc thông tin lịch sử. Bằng chứng gốc và [bộ UML v2 tham khảo](reference/UML-v2/) được lưu riêng với bản xuất database hiện tại.
