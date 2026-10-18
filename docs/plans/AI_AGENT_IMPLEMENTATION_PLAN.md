# Kế hoạch tích hợp AI agent vào PLANORA

Ngày lập: 07/10/2026, múi giờ Asia/Saigon.

## 1. Mục tiêu và phạm vi bản đầu

Xây dựng một **agent lập kế hoạch** để người dùng yêu cầu bằng tiếng Việt, ví dụ:

> “Lập lịch từ thứ Hai đến Chủ nhật tuần tới, ưu tiên báo cáo, mỗi ngày tối đa 3 giờ và giữ nguyên các phiên đã khóa.”

Agent đọc dữ liệu thuộc tài khoản, làm rõ yêu cầu, gọi công cụ mô phỏng lịch, đánh giá kết quả và trả về đề xuất có giải thích. Người dùng xem trước và bấm **Áp dụng kế hoạch** để cập nhật lịch.

Phạm vi MVP:

- Tạo kế hoạch cho phạm vi 1–30 ngày và điều chỉnh các phiên tương lai đủ điều kiện.
- Hiểu phạm vi ngày, công việc muốn ưu tiên, giới hạn phút làm việc mỗi ngày và khung giờ muốn tránh.
- Giải thích các phiên thay đổi, công việc chưa xếp được và ràng buộc chưa thể đáp ứng.
- Hỏi lại khi tên công việc trùng nhau, thời gian mơ hồ hoặc yêu cầu chưa được hỗ trợ.
- Giữ quy tắc hiện tại về deadline, phụ thuộc, phiên khóa, phiên đang thực hiện và sự kiện cố định.
- Hoạt động theo yêu cầu của người dùng; chưa có agent tự chạy nền hoặc tự áp dụng lịch.

Agent phân rã công việc, agent theo dõi tiến độ và agent tư vấn workload là các giai đoạn tiếp theo. Các vai trò này chưa cần triển khai thành nhiều agent trao đổi với nhau trong MVP.

## 2. Nền tảng đã có và phần cần bổ sung

| Thành phần hiện tại | Khả năng tái sử dụng | Phần cần làm |
|---|---|---|
| `TaskInterpreter.cs` | Adapter OpenAI, cấu hình AI, xử lý lỗi và kiểm tra dữ liệu trả về | Tách adapter dùng chung; bổ sung giao thức gọi công cụ cho agent |
| `PlanningEngine.cs` | Lập lịch xác định, phụ thuộc, khoảng rảnh, ước lượng và phân tích workload | Thêm ràng buộc theo yêu cầu; kiểm tra kết quả ứng viên |
| `PlanService.cs` | Lưu proposal, review, apply/discard, hết hạn 30 phút và kiểm tra revision | Lưu ràng buộc của proposal; kiểm tra snapshot trước khi lưu và ràng buộc khi apply |
| `IWorkspaceService` và các controller | Truy xuất dữ liệu theo tài khoản; API lập lịch, reschedule và phân tích | Thêm service ngữ cảnh tối thiểu và API điều phối agent |
| `frontend/src/calendar.jsx` và `controller.jsx` | Luồng xem trước và áp dụng kế hoạch | Thêm giao diện yêu cầu agent và xử lý trạng thái |
| `JobHistoryService.cs` | Ghi trạng thái job | Bổ sung metadata vận hành của lần chạy agent |

**Khoảng trống cần xử lý:** `PlanInput` hiện chưa có giới hạn tổng phút mỗi ngày, khung giờ loại trừ hay ưu tiên tạm thời. `DetectConflictsAsync` hiện kiểm tra các phiên đã lưu; cần một hàm kiểm tra riêng cho lịch ứng viên. Các yêu cầu mới phải được thể hiện trong dữ liệu và thuật toán, không chỉ trong prompt.

## 3. Kiến trúc đề xuất

Luồng chính:

`Giao diện → AgentController → PlanningAgentService → AI adapter và các công cụ → PlanningEngine → proposal → giao diện preview → API apply hiện có`.

