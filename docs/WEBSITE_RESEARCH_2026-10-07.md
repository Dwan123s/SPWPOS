# Nghiên cứu website tham khảo cho PLANORA

Ngày nghiên cứu: **07/10/2026**, múi giờ Asia/Saigon.

## 1. Mục tiêu và phương pháp

PLANORA — Smart Personal Workload Planning and Optimization System — tập trung vào quản lý công việc cá nhân, thời gian rảnh, lập lịch theo deadline, thực hiện phiên và phân tích năng lực thực tế. Vì vậy, nghiên cứu ưu tiên các sản phẩm nối **task → thời lượng → calendar → execution → workload**, thay vì chỉ có bảng công việc.

Phạm vi gồm 10 sản phẩm tiêu biểu. Nguồn là website sản phẩm và tài liệu trợ giúp chính thức, đối chiếu với mã nguồn cùng `USE_CASE_COVERAGE.md` của dự án. Đây là nghiên cứu chức năng và luồng sử dụng từ tài liệu công khai; chưa đăng nhập để kiểm thử các sản phẩm, chưa đánh giá hiệu năng hoặc xác nhận tất cả tính năng ở từng gói dịch vụ. Các đề xuất bên dưới là nhận định cho PLANORA, không phải kết luận do nhà cung cấp công bố.

Không đưa giá vào bảng so sánh vì mục tiêu hiện tại là thiết kế và định hướng phát triển. Chức năng và gói dịch vụ có thể thay đổi sau ngày nghiên cứu.

## 2. Danh sách đối chiếu

Trong bảng, “mức liên quan” đánh giá độ gần với phạm vi PLANORA, không xếp hạng chất lượng chung của sản phẩm.

