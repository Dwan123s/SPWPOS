# Nhóm đồ án và lịch cá nhân

Luồng nghiệp vụ: leader tạo task chung; member bấm **Nhận task · NORA xếp lịch**. Popup mở ngay để phân tích nội dung công việc, thời lượng đã xác nhận, deadline và lịch của chính member đó. Member xem phương án rồi xác nhận để nhận task và cập nhật lịch. Hai task cùng deadline có thể dẫn tới hai lịch hoàn toàn khác nhau cho hai tài khoản.

Trong bản nháp tạo task, leader chọn **Người nhận task**: mặc định **Cả nhóm · Tự nhận task**, hoặc chọn một thành viên cụ thể (kể cả leader). Với bản chia việc từ file, chọn riêng cho từng task. Lựa chọn chỉ được lưu khi xác nhận tạo; model không tự gán người nhận. `DesignatedUserId` chỉ là người được phép nhận, khác `AssigneeId` là người đã xác nhận nhận việc. Task vẫn `Open` và không chạm lịch cá nhân cho tới khi người được chỉ định xác nhận phương án NORA.

Backend kiểm tra membership/tài khoản hoạt động tại lúc giao và chặn người khác ở cả preview lẫn confirm nhận task; quyền leader không bỏ qua giới hạn này. Người được chỉ định nhận notification hệ thống (và hàng đợi Telegram theo thiết lập hiện tại), không nhân đôi khi retry. Task được giao đích danh hiện trong **Việc của tôi** kể cả trước khi nhận. Leader có thể đổi/bỏ chỉ định trong popup khi task chưa được nhận; thay đổi version vô hiệu hóa phương án cũ. Nếu đã nhận, phải trả việc trước. Hoàn tác/trả việc giữ chỉ định, để người khác không tự lấy task. Thành viên có task chỉ định còn chờ nhận cần leader đổi/bỏ chỉ định trước khi rời nhóm.

## Dữ liệu và quyền

- Nhóm chỉ trả task chung, người nhận, tiến độ được xác nhận, thành viên và hoạt động chung. Một task có một người nhận tại một thời điểm.
- `WorkTask`, `WorkSession`, ảnh thời khóa biểu và đề xuất nhận việc thuộc tài khoản cá nhân. Leader không được xem lịch riêng của thành viên.
- Chỉ leader được tạo bản nháp và phát hành task bằng hội thoại; API kiểm tra lại quyền khi xác nhận. Thành viên không có nút giao task. NORA đề xuất thời lượng nếu thiếu; phải xác nhận trước khi phát hành task. Deadline mới cần dẫn chứng từ yêu cầu người dùng.
- Leader quản lý thành viên, mã mời và yêu cầu đổi deadline/thời lượng. Yêu cầu của task đã nhận chỉ có hiệu lực khi người nhận xem lịch riêng và xác nhận.

## File báo cáo khi gửi duyệt hoàn thành

Popup **Gửi xác nhận hoàn thành** có ghi chú tối đa 1.000 ký tự và tùy chọn **Đính kèm file**. Mỗi lần gửi tối đa 3 file, mỗi file tối đa 8 MB, tổng tối đa 20 MB. Hỗ trợ PDF, DOCX, XLSX, PPTX, TXT, Markdown, CSV, ZIP, PNG, JPEG và WebP. File nằm ở trình duyệt đến khi member bấm **Xác nhận & gửi duyệt**; có thể bỏ từng file trước khi gửi.

Máy chủ kiểm tra người nhận task, phiên bản, định dạng và dung lượng. File và ghi chú lưu cùng bản ghi lần gửi duyệt trong transaction chuyển task sang `PendingReview`; không dùng AI để duyệt báo cáo. Request ID giữ nguyên khi thử gửi lại sau lỗi mạng, không lưu trùng. Nếu người dùng đổi nội dung hoặc file, giao diện tạo request ID mới. Lỗi xác nhận trạng thái, ví dụ phiên làm việc còn chạy, rollback cả trạng thái và file.