- AI chuyển yêu cầu thành dữ liệu có cấu trúc, chọn công cụ và giải thích kết quả.
- Orchestrator .NET thực thi công cụ trong danh sách cho phép, quản lý vòng lặp và ngân sách.
- `PlanningEngine` tạo lịch và thực thi ràng buộc nghiệp vụ.
- Công cụ gọi trực tiếp service backend; không để model chọn URL tùy ý hoặc gọi SQL.
- Danh tính tài khoản lấy từ phiên đăng nhập. Model không được truyền `UserId` để đổi tài khoản.
- Các vòng mô phỏng chạy trên snapshot và chưa lưu proposal. Khi kết thúc, backend lưu một proposal được kiểm tra, rồi trả `PlanId`.

Giới hạn đề xuất cho MVP, có thể điều chỉnh sau đo thử: tối đa 4 vòng model, 10 lần gọi công cụ, 30 giây cho toàn bộ lần chạy và số token có cấu hình. Nếu hết giới hạn, chỉ trả ứng viên đã kiểm tra hoặc thông báo cần thử lại; không trả lịch chưa xác thực.

Không giữ transaction hay khóa tài khoản trong thời gian chờ provider. Kiểm tra revision snapshot trước khi lưu proposal; kiểm tra lại khi người dùng áp dụng.

## 4. Các công cụ của agent

| Công cụ đề xuất | Chức năng | Tác động dữ liệu |
|---|---|---|
| `get_planning_context` | Lấy task cần thiết, dependency, lịch rảnh, sự kiện và phiên liên quan trong phạm vi | Chỉ đọc |
| `simulate_plan` | Tạo ứng viên với ngày, phạm vi, công việc ưu tiên và ràng buộc đã xác thực | Không cập nhật lịch |
| `evaluate_candidate` | Kiểm tra trùng giờ, deadline, phụ thuộc, giới hạn mỗi ngày và khả năng đáp ứng | Chỉ tính toán |
| `analyze_workload` | Tính tải, khả năng thực hiện và rủi ro từ engine | Chỉ tính toán |

Agent có thể thay đổi tham số mô phỏng sau khi đọc kết quả đánh giá, trong giới hạn người dùng yêu cầu. Ràng buộc cứng không được tự nới lỏng; nếu không khả thi, trả rõ phần thiếu và phương án để người dùng chọn.

Thao tác lưu proposal do orchestrator thực hiện sau khi kiểm tra. Agent không có công cụ áp dụng lịch, xóa công việc, thay đổi hồ sơ hoặc gửi email.

## 5. Hợp đồng dữ liệu và API

Tên sau là đề xuất, chưa tồn tại trong code:

- `AgentPlanningInput`: yêu cầu người dùng, đồng ý gửi dữ liệu AI, phạm vi tùy chọn và khóa chống gửi trùng.
- `PlanningConstraints`: ngày bắt đầu/kết thúc, giới hạn phút mỗi ngày, khung giờ loại trừ và ID task ưu tiên.
- `AgentPlanningResult`: `runId`, trạng thái, yêu cầu đã hiểu, câu hỏi làm rõ, ràng buộc, `planId`, cảnh báo và giải thích.
- `PlanningAgentService`, `IPlanningAgent`, `IPlanningAiClient` và bộ DTO công cụ.

API dự kiến:

- `POST /api/agent/planning`: chạy agent; trả đề xuất, câu hỏi làm rõ hoặc thông báo AI không khả dụng.
- `GET /api/plans/{id}`: xem lại đề xuất như luồng hiện tại.
- `POST /api/plans/{id}/apply`: người dùng áp dụng sau khi xem trước.
- `POST /api/plans/{id}/discard`: bỏ đề xuất.

MVP dùng yêu cầu đồng bộ có timeout và hỗ trợ hủy. Chỉ bổ sung hàng đợi hoặc streaming nếu đo thực tế cho thấy cần thiết.

Ràng buộc là tùy chọn để giữ tương thích luồng lập lịch cũ. Lưu bản ràng buộc cùng proposal để áp dụng lại đúng điều kiện. Ưu tiên tạm thời chỉ tác động proposal, không sửa `WorkTask.Priority` hoặc preferences gốc.

