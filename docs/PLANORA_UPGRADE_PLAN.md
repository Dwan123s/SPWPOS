# Kế hoạch nâng cấp PLANORA

Ngày: 14/10/2026. Phạm vi lượt triển khai đầu: giai đoạn 1.

## Hiện trạng được xác minh từ mã nguồn

| Phần | Đã có | Khoảng trống |
|---|---|---|
| Thời gian | WeeklyAvailability, AvailabilityException, FixedEvent; kiểm tra xung đột phiên khóa | Chưa tự xếp lại phiên linh hoạt khi đổi thời gian; chưa có giới hạn tổng phút/ngày hoặc sự kiện lặp theo tuần |
| Công việc/lịch | Task/subtask, phụ thuộc, deadline, proposal/revision, phiên khóa, thực thi và thời gian nghỉ | Cần nối thay đổi thời gian với bộ lập lịch, bảo toàn lịch sử |
| AI | TaskInterpreter (Gemini/OpenAI), note được phân tích thành task và xếp lịch | Note chưa cập nhật lịch rảnh/sự kiện; chưa có hội thoại đa lượt, preview nguyên tử hoặc hoàn tác |
| Thống kê | Phân tích workload, bias, risk, lịch sử thực thi và PlanItem | Chưa có báo cáo lịch sử 3/7/30 ngày bằng baseline ổn định |
| Quản trị | Tìm user, khóa/mở tài khoản, thu hồi phiên, job/status | Chưa có quản trị nội dung pet, skin, ngày lễ hoặc tài chính |
| Pet/payment | Chưa có nghiệp vụ XP, pet, skin, xu hoặc đơn thanh toán | Xây mới sau khi phần lập lịch ổn định |

## Backlog và tiêu chí nghiệm thu

### Giai đoạn 1 — Thời gian và chat agent

- Mở rộng WorkPreference: tổng phút tối đa/ngày và tùy chọn tự điều chỉnh khi đổi thời gian.
- Sự kiện lặp theo tuần: tạo các lần xuất hiện hữu hạn, kiểm tra toàn bộ trước khi lưu; người dùng sửa/xóa từng lần.
- AgentTurn lưu yêu cầu, hội thoại, khóa chống trùng, lệnh đã hiểu, kết quả, revision và thông tin hoàn tác. Không giữ transaction trong lúc đợi AI.
- AI đọc snapshot tối thiểu theo tài khoản; trả dữ liệu có cấu trúc; backend kiểm tra và lập lịch xác định.
- API: GET /api/agent/capabilities; GET /api/agent/turns; POST /api/agent/turns; POST /api/agent/turns/{id}/apply; POST /api/agent/turns/{id}/undo.
- Preview không tạo task/sự kiện/phiên thật. Áp dụng kiểm tra revision, thời gian, quyền sở hữu và các ràng buộc trong một transaction.
- Tự dời các phiên tương lai linh hoạt bị xung đột; giữ phiên đang chạy, tạm dừng, khóa, lịch sử đã làm và các phiên ngoài phạm vi.
- Chat tiếng Việt có lịch sử, trạng thái chờ/lỗi, xem trước hoặc tự áp dụng, bảng thời gian và tóm tắt trước/sau.
- Nghiệm thu: họp 14–16h; báo cáo liên tục 120 phút trước 18h (khi giới hạn phiên cho phép); không đủ giờ báo thiếu; không trùng khi retry; preview/apply cũ bị từ chối; không truy cập chéo tài khoản; hoàn tác bị từ chối nếu dữ liệu đã thay đổi hoặc phiên đã bắt đầu.

### Giai đoạn 2 — Lịch sử, tự điều chỉnh và ngày lễ

- Baseline ngày/phiên: lưu kế hoạch trước thời điểm thực hiện; bảng thay đổi nối phiên cũ/mới. Không suy ra lịch sử từ lịch hiện tại.
- GET /api/progress?from=&to=, bộ lọc 3/7/30 ngày; báo cáo từng ngày, estimate/actual, đúng hạn, bỏ lỡ; ngày không kế hoạch không ghi 100%.
- AutoAdjustmentAttempt có khóa chống trùng theo phiên/lần đánh giá; worker xử lý phiên trễ >=48h và rủi ro deadline ngay; chỉ xếp phần còn lại, không tự đổi deadline. Chống dời vô hạn.
- VietnamEventDefinition/Occurrence theo năm, nguồn và trạng thái xác minh; quy đổi lịch âm được kiểm thử; nghỉ bù cập nhật từ nguồn chính thức.
- UserHolidayPolicy tách sự kiện thông tin và khoảng bận; nhắc mặc định 20h ngày trước; outbox/dedupe để worker chạy khi đóng trình duyệt.