Leader thấy ghi chú và file trong cửa sổ **Duyệt hoàn thành task**, có nút **Tải file** trước khi quyết định duyệt hoặc yêu cầu bổ sung. Người gửi và leader cũng xem được kết quả hiện tại trên thẻ task. Các thành viên còn lại không nhận metadata file hoặc ghi chú báo cáo. Endpoint tải yêu cầu đăng nhập, tài khoản hoạt động, tư cách thành viên hiện tại và quyền người gửi hoặc leader; file được trả về dưới dạng attachment với chính sách không thực thi nội dung và không lưu cache.

Khi bị yêu cầu sửa, member gửi một bản báo cáo mới. Thẻ task chỉ hiển thị file của lần gửi mới nhất; file cũ vẫn được lưu theo lần gửi để không ghi đè kết quả trước. Báo cáo và file không tự đưa vào chat nhóm hoặc Telegram. Thông báo hoàn thành task vẫn được tạo sau quyết định duyệt của leader.

Migration `TeamCompletionAttachments` bổ sung bảng `TeamCompletionSubmissions` và `TeamCompletionFiles` (nội dung nhị phân trong database), không sửa hoặc xóa các bản ghi lịch, task, Notes hay chat hiện có.

## Link mời tham gia nhóm

Leader bấm **Tạo link mời** ở trang nhóm (chọn một nhóm mình quản lý), hoặc **Mời thành viên** trong nhóm để tạo và sao chép link. Link dùng địa chỉ ứng dụng hiện tại và fragment `#teams?join=...`; mã mời không nằm trong query gửi tới máy chủ. Người nhận có thể mở link trực tiếp hoặc dán nguyên link vào ô **Mã mời hoặc link tham gia**.

Mở link chỉ xem tên nhóm, số thành viên, hạn dùng và trạng thái yêu cầu; chưa thêm thành viên. Nếu chưa đăng nhập, lời mời được giữ trong sessionStorage của tab trong tối đa 24 giờ và quay lại trang nhóm sau khi đăng nhập hoặc tải lại. Người nhận bấm **Gửi yêu cầu tham gia** và chờ leader duyệt; người đã là thành viên bấm **Mở nhóm**. Đóng lời mời hoặc gửi yêu cầu thành công sẽ xóa lời mời đang chờ trong tab.

Yêu cầu mới có `ApprovalStatus=Pending`, `Active=false`; chưa được đọc task, chat, tài liệu hoặc nhận việc. Leader thấy mục **Chờ duyệt tham gia** với tên và email để quyết định duyệt/từ chối. Chỉ leader được gọi API duyệt; version ngăn xử lý lại yêu cầu cũ. Lời mời hết hạn/thu hồi hoặc tài khoản không khả dụng không được duyệt. Người gửi thấy trạng thái riêng, nhận thông báo kết quả, danh sách nhóm tự cập nhật khi được duyệt. Gửi lặp cùng yêu cầu đang chờ không trừ thêm lượt. Từ chối chặn gửi lại qua cùng lời mời; leader có thể tạo lời mời mới. Mỗi tài khoản có tối đa 20 yêu cầu đang chờ.

Leader dùng **Loại khỏi nhóm**, xem tác động rồi xác nhận. Người bị loại mất quyền nhóm ở API, không dùng được cả link cũ lẫn link mới để tự vào lại. Công việc chưa hoàn tất trở về `Open`, bỏ chỉ định, hủy đề xuất nhận việc cũ và tách assignment. Giữ các task/bản sao và lịch cá nhân, lịch sử thực hiện, công việc nhóm đã hoàn tất và báo cáo đã lưu; leader không được xem lịch riêng. Member tự **Rời nhóm** vẫn cần trả việc và bỏ chỉ định trước. Migration `TeamMembershipApproval` bổ sung trạng thái/yêu cầu vào membership hiện có; thành viên đang hoạt động giữ quyền, người đã rời trước cập nhật không bị tự kích hoạt lại.