| Website và nguồn chính thức | Mức liên quan | Chức năng được mô tả trong nguồn | Ý tưởng đề xuất cho PLANORA |
|---|---|---|---|
| [Motion — AI Task Manager](https://www.usemotion.com/features/ai-task-manager) | Rất cao | Xếp công việc lên lịch theo deadline, ưu tiên, phụ thuộc và cửa sổ thời gian; điều chỉnh lịch khi thời lượng hoặc ưu tiên thay đổi. | Làm nổi bật việc tiếp theo và các phiên bị tác động khi lập lại lịch. |
| [SkedPal — Planning](https://docs.skedpal.com/planning/introduction-to-planning) | Rất cao | Lập lịch thích nghi, xử lý thay đổi và kiểm tra khả năng nhận thêm công việc trước khi đề xuất ngày thực tế hơn. | Hiển thị phần công việc chưa thể xếp lịch và hướng xử lý ngay trong preview. |
| [Sunsama — Daily Planning](https://help.sunsama.com/docs/usage-guides/daily-planning/) | Rất cao | Hướng dẫn lập kế hoạch ngày, tổng thời lượng dự kiến, so sánh ngưỡng workload và hoãn việc khi quá tải. | Dùng luồng “chọn việc → kiểm tra sức chứa → xem lịch → xác nhận”. |
| [Reclaim 2.0 — Tasks](https://help.reclaim.ai/en/articles/16558552-reclaim-2-0-tasks-overview) | Cao | Gợi ý ưu tiên task dựa trên deadline, ưu tiên và ngữ cảnh lịch; theo dõi thời gian làm việc. Trong phiên bản 2.0, từng task không tự trở thành sự kiện lịch. | Tách khung tập trung khỏi danh sách việc gợi ý để người dùng có lựa chọn trong phiên. |
| [Akiflow — Features](https://akiflow.com/features) | Cao | Universal Inbox, time blocking, nhập task, command bar, focus timer và các tích hợp. | Rút ngắn đường đi từ ghi nhanh đến công việc đã được xác nhận và đưa vào kế hoạch. |
| [Todoist — Features](https://www.todoist.com/features) | Cao | Quick Add bằng ngôn ngữ tự nhiên, ngày lặp lại, nhắc việc và tổ chức công việc. | Form thêm việc gọn, các trường nâng cao mở khi cần, metadata dễ đọc. |
| [TickTick — Features](https://ticktick.com/features) | Cao | Danh sách và bộ lọc, nhiều chế độ calendar, Pomodoro, thói quen và thống kê. | Kết nối danh sách task, phiên tập trung và lịch sử hoàn thành; ưu tiên agenda trên mobile. |
| [ClickUp — Measure your workload](https://help.clickup.com/hc/en-us/articles/30799712357271-Measure-your-workload) | Bổ trợ | Đo workload bằng các đơn vị như task, estimate; có thể hiển thị thời gian đã theo dõi cạnh estimate. | So sánh estimate, adjusted estimate và actual bằng cùng đơn vị phút/giờ. |
| [Asana — Workload](https://asana.com/features/resource-management/workload) | Bổ trợ | Biểu diễn sức chứa, workload theo người; effort có thể dùng giờ hoặc điểm, có chỉ báo quá tải. | Chuyển mẫu trực quan sức chứa sang theo ngày/tuần của một cá nhân. |
| [Toggl — Features](https://toggl.com/features) | Bổ trợ | Time tracking, nhật ký thời gian, báo cáo, capacity planning và cảnh báo theo ngưỡng. | Cho phép đi từ biểu đồ tổng quan xuống các phiên tạo ra số liệu đó. |

## 3. Những sản phẩm cần nghiên cứu sâu nhất

### Motion: tham khảo logic điều chỉnh lịch

Điểm liên quan là nối dữ liệu task với lịch thay vì để hai phần độc lập. Với PLANORA, người dùng cần hiểu điều gì xảy ra khi tăng thời lượng một công việc, bỏ lỡ phiên hoặc thay đổi deadline. Motion mô tả việc tự điều chỉnh lịch theo thay đổi trong ngày. [Nguồn](https://www.usemotion.com/features/ai-task-manager).

Đề xuất: khi preview lập lại lịch, hiển thị số phiên giữ nguyên, số phiên chuyển giờ và số việc chưa xếp được. Giữ quy trình xác nhận của PLANORA; những phiên khóa phải có nhãn rõ ràng. Đây là đề xuất cách trình bày, không khẳng định giao diện hiện tại của Motion có các nhãn này.

### SkedPal: tham khảo giới hạn và cửa sổ thời gian

Time Maps quy định thời gian phù hợp cho từng loại công việc, ví dụ giờ làm việc, buổi tối, cuối tuần hoặc deep work. [Nguồn Time Maps](https://docs.skedpal.com/time-maps/introduction-to-time-maps).

PLANORA hiện có availability theo tuần, sự kiện cố định và ngoại lệ. Có thể phát triển thêm cửa sổ ưu tiên theo loại task: lập trình vào buổi sáng, việc nhẹ vào cuối ngày. Khi bổ sung, cần phân biệt **giới hạn bắt buộc** với **ưu tiên mềm**, đồng thời kiểm tra tương tác với deadline, phụ thuộc và phiên khóa.

### Sunsama: tham khảo trải nghiệm lập kế hoạch thực tế

Tài liệu Sunsama trình bày các bước xem lại ngày trước, thêm việc, kiểm tra workload dự kiến, hoàn thiện kế hoạch và chia sẻ. Tổng thời lượng được so với ngưỡng workload; người dùng có thể hoãn những việc vượt sức chứa. [Nguồn](https://help.sunsama.com/docs/usage-guides/daily-planning/).

Đề xuất cho PLANORA: trang Hôm nay đặt cạnh nhau lượng việc cần làm và thời gian có thể làm. Đưa nút tạo kế hoạch vào đúng thời điểm người dùng đã nhìn thấy nguy cơ quá tải. Kết thúc ngày bằng một bản xem lại ngắn về việc hoàn thành, phiên bỏ lỡ và chênh lệch thời lượng.

### Reclaim: phân biệt phiên bản trước khi so sánh

Tài liệu [Reclaim 1.0](https://help.reclaim.ai/en/articles/6210740-features-in-reclaim) và [Reclaim 2.0](https://help.reclaim.ai/en/articles/14846468-reclaim-ai-2-0-overview) mô tả các workflow khác nhau. Theo hướng dẫn Tasks 2.0, hệ thống gợi ý việc trong khung tập trung thay vì tự đặt từng task thành sự kiện lịch. Vì vậy, không nên dùng mô tả của bản 1.0 để kết luận workflow hiện tại của bản 2.0. [Nguồn Tasks 2.0](https://help.reclaim.ai/en/articles/16558552-reclaim-2-0-tasks-overview).

Với PLANORA, đây là một hướng mở rộng để người dùng chọn việc phù hợp trong một khung tập trung. Nên thử sau khi luồng lập lịch task hiện tại đã rõ ràng; cần có tiêu chí đánh giá để tránh làm người dùng khó hiểu giữa task, phiên và khung thời gian.

### Akiflow và Todoist: tham khảo tốc độ nhập công việc

Akiflow mô tả inbox tập trung và command bar; Todoist mô tả Quick Add bằng ngôn ngữ tự nhiên. [Akiflow](https://akiflow.com/features), [Todoist](https://www.todoist.com/features).

Đề xuất: giữ một đường nhập nhanh dễ tìm trên desktop và mobile. Sau khi phân tích câu, hiển thị rõ tên, thời lượng, deadline và mức ưu tiên được nhận diện để người dùng sửa trước khi lưu. Không nên buộc người dùng nhập mọi thuộc tính trong lần tạo đầu tiên.

## 4. Đối chiếu với hệ thống hiện có

Bảng dưới dựa trên mã nguồn và tài liệu của PLANORA; “đã có” xác nhận phần triển khai tồn tại, không thay thế kiểm thử sản phẩm thực tế.

| Khả năng | Tình trạng trong PLANORA | Hướng phát triển đề xuất |
|---|---|---|
| Task, subtask, category, dependency | Đã có API và React UI | Giảm thao tác tạo/chỉnh sửa, hiển thị phụ thuộc dễ hiểu. |
| Nhập câu tự nhiên và chia nhỏ task | Đã có adapter AI tùy chọn và fallback quy tắc | Kiểm chứng provider thực tế; trình bày nguồn gợi ý, preview và lỗi rõ ràng. |
| Availability, sự kiện cố định, ngoại lệ | Đã có | Cải thiện onboarding; cửa sổ ưu tiên theo loại task là tính năng mới cần thiết kế. |
| Tạo/xem trước/áp dụng/lập lại lịch | Đã có | So sánh lịch trước và sau, làm rõ lý do các thay đổi. |
| Khóa, chuyển, đổi thời lượng và chia phiên | Đã có | Làm nổi bật ràng buộc; kiểm tra chỉnh sửa trên mobile. |
| Timer, tạm dừng, tiếp tục, actual duration | Đã có | Tăng độ rõ của trạng thái chạy và lịch sử chỉnh actual. |
| Bias, effective capacity, overload/task risk | Đã có bằng thuật toán và thống kê | Hiển thị số mẫu, công thức/ý nghĩa và hành động tiếp theo. |
| Kịch bản what-if | Đã có | Dễ so sánh trước/sau và phân biệt mô phỏng với kế hoạch sẽ áp dụng. |
| Đồng bộ calendar ngoài | Chưa triển khai trong phạm vi hiện tại | Nghiên cứu OAuth, đồng bộ hai chiều, trùng sự kiện và timezone trước khi thêm. |
| Chatbot về lịch/workload | Chưa có | Bắt đầu bằng hỏi đáp dữ liệu được phép xem; các đề xuất thay đổi cần preview. |

Các điểm kiểm tra trong repository:

- `docs/USE_CASE_COVERAGE.md`: đối chiếu 72 use case và các giới hạn triển khai.
- `src/SPWPOS.Application/Workspace/PlanningEngine.cs`: planner thuần deterministic, bias, capacity và risk.
- `src/SPWPOS.Infrastructure/Workspace/TaskInterpreter.cs`: AI capture/breakdown, schema validation và fallback.
- `src/SPWPOS.Infrastructure/Workspace/PlanService.cs`: các luồng kế hoạch.
- `src/SPWPOS.Infrastructure/Workspace/LearningService.cs`: quan sát thời lượng và hồ sơ năng lực.
- `frontend/src/tasks.jsx`, `calendar.jsx`, `schedule.jsx`, `dashboard.jsx`, `pages.jsx`: màn hình tương ứng.

## 5. Định hướng riêng cho PLANORA

Định hướng đề xuất: **lập kế hoạch cá nhân theo thời gian thực tế, giải thích được rủi ro và điều chỉnh từ lịch sử thực hiện**. Điều này phù hợp với cấu trúc đang có. Nghiên cứu này không chứng minh PLANORA có tính năng độc quyền hoặc tốt hơn các sản phẩm khác.

Ba câu hỏi nên dẫn dắt toàn bộ trải nghiệm:

1. Tôi nên làm việc gì tiếp theo?
2. Tôi có đủ thời gian để hoàn thành trước deadline không?
3. Nếu kế hoạch không khả thi, tôi có thể thay đổi điều gì?

Ví dụ minh họa cho UI, không phải dữ liệu thật: người dùng có 10 giờ rảnh, hệ số capacity 0,8, nên sức chứa hiệu dụng là 8 giờ. Nếu tổng thời lượng còn lại sau điều chỉnh là 10 giờ, workload bằng 125% và thiếu 2 giờ. Giao diện nên hiển thị cả số giờ và hành động như xem việc có thể hoãn hoặc thử kịch bản thêm thời gian.

Bias hiện cần ít nhất 3 mẫu trước khi cá nhân hóa. Khi chưa đủ dữ liệu, UI cần phân biệt ước tính ban đầu với điều chỉnh dựa trên lịch sử; tránh trình bày như kết luận chắc chắn về năng lực của người dùng.

## 6. Đề xuất UI theo từng màn hình

Đây là bản định hướng thiết kế cho PLANORA, chưa phải thay đổi mã nguồn.

| Màn hình | Bố cục và hành vi đề xuất | Tham khảo chính |
|---|---|---|
| Hôm nay | Một hành động chính “Bắt đầu phiên”; lịch trong ngày; workload theo giờ; cảnh báo có cách xử lý. | Sunsama, Motion |
| Công việc | Danh sách gọn, bộ lọc, metadata deadline/estimate/priority; chi tiết mở cạnh danh sách trên desktop. | Todoist, TickTick |
| Lịch | Desktop ưu tiên lịch tuần; mobile ưu tiên agenda; nhãn cố định/khóa/đề xuất rõ ràng. | Akiflow, SkedPal |
| Preview kế hoạch | Tổng giờ xếp được, phần chưa xếp, lý do thay đổi và nút xác nhận. | Motion, Sunsama |
| Phiên tập trung | Timer dễ đọc, task hiện tại, tạm dừng/hoàn thành, nhập actual sau phiên. | TickTick, Toggl |
| Phân tích | Estimate → adjusted estimate → actual; capacity/load theo tuần; nhấn số liệu để xem phiên liên quan. | ClickUp, Asana, Toggl |
| Kịch bản | Kết quả trước/sau, phần chênh lệch giờ và rủi ro; đánh dấu mô phỏng rõ. | Phù hợp với luồng scenario hiện có |
| AI | Preview nội dung nhận diện/chia nhỏ, nguồn gợi ý, quyền cho phép gửi dữ liệu. | Định hướng từ tính năng AI hiện có |

Giữ thương hiệu xanh–trắng và liquid glass đã chọn. Dùng glass cho shell, thanh công cụ và vùng nổi; danh sách, form, calendar và bảng dữ liệu nên có nền đủ rõ để dễ đọc. Trạng thái rủi ro cần cả nhãn/icon lẫn màu. Trên mobile, các thao tác quan trọng không phụ thuộc hover; khi dùng bàn phím, focus phải nhìn thấy được.

## 7. Thứ tự phát triển cho nhóm 4 người

| Ưu tiên | Công việc đề xuất | Chủ trì theo kế hoạch nhóm | Tiêu chí đánh giá |
|---|---|---|---|
| P1 | Làm rõ Hôm nay, workload và phiên tiếp theo | TV4, phối hợp TV3 | Người dùng đọc được việc tiếp theo, giờ còn lại và nguy cơ quá tải. |
| P1 | Rút ngắn nhập task và preview capture | TV2, phối hợp TV1 | Sửa được các trường nhận diện trước lưu; fallback rõ; không mất dữ liệu nhập. |
| P1 | Preview lập lại lịch trước/sau | TV3 | Phân biệt phiên giữ nguyên, đổi giờ, chưa xếp và lý do; phiên khóa được bảo vệ. |
| P1 | Hoàn thiện hành vi mobile và component chung | TV1, cùng các chủ module | Form, modal, menu, agenda và timer sử dụng được bằng cảm ứng/bàn phím. |
| P2 | Xem lại cuối ngày/tuần | TV4, phối hợp TV2/TV3 | Báo cáo truy ngược được về task/phiên thật; số liệu nhất quán. |
| P2 | Cửa sổ ưu tiên theo loại task | TV3, phối hợp TV2 | Phân biệt ràng buộc cứng/mềm; xử lý thiếu thời gian và timezone. |
| P2 | Trợ lý hỏi đáp workload/lịch | TV4, phối hợp TV1/TV3 | Chỉ đọc dữ liệu được cấp quyền; mọi con số kiểm chứng được từ backend. |
| P3 | Đồng bộ Google/Outlook calendar | TV1/TV3 | Có chính sách conflict, chống trùng, xử lý token hết hạn và quyền truy cập. |

P1 tập trung vào các khả năng đã có để hoàn thiện trải nghiệm. P2/P3 là phạm vi mới, cần thống nhất trước khi triển khai. Phân công này nối tiếp `TEAM_DEVELOPMENT_PLAN.md`, không thay thế toàn bộ kế hoạch hiện có.

## 8. Cách xác thực sau nghiên cứu

Cho người thử thực hiện cùng một kịch bản: tạo 5 công việc với tổng thời lượng vượt sức chứa, thêm sự kiện cố định, tạo preview, hoãn một việc, áp dụng lịch, hoàn thành một phiên với actual khác estimate và xem kết quả phân tích.

Ghi nhận thời gian hoàn thành thao tác, số lần cần giải thích, lỗi nhập, việc người dùng có hiểu workload hay không và khả năng hoàn thành trên mobile. Với planner, kiểm tra deadline, dependency, phiên khóa và không xếp chồng. Với AI, kiểm tra dữ liệu nhận diện, fallback và xác nhận trước lưu.

Kết quả thử nghiệm này mới giúp chọn phương án phù hợp nhất cho PLANORA; các tuyên bố tiếp thị của sản phẩm tham khảo không thay thế bằng chứng đó.