### Giai đoạn 3 — Streak, XP và pet

- DailyGoalSnapshot/StreakDay, XpLedger, UserPet, PetDefinition/PetEvolution; unique reward key.
- Mốc tiến hóa 1/5/10/15/20/50/100. Cấu hình XP/level có phiên bản; thưởng từ hoàn thành hợp lệ, không từ đổi lịch.
- Đề xuất ban đầu: XP theo phút dự kiến đủ điều kiện, giới hạn 240 XP/ngày; task cha chỉ nhận thưởng qua task lá; mỗi task/phiên không tính trùng. Chốt công thức bằng mô phỏng trước khi áp dụng.
- Ngày nghỉ khai báo trước không tăng/mất streak; không giảm level khi bỏ lỡ; mua skin không tăng XP.
- Trang pet, tiến hóa, inventory, animation; kiểm thử reopen/complete, parent/child, múi giờ và chỉnh sửa lịch sau khi nhận thưởng.

### Giai đoạn 4 — Skin, xu, thanh toán và admin

- Skin, SkinOwnership, CoinPackage, PaymentOrder, PaymentEvent, CoinLedger, SkinPurchase; unique provider transaction/order key.
- Luồng server định giá -> tạo đơn -> cổng thanh toán -> webhook xác thực -> đối chiếu -> transaction cộng xu. Adapter và fake transport phục vụ kiểm thử; khảo sát tài liệu payOS hiện hành trước tích hợp thật.
- Xu không chuyển giữa user/không rút tiền trong phạm vi này. XP và xu tách biệt.
- Chống webhook trùng, mua đồng thời, số dư âm; xử lý đơn hết hạn nhưng nhận tiền muộn, sai tiền, hoàn tiền và đối soát thủ công có nhật ký.
- Admin roles theo tài khoản/nội dung/tài chính; sổ tiền thực thu/hoàn tiền riêng với sổ xu phát hành/tiêu; không sửa số dư trực tiếp.
- Nghiệm thu payment bằng kiểm thử giả/sandbox trước; giao dịch thật cần cấu hình merchant và triển khai webhook HTTPS.

## Nguyên tắc migration và vận hành

- Migration cộng thêm, giữ dữ liệu cũ; default tương thích cho giới hạn mỗi ngày và tự điều chỉnh.
- Reuse account transaction lock, revision, PlanningEngine và PlanItem; không để AI truy cập SQL hoặc API tùy ý.
- AI key chỉ ở server. Gửi task/lịch/hội thoại cần thiết, không gửi email/token/thông tin đăng nhập.
- Tests dùng AI giả; không gửi dữ liệu người dùng hoặc phát sinh phí AI/payment.
- Báo cáo cuối giai đoạn phải phân biệt code đã có, checks đã chạy và tính năng vẫn nằm trong backlog.

## Kết quả giai đoạn 1 — 14/10/2026

Đã triển khai từ giao diện đến service, API và migration:

- Chat tiếng Việt, lịch sử theo tài khoản, nhiều hội thoại và ngữ cảnh 8 lượt gần nhất.
- Thêm/sửa lịch rảnh, ngoại lệ ngày, sự kiện; sự kiện lặp hàng tuần hữu hạn; tạo/cập nhật task và xếp lịch.
- Preview không tạo dữ liệu lịch thật; áp dụng nguyên tử, kiểm tra revision, deadline, giới hạn, xung đột và quyền sở hữu.
- Chống retry trùng, hoàn tác hợp lệ, bảng thay đổi trước/sau, thời gian theo múi giờ tài khoản.
- Form tự xếp lại phiên tương lai linh hoạt bị ảnh hưởng; có thiết lập tắt tự điều chỉnh và thông báo.
- Giới hạn tổng phút/ngày; chat giữ thời lượng đã xác định, không tự nhân bias/năng lực hoặc nâng giới hạn phiên.
- Migration AddPlanningAgent và script SQL idempotent đã được tạo; không thay đổi database development trong lượt này.

Kiểm tra đã chạy:

| Kiểm tra | Kết quả |
|---|---|
| .NET Release build | Qua, 0 warning / 0 error |
| Unit tests | 177 qua |
| HTTP integration tests | 93 qua; 10 ca SQL Server bỏ qua do chưa có SPWPOS_TEST_SQLSERVER |
| React/Vitest | 178 qua |
| Browser session-refresh | 4 qua |
| Frontend production build | Qua |
| EF pending model changes | Không có thay đổi chưa được migration hóa |
| SQL migration script | Sinh thành công; chưa thực thi trên SQL Server thật |
| Diff whitespace | Qua |