Chỉ leader tạo/thu hồi lời mời. Mã mời ngẫu nhiên 160 bit được lưu dưới dạng hash ở máy chủ, hết hạn sau 7 ngày và có tối đa 20 lượt tham gia mới; xem trước không trừ lượt. Thu hồi làm cả link và mã tương ứng mất hiệu lực. Link hết hạn, bị thu hồi hoặc hết lượt sẽ báo lỗi; không tự thêm người hoặc mở thông tin lịch riêng. Nhập link của địa chỉ khác bị từ chối; không điều hướng theo link do người dùng dán. Người nhận cần truy cập được địa chỉ máy chủ trong link (link localhost chỉ dùng trên máy đang chạy ứng dụng).

## Tạo nhiều task từ tài liệu công việc

Trong **Tạo task cùng NORA**, leader có thể giữ cách nhập một task hoặc chọn **Chia việc cho cả nhóm**, đính kèm PDF, Word `.docx`, TXT UTF-8 hoặc Markdown. Tối đa 8 MB/file; văn bản trích xuất tối đa 60.000 ký tự, phần bổ sung tối đa 12.000 ký tự. Có thể kéo thả/chọn file và nhập bổ sung deadline bằng lời.

PDF được Gemini 3.8 Flash đọc trực tiếp; DOCX được trích nội dung đoạn văn, bảng, header/footer và chú thích tại máy chủ, không chạy macro hay tải liên kết ngoài. TXT/Markdown được đọc nguyên văn. Không cắt bỏ nội dung âm thầm khi file quá giới hạn. File gốc nằm trong bản nháp riêng của leader cho tới khi xác nhận giao task. Khi xác nhận, transaction lưu một bản trong `TeamTaskDocuments`, liên kết toàn bộ task của đợt chia việc và loại dữ liệu file khỏi JSON bản nháp đã áp dụng. Retry xác nhận không nhân đôi file. Không chèn file vào chat nhóm.

Leader chọn **Số gói việc NORA sẽ chia**, từ 1 đến số thành viên đang hoạt động (bao gồm leader), tối đa 20 gói/lần; mặc định bằng số thành viên. `TaskCount` được kiểm tra tại server trước khi gọi AI. NORA nhận riêng số thành viên thực tế và số gói đã chọn, phải bao phủ đầy đủ phạm vi tài liệu trong đúng số gói đó; chọn 1 sẽ tạo một gói đầy đủ cho cả phạm vi. Đổi số gói giữ nội dung/file nhưng bỏ bản nháp cũ để phân tích lại. Không gửi lịch cá nhân, tên/email hoặc avatar thành viên cho model. Nếu thiếu hạn chót, nội dung không đọc được hoặc không thể chia hợp lý, hiển thị câu hỏi để leader bổ sung; chưa có task nào được giao. Thời lượng là đề xuất cần xác nhận. Deadline có trích dẫn căn cứ từ tài liệu hoặc lời bổ sung; với văn bản/DOCX máy chủ kiểm tra trích dẫn có trong nguồn, với PDF leader kiểm tra căn cứ model đọc trong phần xem trước.

Mỗi task lưu đầy đủ tóm tắt, yêu cầu thực hiện, đầu ra bàn giao, tiêu chí hoàn thành, ghi chú phối hợp, tham chiếu nguồn và căn cứ deadline trong phần mô tả (tối đa 4.000 ký tự/task). Không cắt ghi chú cho vừa giới hạn: yêu cầu phân tích lại nếu quá dài. Bảng nhóm dùng danh sách 8 task/trang, lọc theo người nhận/trạng thái và sắp xếp theo deadline hoặc ưu tiên. Bấm dòng task, tên hoặc **Chi tiết** mở popup. Ghi chú hiển thị thành các khối theo tiêu đề, hỗ trợ danh sách và chữ đậm; vẫn giữ nội dung văn bản, không chạy HTML. Việc cá nhân được tạo khi nhận task giữ nguyên mô tả.

