# Chỉnh sửa bản chia việc trước khi giao

Leader có thể chỉnh trực tiếp các đề xuất của NORA trong bản nháp chia việc nhóm: hạn chung, thời lượng/hạn riêng và tên/tóm tắt/yêu cầu/đầu ra/tiêu chí/ghi chú từng gói. Lưu bản nháp giữ file nguồn, không chạy AI lần nữa, không tạo task hoặc lịch. Chỉ xác nhận giao việc mới tạo các task nhóm.

Bản nháp thiếu giờ nộp hiện được giữ trên máy chủ 30 phút, có ID và revision. Chỉnh từng gói có thể lưu một phần; còn gói thiếu deadline thì tiếp tục chặn giao việc. Hạn chung cập nhật mọi gói một lần. Các bản nháp đã sửa yêu cầu confirm đúng revision đã xem; task IDs, quyền leader, owner, membership và thời hạn được kiểm tra. Dùng schema hiện có, không thêm migration.

Kiểm chứng:

- Backend `PrivateTeamPlanningTests`: **100 passed**. Gồm lưu nội dung/thời lượng/hạn mới, giữ file nguồn khi publish, không thêm AI call, không tạo lịch, sửa một phần, thiếu quyền, stale, hết hạn và dữ liệu không hợp lệ.
- API `TeamDraftEditingApiTests` + `TeamBriefApiTests`: **2 passed**. Kiểm tra pending draft chỉnh được nhưng chưa publish, revision mới, chặn confirm cũ và lưu đúng nội dung khi giao việc.
- Frontend nhóm/người nhận: **22 passed**. Kiểm tra hạn chung không gọi AI lại, chỉnh tóm tắt/yêu cầu/thời lượng, giữ gói khác/file nguồn, hủy sửa, chặn giao việc khi đang sửa và confirm gửi revision mới.
- Production frontend build thành công. Giao diện được kiểm tra bằng dữ liệu giả lập; không dùng task hoặc lịch thật của người dùng để chạy kiểm thử.
- Browser ở **1440, 390, 320 px**: sửa tóm tắt/yêu cầu/thời lượng, lưu đúng giá trị, chốt hạn chung và xác nhận giao việc; chỉ một request phân tích AI, không lỗi trang hoặc tràn ngang. Ảnh `artifacts/team-draft-editor-*.png` và `team-draft-options-*.png`.
- Local API Debug PID **12004**, schema hiện tại khớp, Gemini **3.8 Flash** sẵn sàng. HTTP `/app/` trả 200, entry **`index-5qm6ZljN.js`**, bundle có hai điều khiển mới và Swagger có route `/briefs/{proposalId}/edit`.

Notes được giữ nguyên. Không push Git trong đợt chỉnh sửa này.

## Sửa lỗi thư viện không khớp khi tải Word

Log local ghi `MissingMethodException` ở constructor `TeamBriefDraft` (có `Revision`): tiến trình chạy từ `bin/Release` giữ Application cũ trong bộ nhớ, trong khi Infrastructure trên ổ đĩa đã thay đổi bởi build khác. Request văng trước bước đọc tài liệu/AI.

`scripts/Restart-PlanoraApi.ps1` giờ rebuild toàn bộ project graph vào một thư mục mới dưới `artifacts/api-runtime/<timestamp-guid>`, kiểm tra schema rồi mới thay tiến trình cũ. Không khởi chạy từ thư mục `bin` có thể bị build khác ghi đè. Config/content root và User Secrets vẫn dùng vị trí máy chủ hiện có. Build hoặc schema lỗi giữ API cũ chạy; không xóa dữ liệu.

Đã kiểm tra:

- Script test bằng tiến trình giả: build/schema lỗi không dừng API, bản chạy ở thư mục riêng, restart tiếp theo thay đúng runtime trước và dùng đường dẫn mới; cửa sổ ẩn/content root/Development/AI status được giữ.
- Full API rebuild vào thư mục riêng: 0 warnings, 0 errors; schema khớp, không đổi database.
- Hai kiểm thử API thông thường về upload và chỉnh sửa qua; một live HTTP test bổ sung với Gemini 3.8 và file Word SSG105 mẫu qua (24 giây). Nhóm hai thành viên trong SQLite tạm nhận đủ hai gói chờ hạn, sửa hạn/thời lượng trả hai gói hợp lệ; không publish hoặc ghi tài khoản thật.
- Kiểm tra process modules local: Api/Application/Infrastructure/Domain đều nạp từ cùng thư mục runtime riêng; HTTP giao diện 200, route sửa bản nháp có mặt; model vẫn `gemini-3.8-flash`.
