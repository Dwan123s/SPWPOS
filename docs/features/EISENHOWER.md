# Eisenhower trong PLANORA

Ngày cập nhật: 02/11/2026. Model NORA giữ `gemini-3.8-flash`.

## Cách sử dụng

Vào **Công việc → Eisenhower** để chuyển từ danh sách sang ma trận. Mỗi ô hiển thị tối đa bốn task/trang, có tổng số, tìm kiếm và phân trang. Điện thoại dùng tab chọn nhóm. Bấm tên công việc mở popup chi tiết; task nhóm mở đúng nhóm và task tương ứng, giữ quyền nhận việc và file đính kèm.

| Nhóm | Ý nghĩa |
| --- | --- |
| Làm trước | Quan trọng và khẩn cấp. |
| Chủ động lên lịch | Quan trọng, còn quỹ thời gian dự phòng. |
| Xử lý / phân công | Mức ảnh hưởng thấp hơn nhưng khẩn cấp; vẫn phải giữ deadline đã cam kết. |
| Có thể để sau | Mức ảnh hưởng thấp hơn và chưa khẩn cấp; không tự xóa công việc. |
| Cần làm rõ | Thiếu căn cứ về ảnh hưởng, thời khóa biểu/giờ làm phù hợp hoặc ngoài horizon 30 ngày. |
| Chờ bạn duyệt | Kết quả member đã gửi, cần leader kiểm tra. Không dùng thời lượng làm ban đầu để xếp lại. |

Trang **Hôm nay** bổ sung tối đa ba việc nên tập trung, có lý do, tín hiệu quá hạn/rủi ro/phụ thuộc. Gợi ý có thể là việc được giao đang chờ nhận hoặc việc cần leader duyệt; các loại này được ghi rõ.

Mở popup để xem đánh giá. Với task chưa xác định mức quan trọng, NORA tự đề xuất mức quan trọng kèm lý do từ nội dung công việc. Bấm **Đồng ý đề xuất** để xác nhận, hoặc **Chỉnh sửa** để đổi mức/lý do trước khi lưu. Task đã xác nhận chỉ phân tích lại khi bấm **NORA đề xuất lại**; đề xuất mới không ghi đè đánh giá đã lưu. Nếu thiếu căn cứ hoặc AI lỗi, có thể thử lại hoặc tự điều chỉnh. Không bắt người dùng chấm điểm hay cấu hình ma trận; không suy ra Important từ Priority cũ.

## Phân loại và xếp lịch

NORA trả đề xuất mức quan trọng cùng lần phân tích task/file hiện tại: `Important`, `Lower`, `Unknown`, lý do, độ tin cậy và trích dẫn. Máy chủ yêu cầu căn cứ và confidence ít nhất 0.8; giá trị thiếu/không hợp lệ chuyển Unknown. Với text/DOCX trích dẫn phải có trong nguồn; PDF được model đọc và leader kiểm tra trong bản nháp. Trường mới optional, tương thích kết quả và bản nháp cũ.

Riêng luồng đề xuất trong popup luôn chờ người dùng xác nhận nên có thể hiển thị gợi ý ít chắc chắn hơn; không hạ ngưỡng 0.8 của luồng nhập tự động. Một lượt Gemini 3.8 Flash nhận tên, mô tả và tên tiền nhiệm thuộc tài khoản (task nhóm chỉ nhận nội dung chung). Đề xuất `Important`/`Lower` phải có trích dẫn thực sự xuất hiện trong ngữ cảnh này. Không gửi lịch riêng hoặc hồ sơ cá nhân. Giới hạn chờ 25 giây; cache theo user, phạm vi, task và version trong 20 phút, gộp request frontend đang chạy. Task hoặc quyền thay đổi trong lúc phân tích thì từ chối kết quả cũ.

Độ khẩn cấp được tính tại máy chủ từ lịch đã có, giờ sinh hoạt/giờ làm được xác nhận, phần việc còn lại, nhu cầu cạnh tranh, khả năng xếp phiên và deadline. Quá hạn, thiếu khả năng hoàn thành, ít dự phòng hoặc hạn trong 24 giờ tạo tín hiệu khẩn cấp. Không biết lịch thì không kết luận “chưa khẩn cấp”. Task cha được loại khỏi tổng nếu có việc con; task nhóm đã nhận chỉ tính bản việc cá nhân tương ứng.

Một lần đánh giá chạy một lần kiểm tra khả thi cho toàn bộ snapshot. Các khoảng ăn/ngủ/nghỉ/di chuyển và lịch cố định tiếp tục do engine hiện tại bảo vệ. Dữ liệu đánh giá không lưu thành lịch và không ghi đè `Priority`/`IsHardDeadline`.

Chính sách xếp lịch thử ưu tiên việc Important và các tiền nhiệm, so với phương án cũ. Nếu có bất kỳ task nào thiếu nhiều phút hơn hoặc bị trễ hơn, giữ phương án cũ. Lịch khẩn cố định dùng quy trình dịch chuyển đang có. Xem ma trận/đổi metadata không tự áp dụng lịch. Nhận task nhóm vẫn phải xác nhận phương án. Sửa mức quan trọng bằng chat riêng lẻ giữ nguyên lịch trừ khi người dùng có yêu cầu lập lịch riêng.

Plan/claim lưu policy version khi có; khi xác nhận kiểm tra chính sách, revision, task version và khả năng áp dụng theo giờ hiện tại. Draft cũ chưa có policy tiếp tục dùng kiểm tra phiên bản/ràng buộc trước đây.

## Nhóm và quyền riêng tư