Mục **Tài liệu từ trưởng nhóm** nằm dưới yêu cầu, hiển thị tên, dung lượng và nút **Tải xuống**. API chỉ trả metadata trong dữ liệu nhóm, không trả binary qua polling. `GET /api/teams/tasks/{taskId}/source/files/{fileId}` kiểm tra tài khoản đang hoạt động/đã xác thực, membership hiện tại và đúng cặp task/file trước khi đọc binary. Tất cả thành viên hiện tại, kể cả chưa nhận task, được tải tài liệu gốc; người ngoài/đã rời nhóm không được tải. File trả dưới dạng attachment với MIME do máy chủ xác định, `no-store`, `nosniff` và CSP sandbox. Báo cáo member vẫn giữ quyền riêng cho người nộp và leader.

Với task chưa có tài liệu (gồm task tạo trước cập nhật), leader dùng **Đính kèm tài liệu** trong popup, chọn file rồi **Gửi tài liệu**. Không thay deadline, yêu cầu hoặc lịch cá nhân. `POST /api/teams/tasks/{taskId}/source` kiểm tra quyền leader, task version và request ID; retry cùng request/file không tạo trùng, không ghi đè file gốc đã có. Hỗ trợ cùng định dạng/giới hạn 8 MB như tài liệu phân tích. Phía client giữ file và request ID khi lỗi để gửi lại.

Leader xem danh sách rồi bấm **Xác nhận & giao N task cho nhóm**. Transaction lưu toàn bộ hoặc không lưu gì, retry không tạo trùng. Kiểm tra lại quyền leader, người sở hữu bản nháp, thời hạn bản nháp 30 phút, deadline và tập thành viên hiện tại. Thay đổi thành viên/quyền làm bản chia việc cũ không còn dùng được. SQL Server xác nhận ở SERIALIZABLE và khóa đọc/cập nhật tài khoản trước khi ghi để tránh lỗi khi bấm đồng thời. Task tạo ra chưa có người nhận; member nhận và NORA xếp lịch riêng theo luồng hiện tại.

## Giờ sinh hoạt

Ngay sau khi đọc/lưu thời khóa biểu, NORA tự hiện lịch sinh hoạt đề xuất trong chat: giờ thức/ngủ quanh lịch hôm sau, ăn trưa/tối 30 phút, ăn sáng, nghỉ ngơi và đệm di chuyển. Bữa ăn được tìm trong khoảng không trùng lịch cố định; ví dụ học đến 11:45 và di chuyển 30 phút thì có thể đề xuất ăn trưa 12:15–12:45. Giờ rảnh là phần còn lại sau khi trừ các khoảng này khỏi ngày; không yêu cầu người dùng tự khai báo giờ rảnh trước. Lịch sinh hoạt chỉ được áp dụng sau xác nhận. Có thể chỉnh bằng lời hoặc thêm hoạt động riêng/extra.

`RoutineDaysJson` lưu chính sách sinh hoạt theo từng ngày đã xác nhận. Giờ rảnh được tính từ thời khóa biểu + sinh hoạt + hoạt động riêng hiện tại, không lưu phần bù đã lỗi thời. Thẻ đề xuất cho thấy cả ăn/ngủ/nghỉ/di chuyển, lịch cố định và các khoảng rảnh.

Các giờ rảnh đã lưu được giữ nguyên trong nguồn dữ liệu. Bộ lập lịch giao chúng với chính sách đã xác nhận và trừ các lịch cố định theo ngày, giờ bận, ăn uống, đệm và phiên đã chiếm chỗ. Khoảng đệm chồng nhau được hợp nhất. Khoảng trống lịch và khoảng có thể xếp phiên tập trung được trình bày riêng; không lưu phần bù của một tuần thành lịch rảnh cố định cho tuần sau.

## Nhận việc và thay đổi lịch