Giới hạn phút/ngày tính theo múi giờ người dùng, bao gồm các phiên được giữ trong phạm vi. Nếu các phiên khóa đã vượt giới hạn, trả xung đột cụ thể; không di chuyển phiên khóa hoặc coi yêu cầu đã được đáp ứng. Deadline cứng và dependency vẫn được bảo toàn khi ưu tiên task.

Ngày tương đối như “tuần tới” được chuyển thành ngày cụ thể trên server theo múi giờ người dùng và hiển thị lại trước khi tạo đề xuất.

## 6. Giao diện

Thêm nút **Lập lịch với AI** trên dashboard hoặc trang lịch. Panel gồm ô yêu cầu, ví dụ ngắn và lựa chọn đồng ý gửi dữ liệu cần thiết đến AI.

Luồng:

1. Người dùng nhập yêu cầu và chọn tạo đề xuất.
2. Hiển thị loading, cho phép hủy và chặn gửi trùng.
3. Nếu thiếu thông tin, hiển thị câu hỏi làm rõ cùng lựa chọn dễ hiểu.
4. Hiển thị các điều kiện đã hiểu, phạm vi ngày, lịch preview và các phiên được thêm/dời/giữ.
5. Hiển thị cảnh báo, công việc chưa xếp được và lý do dựa trên dữ liệu của engine.
6. Người dùng chọn **Áp dụng**, **Điều chỉnh yêu cầu** hoặc **Bỏ đề xuất**.

Nếu AI tắt hoặc provider lỗi, hiển thị lựa chọn chuyển sang form lập lịch hiện tại. Không tự bỏ qua giới hạn người dùng nhập để tạo một lịch có vẻ thành công. Trạng thái tiến trình chỉ phản ánh bước backend đã thực hiện; không hiển thị phần trăm giả.

## 7. Kiểm soát cần có trong triển khai

- Chỉ gửi trường phục vụ lập lịch; không gửi email, mật khẩu, token, avatar hoặc thông tin quản trị.
- Chặn việc truy xuất hoặc lưu proposal của tài khoản khác ở từng công cụ.
- Coi tên/mô tả task là dữ liệu; nội dung đó không thể thay đổi danh sách công cụ hoặc quyền thực thi.
- Kiểm tra schema và giá trị tham số công cụ; ID task phải tồn tại và thuộc tài khoản.
- Áp dụng hạn mức theo IP và tài khoản, giới hạn context, token, vòng lặp và concurrency.
- Khóa chống gửi trùng phải gắn với tài khoản và payload; retry không tạo nhiều proposal tương đương.
- Lưu metadata gồm thời lượng, số công cụ, token nếu provider trả về và mã kết quả. Không log API key hay toàn bộ prompt/phản hồi có dữ liệu cá nhân.
- Giải thích phải khớp mã lý do và cảnh báo thực tế; không để model tuyên bố đáp ứng ràng buộc khi bộ kiểm tra trả lỗi.
- Kết quả mô phỏng dựa trên snapshot cũ phải được tạo lại trước khi lưu hoặc apply.

## 8. Lộ trình và phân công

Ước lượng **10–15 ngày làm việc** cho MVP nếu 4 thành viên phối hợp và nền tảng hiện tại vẫn ổn định. Đây là ước lượng, chưa tính thời gian chờ API key, hạn mức provider hoặc thay đổi yêu cầu. Không bao gồm triển khai các agent tiếp theo.

| Giai đoạn | Thời gian dự kiến | Chủ trì | Kết quả bàn giao |
|---|---|---|---|
| 1. Chốt phạm vi và DTO | Ngày 1–2 | TV1 + TV3 | Các yêu cầu được hỗ trợ, tool schemas, ownership, hợp đồng UI/API và dữ liệu test |
| 2. Bổ sung ràng buộc planner | Ngày 3–5 | TV3 | Mô phỏng, giới hạn mỗi ngày, ưu tiên tạm thời, kiểm tra ứng viên và lưu/apply ràng buộc |
| 3. Agent và AI adapter | Ngày 3–6 | TV2 | Vòng gọi công cụ, câu hỏi làm rõ, giới hạn chạy và xử lý provider lỗi |
| 4. Giao diện và vận hành | Ngày 5–8 | TV1 + TV4 | Panel yêu cầu, preview/apply, hủy, chống gửi trùng và metadata lần chạy |
| 5. Tích hợp và đánh giá | Ngày 9–12 | Cả nhóm, TV4 điều phối | Kiểm thử engine, agent, SQL concurrency, UI và gọi provider thật có cấu hình |
| 6. Sửa lỗi và demo | Ngày 13–15 | Cả nhóm | Tài liệu, bộ tình huống demo, cấu hình bật/tắt và báo cáo độ trễ/chi phí đo được |