Leader xác nhận mức quan trọng chung trong bản nháp từng task; chỉ leader được sửa đánh giá chung. Member có thể đặt đánh giá riêng trên công việc cá nhân đã nhận. Nếu chưa có override riêng, kế thừa mức chung từ assignment/membership đang hiệu lực. Thay đổi đánh giá chung làm claim cũ stale và tăng revision của tài khoản đang nhận để bảo vệ preview riêng.

Task chỉ định chưa được nhận nằm trong “Cần nhận task”, có đánh giá dự kiến theo lịch của chính người được giao, không tạo `WorkTask` hoặc tự nhận việc. Bộ tính xét tập task chỉ định đang chờ nhận cùng nghĩa vụ hiện tại. Task mở cho cả nhóm chỉ được thêm vào snapshot ứng viên của người đang xem khi yêu cầu đánh giá riêng.

Leader không đọc được capacity/urgency/ma trận cá nhân của member. Chỉ metadata quan trọng chung được trả qua API nhóm. Q3 không tự giao lại người khác, không làm lộ lịch để tìm “người đang rảnh”.

## API

Các endpoint yêu cầu đăng nhập, dùng tài khoản từ token:

- `GET /api/eisenhower`: snapshot cá nhân, gồm task đã nhận, task chỉ định chờ nhận và hàng đợi cần duyệt. Không dùng `userId` từ query để xem hộ người khác.
- `GET /api/tasks/{id}/eisenhower`: đánh giá công việc thuộc tài khoản.
- `GET /api/teams/tasks/{id}/eisenhower`: đánh giá theo lịch người nhận/người được chỉ định, hoặc snapshot ứng viên hợp lệ của người tự nhận.
- `POST /api/tasks/{id}/importance`: `{ version, level, reason }`, chỉ chủ sở hữu, kiểm tra task version.
- `POST /api/teams/tasks/{id}/importance`: cùng payload, chỉ leader, kiểm tra membership và task version.
- `POST /api/tasks/{id}/importance/proposal`: `{ version }`, trả `{ version, suggestion: { level, reason, confidence, evidence } }`, chỉ chủ sở hữu.
- `POST /api/teams/tasks/{id}/importance/proposal`: cùng payload/kết quả, chỉ leader; không đọc lịch riêng của member.

`reason` bắt buộc, tối đa 1.000 ký tự. Các API GET không gọi AI, không tạo phiên, claim hoặc notification. Chỉ endpoint POST `/importance/proposal` gọi AI và ghi nhật ký sử dụng; không đổi mức quan trọng, lịch hoặc nhận task. Xác nhận/chỉnh sửa dùng endpoint `/importance` riêng và version của đề xuất. Payload đánh giá chỉ có metadata, không có các slot lịch nguồn hoặc nội dung file. API trả toàn bộ snapshot trong giới hạn tải công việc hiện tại để tính nhất quán tổng nhu cầu và gợi ý top 3; giao diện phân trang từng ô, không tính urgency độc lập trên từng trang.

## Cache, thời gian và thông báo

Cache tách theo user và hash của snapshot: lịch, metadata task, mức quan trọng kế thừa, assignment và queue duyệt. Hạn cache tối đa 30 giây hoặc mốc vào 24 giờ trước deadline gần nhất. UI tải lại theo hạn này; đọc lại ở request sau và worker đều kiểm tra thời gian. Không cache binary và không gọi LLM để polling.

Worker notification hiện tại phát hiện chuyển vào Q1 hoặc rủi ro mới. Lần bật tính năng đầu chỉ lập baseline để không gửi hàng loạt cho backlog cũ. Mỗi episode/tín hiệu có dedupe key; thoát cảnh báo cần ổn định mười phút để hạn chế dao động. Quá hạn hoặc thiếu quỹ mới được cảnh báo ngay. Dùng notification và hàng đợi Telegram hiện có, giữ opt-in, binding, retry; chỉ gửi cho chính tài khoản có thay đổi.

## Cấu hình và rollout

```json
"Eisenhower": {
  "Enabled": true,
  "SchedulingEnabled": true,
  "NotificationsEnabled": true
}
```

`Enabled=false` ẩn feature và tắt tích hợp. Có thể giữ UI/bộ đánh giá nhưng tắt riêng tác động lên cách chọn phương án hoặc thông báo. Những cờ này nằm ở cấu hình máy chủ; không thêm bước cấu hình bắt buộc cho user.

Áp dụng migration `EisenhowerPrioritization` sau backup/verify. Migration thêm các cột quan trọng vào WorkTasks/TeamTasks và bảng PriorityAlertStates, không đổi mức ưu tiên hoặc deadline cũ. Cần build cấu hình không đè API đang chạy, kiểm tra schema rồi restart. Rollback hành vi bằng cờ cấu hình, giữ dữ liệu thêm mới.

## Giới hạn chủ động

- Quỹ thời gian cá nhân chỉ đánh giá trong horizon 30 ngày; không hứa khả thi cho deadline xa hơn.
- Đánh giá độ tin cậy là đề xuất của AI, không phải đo lường khách quan về tầm quan trọng. Người dùng/leader có thể điều chỉnh.
- Không tự ước lượng thời lượng duyệt bài hoặc tự chia việc Q3 cho thành viên khác.
- Không tự backfill Importance cho task cũ; không sửa Notes và không gọi AI hàng loạt trong nền.
- UI có thể chậm cập nhật tối đa 30 giây khi thay đổi xảy ra từ tài khoản/thiết bị khác; khi áp dụng phương án luôn kiểm tra dữ liệu mới.