Preview chạy ngoài transaction; chỉ lưu metadata đề xuất riêng, không nhận task hay thay lịch. NORA diễn giải các phương án đã được bộ lập lịch kiểm tra. Nếu AI không phản hồi, kết quả kiểm tra xác định vẫn dùng được; không tự bịa chỗ trống.

Nếu đã gửi thời khóa biểu nhưng chưa xác nhận giờ có thể làm việc, popup hướng dẫn xem và chấp nhận chính sách sinh hoạt ngay tại chỗ rồi tự tính lại task. Không gán tình huống thiếu cấu hình thành thiếu quỹ thời gian. Khi không xếp được, phản hồi riêng nêu số phút công việc cần, khoảng trống/quỹ xếp được trước hạn và các ràng buộc cụ thể như phiên tối đa, nghỉ, phiên khóa hay yêu cầu làm liên tục; không chỉ trả một cảnh báo chung chung.

Xác nhận kiểm tra chủ sở hữu, membership, task version, revision lịch và hạn dùng. Transaction nhận task, tạo việc cá nhân, áp dụng phiên và ghi hoạt động nhóm cùng nhau. Lỗi bất kỳ bước nào rollback toàn bộ. Assignment có ràng buộc duy nhất đang hoạt động; retry cùng proposal không tạo thêm dữ liệu.

Ưu tiên khoảng trống trước, rồi thử dời phiên linh hoạt của việc có ưu tiên thấp hơn. Giữ lịch cố định, phiên khóa/đang thực hiện và deadline của mọi việc. Dịch chuyển mặc định tối đa hai ngày; nút riêng cho phép thử xa hơn, tối đa 30 ngày và trong hạn cũ. Chỉ lưu sau khi member xác nhận phương án cụ thể.

Trả việc có bản xem trước các phiên sẽ hủy, kiểm tra revision rồi giải phóng assignment và hủy phiên tương lai trong cùng transaction. Hoàn tác nhận việc kiểm tra phiên bản và lịch chưa đổi, chưa bắt đầu. Thành viên đang nhận việc cần giải quyết trách nhiệm trước khi rời nhóm.

Hoàn thành phiên chỉ ghi nhận thời gian. Member bấm **Báo hoàn thành · Gửi duyệt**, xác nhận trong popup rồi task chuyển `Assigned → PendingReview`. Hoàn thành từ trang công việc cá nhân cũng đi qua bước chờ duyệt này. Leader nhận thông báo để kiểm tra; chỉ leader được duyệt `PendingReview → Completed`. Leader có thể yêu cầu bổ sung kèm nhận xét: task trở về `Assigned`, việc cá nhân mở lại ở tiến độ trước lúc gửi duyệt; thành viên làm xong rồi gửi duyệt lại. Task đang chờ duyệt không được đổi yêu cầu hoặc bỏ lại khi xóa thành viên. Không được sửa deadline/thời lượng task nhóm qua API chỉnh task cá nhân.

## Chat chung

Khung chat ở trang nhóm lưu tin nhắn văn bản tối đa 2.000 ký tự, hiển thị tác giả và thời gian, tải 50 tin gần nhất và có nút xem tin cũ. Tự lấy tin mới mỗi 7 giây khi trang hiển thị; dừng khi chuyển trang hoặc ẩn tab. Retry giữ request ID để tránh gửi trùng. Chỉ thành viên đang hoạt động được đọc/gửi; người gửi lấy từ phiên đăng nhập.

Sau khi leader duyệt, chat chung tự xuất hiện tin **Hệ thống** ghi tên người hoàn thành, tên task và người duyệt. Tất cả thành viên đang hoạt động nhận notification hệ thống; tài khoản đã bật và kết nối Telegram được xếp hàng gửi Telegram theo cấu hình hiện có. Member báo hoàn thành mới chỉ gửi yêu cầu duyệt đến leader, chưa thông báo task hoàn thành cho nhóm. Mỗi lần gửi duyệt có mã riêng, version và transaction bảo đảm retry/duyệt đồng thời không nhân đôi thông báo hoặc tin chat. Nhận xét và trạng thái duyệt được lưu cùng task.