TV1 phụ trách auth, ownership, API và UI dùng chung. TV2 phụ trách điều phối agent và diễn giải yêu cầu. TV3 sở hữu thuật toán và tính đúng của lịch. TV4 phụ trách phân tích workload, metadata và điều phối kiểm thử. Chủ phần triển khai vẫn viết test cho phần của mình.

Tách controller/DTO agent thành file mới; hạn chế sửa đồng thời các file lớn như `WorkspaceControllers.cs`, `Contracts.cs`, `controller.jsx`. Merge theo thứ tự: contracts → planner/tools → agent → UI → tích hợp. Chủ feature tạo migration nếu cần; phối hợp migration và snapshot với TV1.

## 9. Kiểm thử và điều kiện hoàn thành

| Nhóm kiểm thử | Tình huống bắt buộc |
|---|---|
| Đúng lịch | Dependency, hard deadline, phiên khóa/đang chạy, overlap, sự kiện cố định và giờ rảnh |
| Ràng buộc mới | Tối đa 180 phút/ngày, phiên giữ vượt giới hạn, ngày theo timezone, khoảng tránh và ưu tiên task |
| Diễn giải | Tên trùng, ngày mơ hồ, yêu cầu không hỗ trợ và phân biệt điều kiện cứng với gợi ý |
| Agent | Gọi công cụ đúng luồng; đọc kết quả và chỉnh ứng viên; tool không hợp lệ; vượt số vòng; hủy/timeout |
| Dữ liệu và quyền | Cross-user ID, prompt injection trong task, context tối thiểu và không lộ secret trong log |
| Proposal/apply | Không đổi lịch trước xác nhận; snapshot stale, hết hạn, apply trùng và gửi trùng request |
| Provider/UI | AI tắt, thiếu cấu hình, provider lỗi; loading/error/empty state; mobile và chỉnh lại yêu cầu |

Test mặc định dùng AI client giả với phản hồi xác định. Test provider thật chạy riêng, opt-in và dùng dữ liệu demo, không thay thế kiểm thử invariant của engine.

MVP hoàn thành khi:

- Các tình huống trên pass; lịch được apply luôn đi qua validation backend.
- Một lần yêu cầu có thể đọc context, mô phỏng, đánh giá và trả proposal; tình huống cần chỉnh lịch chứng minh được vòng công cụ tiếp theo.
- Agent không áp dụng lịch; chỉ thao tác xác nhận của người dùng mới gọi API apply.
- Không sửa task/preferences gốc để đáp ứng yêu cầu tạm thời.
- Chạy bộ đánh giá cố định ít nhất 20 yêu cầu tiếng Việt; ghi nhận diễn giải đúng, trường hợp hỏi lại và cảnh báo không khả thi.
- Đo thời gian, số lần gọi model và token trên bộ demo để chốt hạn mức thực tế trước khi bật rộng rãi.
- Có thể tắt tính năng bằng cấu hình, tiếp tục dùng luồng lập lịch hiện tại.

## 10. Các bước sau MVP

1. Agent phân rã công việc: tạo đề xuất việc con và dependency; người dùng chọn trước khi lưu.
2. Agent theo dõi tiến độ: phân tích các sự kiện hoàn thành sớm, bỏ lỡ phiên và đổi deadline; trả đề xuất điều chỉnh theo cài đặt của người dùng.
3. Agent tư vấn workload: giải thích xu hướng lịch sử và thử các phương án giảm tải qua mô phỏng.
4. Chỉ đánh giá việc tách nhiều agent khi một agent với công cụ đã có bộ đo chất lượng và xuất hiện nhu cầu phân vai rõ ràng.

Tài liệu này là kế hoạch triển khai. Chưa bổ sung agent, API, schema hoặc thay đổi hành vi của ứng dụng.