Tổng: 452 kiểm thử qua. AI được kiểm thử bằng phản hồi giả; chưa gọi provider thật, chưa phát sinh phí AI hoặc thanh toán.

Trên môi trường này, chạy xUnit runner trong tiến trình hiện tại và Vitest bằng worker threads để tránh hạn chế giao tiếp của test runner. Test host dùng data protection tạm trong bộ nhớ, không tạo khóa trong hồ sơ Windows của người dùng.

Hướng dẫn dùng và cập nhật database: [PLANNING_AGENT_GUIDE.md](PLANNING_AGENT_GUIDE.md). Giai đoạn 2–4 vẫn là backlog, chưa được triển khai.

### Bật AI thật sau giai đoạn 1

Theo yêu cầu tiếp theo ngày 14/10/2026:

- Xác minh model Gemini `gemini-3.5-flash-lite` bằng khóa đã có, không thay hoặc in khóa.
- Hai yêu cầu thực qua chính TaskInterpreter/PlanningAgentInterpreter đều qua: cuộc họp ngày 15/10/2026 từ 14h–16h theo Asia/Ho_Chi_Minh; báo cáo SWP liên tục 120 phút trước 18h, được PlanningEngine xếp vào 14h–16h. Không lưu dữ liệu mẫu vào lịch.
- Thêm `scripts/Test-LiveAi.ps1` và chế độ `--check-ai` để kiểm tra lại chủ động. Phân biệt lỗi quyền/API key, model, quota, định dạng và dịch vụ AI.
- Áp dụng migration AddPlanningAgent vào đúng database SPWPOS tại 127.0.0.1:11433 và nạp lại server Release tại https://localhost:7043.
- Kiểm tra HTTP: trang 200; endpoint agent 401 khi không có đăng nhập, xác nhận route hoạt động và vẫn yêu cầu xác thực.
- Release build qua; 7 kiểm thử adapter agent qua sau thay đổi thông báo lỗi.
- Cấu hình đang dùng Gemini Developer API/AI Studio. Chưa chuyển sang Vertex AI hoặc thay đổi billing account; cần Project ID/nguồn xác thực Cloud để thực hiện việc chuyển.

## Google Cloud adapter - 14/10/2026

**Cập nhật 15/10/2026:** Google Cloud đã kích hoạt thành công bằng API key. Ba kiểm tra AI thật (meeting, SWP/planner, note) đạt, cấu hình VertexAI đã lưu và backend nạp lại trả HTTP 200. Model gemini-3.5-flash-lite, project planora-511614, location global, ExpressMode=true. Bản sửa gửi query key đã mã hóa cùng header, tắt HTTP logging của client AI; 200 unit tests và kiểm tra script offline đạt. Chưa đối chiếu khấu trừ credit trong Cloud Billing. Các ghi chú chờ xác thực bên dưới là lịch sử trước khi kích hoạt.

Đã thêm provider VertexAI cho chat, note, capture và breakdown; hỗ trợ Cloud API key riêng, ADC, endpoint global/regional và express mode được chọn rõ ràng. Cấu hình project planora-511614, giữ model gemini-3.5-flash-lite. Script Configure-VertexAI nhập key ẩn, kiểm tra Cloud bằng dữ liệu mẫu trước khi lưu User Secrets và nạp lại API. Chưa xác nhận gọi Cloud thật: cửa sổ cấu hình đang chờ key do người dùng tạo.

Xác minh: Release build 0 warning/0 error; locked restore đạt; 192 unit + 93 HTTP integration + 179 frontend + 4 browser tests đạt (468). 10 kiểm tra SQL thực bỏ qua vì chưa cấu hình SPWPOS_TEST_SQLSERVER. Frontend production build đạt; server nạp lại trả trang chính 200 và agent endpoint 401 khi chưa đăng nhập. Xem [GOOGLE_CLOUD_AI.md](GOOGLE_CLOUD_AI.md).

### ADC theo chính sách tổ chức

Agent Studio báo API Keys are Disallowed. Đã chuyển bước xác thực sang ADC, giữ nguyên policy. Chuẩn bị Google Cloud CLI 589.0.0 từ bản ZIP chính thức, xác minh SHA256 b9c2f4f296154f55e8a31a6979106573bc1ad8691041d4a32ea90c1834fdf995 và chạy --version đạt. Script Connect-GoogleCloudAdc mở đăng nhập Google để người dùng hoàn tất; chưa xác nhận AI Cloud khi đăng nhập chưa xong.