Chat chung không tự gửi thông tin lịch riêng, lịch sử NORA hoặc phương án cá nhân. Gửi chat không làm thay đổi revision lịch hoặc vô hiệu hóa phương án nhận task.

## Kiểm chứng

Từ 02/11/2026, khi NORA đã đọc được phạm vi nhưng deadline còn thiếu giờ hoặc chưa có căn cứ, bản nháp giữ toàn bộ gói việc ở `pendingTasks` với đầy đủ yêu cầu, đầu ra, tiêu chí và ghi chú. UI hiển thị đúng số gói và trạng thái “Hạn chót chờ xác nhận”; chưa cho giao việc. Ngày nộp không có giờ không được tự biến thành 23:59. Trích dẫn deadline chấp nhận khác biệt khoảng trắng/xuống dòng từ Word nhưng vẫn phải xuất hiện trong nguồn.

Leader có thể dùng **Áp dụng hạn chót chung** và **Chỉnh sửa gói việc** ngay trong bản nháp chia việc. Các ô tên, thời lượng, hạn chót, tóm tắt, yêu cầu, đầu ra, tiêu chí hoàn thành và ghi chú được nạp từ đề xuất để sửa tùy chọn. Tham chiếu và file nguồn được giữ; lưu sửa không gọi lại AI, không giao việc hay đổi lịch. Nếu vẫn thiếu hạn ở gói khác, bản nháp tiếp tục chờ. Khi sửa nội dung làm căn cứ AI cũ không còn phù hợp, mức quan trọng do AI đề xuất được chuyển về chưa xác định để đánh giá lại.

API `POST /api/teams/{teamId}/briefs/{proposalId}/edit` nhận `{ revision, tasks: [{ id, name, description, estimatedMinutes, deadline }] }`. Máy chủ kiểm tra chủ bản nháp, quyền leader hiện tại, membership, thời hạn, danh sách ID và giới hạn trường; trả bản nháp cùng revision mới. Draft chờ được lưu riêng 30 phút với file nguồn để leader chỉnh mà không đọc AI lại. Bản nháp đã sửa chỉ được giao bằng `confirm` có `revision` khớp; các tab cũ bị từ chối. Không cần migration mới. Leader vẫn có thể bổ sung mô tả để NORA phân tích lại nếu muốn.

- `PrivateTeamPlanningTests`: riêng tư, stale, idempotency, rollback, trả/hoàn tác, thay yêu cầu, tiến độ, chat và hai tài khoản cùng deadline có lịch khác nhau.
- `AvailabilityPolicyTests`: không tự dùng preset, hợp đệm, bảo toàn ngày và giờ đã có, khoảng ngắn và deadline qua đêm.
- `PrivateTeamSqlTests`: hai người xác nhận đồng thời trên SQL Server tạm; một người thành công, lịch người còn lại không đổi; chat hoạt động trên SQL Server.
- `PrivateTeamLiveTests`: opt-in AI thật, chỉ dùng tài khoản/lịch giả trong SQLite bộ nhớ.
- Frontend: popup mở ngay, xác nhận/hủy/stale, trả việc, chat/retry và Notes regression. Nguồn `frontend/src/notes.jsx` giữ nguyên.

Migration `PrivateTeamPlanning`, `TeamSharedChat`, `TeamCompletionReview`, `TeamCompletionAttachments`, `TeamTaskSourceDocuments` và `TeamTaskDesignatedRecipients` bổ sung bảng/token/link tài liệu/người được chỉ định; không chuyển hoặc xóa dữ liệu Notes/lịch cũ. Cần backup và áp dụng migration trước khi chạy API mới. Phạm vi lập lịch nhận việc hiện tại là deadline trong 30 ngày tới; chưa hỗ trợ nhiều người cùng nhận một task.
