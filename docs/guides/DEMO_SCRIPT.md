# Kịch bản demo PLANORA và trợ lý NORA

Phiên bản soạn ngày 30/10/2026, dựa trên code hiện có ở commit `51362b8` và các thay đổi trước đó. Đây là kịch bản trình diễn, không phải biên bản xác nhận mọi dịch vụ bên ngoài đã chạy thành công.

## 1. Mục tiêu và cách tổ chức

Thời lượng gợi ý: 20–25 phút, một người thuyết trình và thao tác. Nếu có hai người, người thứ hai phụ trách điện thoại, giữ các tab đã chuẩn bị và theo dõi thời gian.

Câu chuyện: một sinh viên có lịch học cố định, nhiều công việc cần hoàn thành, phát sinh một cuộc họp khẩn và có một phiên đã bỏ lỡ. PLANORA giúp chuyển đầu vào thành lịch khả thi, xử lý thay đổi và theo dõi kết quả.

Thông điệp xuyên suốt:

> “PLANORA giúp người dùng biến việc cần làm thành những khoảng thời gian có thể thực hiện, kiểm tra tác động của thay đổi và chủ động điều chỉnh khi kế hoạch bị gián đoạn.”

Không cần đi hết mọi nút. Ưu tiên chứng minh được: nhập liệu → kiểm tra → đánh giá phương án lập lịch → lưu → thay đổi → ghi nhận kết quả. Các phần tài khoản và quản trị có thể rút ngắn; thuật toán cần được làm rõ bằng phần so sánh phương án.

Với buổi bảo vệ nhấn mạnh tối ưu hóa hoạch định, **Cảnh 5A là phần trọng tâm**, không để thuật toán chỉ xuất hiện ở câu hỏi cuối. Chèn cảnh này sau khi phân biệt task và session; bỏ Cảnh 6, rút phần Google Calendar/admin để giữ tổng thời lượng khoảng 20–25 phút. Căn lại sự kiện countdown theo giờ trình bày thực tế của phiên bản kịch bản đã chọn.

## 2. Chuẩn bị trước buổi demo

### 2.1. Tài khoản và màn hình

- Tài khoản sinh viên demo riêng, đã xác minh email và đăng nhập thử thành công.
- Tài khoản admin riêng nếu có trình bày quản trị. Dùng cửa sổ hoặc hồ sơ trình duyệt khác để tránh thay đổi phiên đăng nhập của tài khoản sinh viên.
- Đặt múi giờ tài khoản `Asia/Ho_Chi_Minh`; kiểm tra giờ máy/điện thoại đúng. Không đổi đồng hồ hệ điều hành để ép countdown hoặc OAuth chạy.
- Chuẩn bị tab PLANORA, tab Google Calendar đúng tài khoản và trang CI đã hoàn thành. Để các tab cấu hình/credentials ra khỏi màn hình trình chiếu.
- Đặt zoom trình duyệt dễ đọc, đóng hộp thoại không liên quan và dùng dữ liệu học tập giả lập.
- AI và ảnh mẫu đã chạy thử. Có sẵn các đoạn nội dung trong mục 3 để dán.

### 2.2. Quy ước ngày giờ

- `D`: ngày demo theo múi giờ tài khoản.
- `T`: giờ bắt đầu phần trình diễn.
- “Ngày mai” trong prompt phải thực sự còn ở tương lai khi gửi.
- Các chỗ `[ngày ...]`, `[giờ ...]` phải thay bằng ngày giờ thật trước khi gửi; không dán nguyên dấu ngoặc.
- Khi nhắc đến một phiên đã được lập lịch, lấy thời gian thực trên màn hình. Không hứa planner luôn chọn một giờ cố định.

### 2.3. Dữ liệu nền

| Dữ liệu | Cách chuẩn bị | Mục đích |
| --- | --- | --- |
| Giờ rảnh | Buổi tối 18:00–23:00 cho ngày mai và các ngày cần dùng; phiên tối đa 90 phút, nghỉ 10 phút nếu phù hợp thiết lập hiện tại. | Có đủ chỗ cho bài demo và một lịch phát sinh. |
| Ảnh thời khóa biểu | Ảnh rõ, có ngày/tháng/năm và giờ bắt đầu/kết thúc; khoảng 4 buổi, gồm buổi đã kết thúc và buổi trong tương lai. Chọn lớp vào buổi sáng để không chiếm hết giờ làm việc tối. | Demo dán ảnh và cắt phần đã qua. |
| Ba công việc chính | Báo cáo SWP391 90 phút, ôn PRN212 45 phút, chuẩn bị slide 30 phút. | Tổng 165 phút, dễ kiểm tra và có dư thời gian xử lý phát sinh. |
| Một phiên chưa ghi nhận đã kết thúc | Chuẩn bị từ trước lúc phiên bắt đầu; thuộc task chưa hoàn thành, chưa có execution, còn deadline và có chỗ bù trong 48 giờ. | Demo ghi nhận bù. |
| Một sự kiện sắp bắt đầu | Tạo sự kiện vào gần đoạn trình bày Hôm nay, dài khoảng 10 phút. | Thấy “BẮT ĐẦU SAU” đổi sang “ĐẾM NGƯỢC KẾT THÚC”. |
| Một sự kiện nhắc điện thoại | Chỉ chuẩn bị nếu Calendar kết nối thật đã thành công. Chọn nhắc trước 2 phút rồi tạo sự kiện mới, tính giờ sao cho lời nhắc đến cuối buổi. | Kiểm tra thông báo của Google Calendar. |

Gợi ý chuẩn bị phiên bị lỡ: nếu bắt đầu demo lúc 14:00, chuẩn bị từ 13:20 một phiên 25 phút, dự kiến 13:25–13:50. Cần có giờ rảnh hợp lệ bao phủ khoảng này và không có lịch khác chặn. Khi vào demo, phiên đã qua và chưa xác nhận sẽ đủ điều kiện hiện ở Hôm nay. Không tạo lùi giờ bằng SQL trong lúc trình diễn.

Gợi ý countdown: dự kiến mở phần này vào khoảng phút 13, tạo sự kiện từ `T + 13 phút` đến `T + 23 phút`. Nếu nhịp demo thay đổi, trình bày trạng thái hiện tại đúng theo thời gian thực, không bắt khán giả chờ cả phút.

### 2.4. Điều kiện riêng của Google Calendar

- Trong PLANORA phải thấy kết nối đúng Gmail và đã đồng bộ được ít nhất một sự kiện thật trước buổi demo.
- Đã kiểm tra một lần thêm từ PLANORA sang Google và đổi giờ từ Google về PLANORA.
- Trên Google chọn đúng lịch phụ **PLANORA**, không chọn nhầm lịch chính.
- Điện thoại đăng nhập đúng Gmail, bật lịch PLANORA và quyền thông báo.
- Bản local thường cần khoảng 60–90 giây để nhận thay đổi từ Google; không mô tả như đồng bộ tức thì.
- Nếu kết nối chưa hoàn tất, phần này chuyển thành giới thiệu cấu hình hoặc video đã ghi rõ thời điểm kiểm thử. Không nói đã đồng bộ khi giao diện đang báo lỗi.

Hướng dẫn cấu hình: [Google Calendar](../integrations/GOOGLE_CALENDAR_SETUP.md).

## 3. Nội dung để dán trong demo

### P1 — Nhập thời khóa biểu từ ảnh

```text
Đây là thời khóa biểu của tôi. Hãy nhập các buổi học trong đúng khoảng ngày ghi trên ảnh, mỗi buổi một lần, không lặp. Bỏ các buổi đã kết thúc, giữ nguyên ngày giờ những buổi còn lại. Không dời buổi đã lỡ sang ngày khác.
```

Ảnh phải có ngày cụ thể. Nếu là bảng lặp theo thứ trong tuần, bổ sung ngày bắt đầu và số tuần thay cho câu “không lặp”. Nếu dùng tiết học, cung cấp bảng quy đổi tiết sang giờ.

### P2 — Ghi chú tạo công việc

```text
Kế hoạch học tập của tôi:
- Viết báo cáo SWP391, cần 90 phút, ưu tiên cao, hạn chót ngày mai lúc 23:00.
- Ôn PRN212, cần 45 phút, hạn chót ngày mai lúc 23:00.
- Chuẩn bị slide thuyết trình, cần 30 phút, hạn chót ngày mai lúc 23:00.
Đây là các công việc linh hoạt, có thể xếp trong giờ rảnh đã cài đặt.
```

### P3 — Câu hỏi đọc lịch

```text
Ngày mai tôi có những lịch gì? Hãy tóm tắt theo thứ tự thời gian và chỉ ra các khoảng trống trong giờ rảnh đã cài đặt.
```

### P4 — Yêu cầu thiếu thông tin

```text
Tôi cần chuẩn bị một bài thuyết trình mới. Giúp tôi lên lịch.
```

Nếu cần trả lời làm rõ:

```text
Bài thuyết trình mới này cần 60 phút, hạn chót ngày kia lúc 22:00. Hãy chỉ cho tôi xem phương án trước, chưa lưu.
```

### P5 — Cuộc họp khẩn trùng phiên linh hoạt

```text
Tôi có cuộc họp khẩn ngày [ngày thật], từ [giờ thật] đến [giờ thật]. Khung này đang trùng với phiên [tên công việc trên lịch]. Hãy ưu tiên cuộc họp, xếp lại phần việc bị ảnh hưởng trong giờ rảnh và giữ nguyên deadline. Cho tôi xem thay đổi trước khi lưu.
```

Chọn một phiên tương lai chưa khóa/chưa bắt đầu và còn thời gian hợp lệ để xếp lại. Dùng cuộc họp 30 phút nằm trong khoảng của phiên đó. Không chọn trùng lớp học cố định cho nhánh demo thành công chính.

### P6 — Kiểm tra sự kiện cố định được bảo vệ

```text
Cuộc họp khẩn vừa rồi trùng với buổi học [tên lớp] của tôi. Hãy cho biết các lựa chọn xử lý và chưa thay đổi lịch khi tôi chưa xác nhận.
```

Chỉ dùng nhánh này nếu đã chuẩn bị hai sự kiện thật sự trùng nhau. Không gửi P6 khi lịch hiện tại không có xung đột.

## 4. Kịch bản chính 20–25 phút

### Cảnh 1 — Mở đầu và nhu cầu thực tế | 00:00–01:00

**Màn hình:** trang giới thiệu hoặc Hôm nay của tài khoản demo.

**Lời nói:**

> “Em xin chào thầy cô. Nhóm em xin giới thiệu PLANORA, hệ thống hỗ trợ quản lý công việc và lập lịch cá nhân với trợ lý NORA.
>
> Một sinh viên thường phải kết hợp lịch học cố định, bài tập có hạn chót và những việc phát sinh. Chỉ ghi lại danh sách việc chưa trả lời được câu hỏi: mình sẽ làm việc đó vào lúc nào và có đủ thời gian không?
>
> Trong phần demo, em sẽ nhập lịch học, thêm các việc cần làm, xử lý một cuộc họp khẩn và ghi nhận lại khi bỏ lỡ một phiên.”

**Thao tác:** đăng nhập bằng tài khoản đã chuẩn bị; mở Hôm nay. Không dành thời gian chờ OTP trừ khi hội đồng yêu cầu riêng.

**Cần chỉ ra:** trạng thái trống hoặc dữ liệu demo đã chuẩn bị. Nếu đã có dữ liệu, nói rõ đây là tài khoản có dữ liệu mẫu.

### Cảnh 2 — Thiết lập thời gian có thể làm việc | 01:00–02:00

**Màn hình:** Giờ rảnh.

**Thao tác:** chỉ các khoảng 18:00–23:00 đã chuẩn bị; mở một khoảng để thấy có thể chỉnh; lưu nếu có thay đổi thật. Nêu một buổi học là khoảng bận cố định.

**Lời nói:**

> “Trước khi lập lịch, người dùng cho hệ thống biết khi nào mình thực sự có thể làm việc. Trong tình huống này, em dành buổi tối cho công việc cá nhân, còn các buổi học là những cam kết cố định.
>
> Những khoảng chưa được khai báo không tự động được coi là giờ rảnh. Khi lập kế hoạch, hệ thống còn kiểm tra khoảng bận, thời gian nghỉ, deadline và các phiên đã khóa.”

**Kết quả mong đợi:** giờ rảnh được lưu và hiển thị lại đúng. Chưa nhận xét planner trước khi thực sự tạo đề xuất.

### Cảnh 3 — Dán ảnh thời khóa biểu vào NORA | 02:00–04:00

**Màn hình:** NORA · Trợ lý AI.

**Thao tác:** tắt Tự động lưu cho lần nhập đầu để giải thích bước kiểm tra. Ctrl+V ảnh mẫu, chỉ bản xem trước ảnh, dán P1 và gửi. Nếu NORA hỏi thêm ngày/giờ, cung cấp dữ liệu còn thiếu. Kiểm tra môn học, ngày, giờ và số buổi. Khi phương án đúng và giao diện cho áp dụng, xác nhận rồi mở Lịch làm việc.

**Lời nói:**

> “Người dùng có thể dán ảnh thời khóa biểu trực tiếp vào khung chat. Ảnh được giữ trong cuộc trò chuyện và được gửi cùng yêu cầu tới mô hình thị giác khi em bấm Gửi.
>
> Em yêu cầu giữ đúng ngày giờ trên ảnh. Những buổi đã kết thúc được bỏ qua; hệ thống không kéo chúng sang tuần sau để bù. Em sẽ kiểm tra lại các buổi còn lại trước khi lưu.”

**Cần chỉ ra:** ảnh vẫn hiển thị trong tin nhắn, số buổi đã qua được thông báo nếu có, các lớp tương lai giữ ngày giờ gốc.

**Nếu AI hỏi lại:**

> “Ảnh chưa đủ thông tin để lập lịch an toàn nên NORA yêu cầu bổ sung. Hệ thống chưa tự đoán giờ học hoặc ngày áp dụng.”

### Cảnh 4 — Từ ghi chú sang công việc và lịch | 04:00–07:30

**Màn hình:** Nhập việc.

**Thao tác:** tạo ghi chú mới, tiêu đề “Kế hoạch học tập demo”, dán P2. Chỉ trạng thái tự lưu/bản nháp. Bấm Xem trước công việc. Xem từng mục được nhận diện, kiểm tra thời lượng/deadline/ưu tiên. Có thể đổi slide từ 30 lên 40 phút để cho thấy chỉnh sửa được, rồi giữ giá trị đã chọn trong phần còn lại của demo.

Bấm **Xem lịch đề xuất** ở luồng activities. Chỉ ngày, phiên, cảnh báo và vùng xác nhận chưa lưu. Sau đó bấm **Lưu sự kiện & công việc**. Nếu phiên bản kết quả đi vào luồng task cũ, theo nút thật **Tạo công việc & xem lịch → Lưu lịch này**, và giải thích việc đã được tạo trước khi lưu lịch ở nhánh đó.

**Lời nói:**

> “Đầu vào là một ghi chú tự nhiên. NORA giúp tách tên việc, thời lượng, hạn chót và mức ưu tiên để người dùng kiểm tra lại.
>
> Kết quả phân tích và lịch thử chưa đồng nghĩa với việc đã lưu. Em có thể sửa thời lượng hoặc bỏ chọn một việc trước khi xác nhận.
>
> Bây giờ em lưu nhóm hoạt động này. Sau khi thao tác thành công, em sẽ đối chiếu lại ở danh sách công việc và lịch làm việc.”

**Cần chỉ ra:** trước xác nhận là preview; sau xác nhận có bản ghi thật. Không chỉ dựa vào câu trả lời AI để kết luận đã lưu.

**Dừng nhánh thành công nếu:** có cảnh báo thiếu thời gian hoặc deadline. Chỉnh dữ liệu/giờ rảnh rồi xem lại; không nhấn ép lưu.

### Cảnh 5 — Kiểm chứng lịch vừa lưu | 07:30–09:00

**Màn hình:** Công việc → Lịch làm việc.

**Thao tác:** mở một công việc, chỉ tên, estimate, deadline và tiến độ; chuyển lịch tuần hoặc danh sách; chỉ lớp học cố định và phiên của công việc. Mở một phiên để thấy nó thuộc công việc nào.

**Lời nói:**

> “Một công việc là kết quả cần hoàn thành. Một phiên là khoảng thời gian dành cho công việc đó. Công việc dài có thể cần nhiều phiên, còn buổi học cố định là một loại cam kết khác.
>
> Em đối chiếu giờ bắt đầu/kết thúc và deadline ở đây để xác nhận đề xuất đã trở thành lịch thực tế. Các màu giúp phân biệt lịch đã qua, đang diễn ra và chưa đến.”

Nếu khán giả hỏi vì sao chọn slot này, giải thích ngắn: lọc ràng buộc cứng rồi chấm điểm các phương án khả thi. Không gọi đây là nghiệm tối ưu toàn cục.

### Cảnh 5A — Tối ưu hóa hoạch định: so sánh phương án | 4–5 phút

Đây là demo bộ lập lịch trên dữ liệu có kiểm soát. Dùng tài khoản demo riêng hoặc trạng thái chỉ có một công việc để các lịch khác không làm nhiễu kết quả.

**Phần này nằm ở đâu trên giao diện?**

- Ngay trong **NORA**, chọn **Tối ưu lịch** hoặc nhắn: “Tối ưu lịch ngày mai cho tôi” / “Xếp lại lịch tuần này”. NORA gọi bộ lập lịch, hiện **Phương án tối ưu lịch** với giờ cũ và giờ đề xuất. Chọn **Xác nhận kế hoạch** để lưu; sau đó có thể **Hoàn tác** nếu chưa có thay đổi hoặc phiên bắt đầu.
- Phạm vi tính theo múi giờ tài khoản, tuần bắt đầu thứ Hai. Phần thời gian đã qua được bỏ qua; “hôm nay” kết thúc đúng nửa đêm. Phiên đã khóa/đang chạy, sự kiện cố định và lịch ngoài phạm vi được bảo vệ. Nếu không có phương án khác hoặc thiếu thời gian, NORA nói rõ và chưa lưu thay đổi. Đây là tối ưu bằng heuristic có chấm điểm, không bảo đảm nghiệm tối ưu toàn cục.
- Hôm nay → **Xem lịch đề xuất**, hoặc Lịch làm việc → **Lập kế hoạch**.
- Hộp **Tạo kế hoạch mới** có thời điểm Bắt đầu, Số ngày và nút **Tạo đề xuất**.
- Nhập việc → **Xem lịch đề xuất** cũng đi qua bộ lập lịch, nhưng ví dụ dưới đây tạo task thủ công để thể hiện rõ kết quả không phụ thuộc diễn giải của AI.
- **Thử nhận thêm việc → Phân tích kịch bản** dùng cho phần so sánh trước/sau khi nhận thêm khối lượng.

**Chuẩn bị dữ liệu:**

| Thành phần | Giá trị |
| --- | --- |
| Múi giờ | Asia/Ho_Chi_Minh. |
| Giờ rảnh ngày mai | 09:00–11:00 và 19:00–21:00. |
| Công việc duy nhất | “Viết báo cáo — demo hoạch định”, 60 phút, ưu tiên Vừa; chưa hoàn thành, chưa có phiên, không phụ thuộc. |
| Hạn chót ban đầu | Ngày mai 22:00; bật **Deadline cứng: không cho xếp lịch sau hạn chót**. |
| Độ dài phiên | Phiên tối thiểu không quá 60 phút, phiên tối đa ít nhất 60 phút; giới hạn ngày ít nhất 60 phút. |
| Lịch bận | Không có sự kiện hoặc phiên khóa trong hai khoảng thử. |
| Phạm vi tạo đề xuất | Bắt đầu ngày mai 08:00, Số ngày = 1. |
| Hệ số cá nhân hóa | Bỏ chọn **Chừa thời gian dự phòng theo khả năng thực hiện** và **Dùng thời lượng đã cá nhân hóa** trong ví dụ này để giữ nguyên nhu cầu 60 phút. |

Việc bỏ hai hệ số chỉ phục vụ so sánh trên cùng dữ liệu; các kiểm tra giờ rảnh, khoảng bận, deadline, phiên và giới hạn ngày vẫn có hiệu lực. Không chọn ưu tiên Khẩn cho task này vì xử lý khẩn có chính sách riêng.

**Lượt A — Ưu tiên buổi sáng:**

1. Vào Giờ rảnh → **Buổi làm việc ưu tiên = Sáng** → **Lưu tùy chọn**.
2. Mở Lập kế hoạch, đặt đúng phạm vi và hai lựa chọn như bảng.
3. Bấm **Tạo đề xuất**. Với đúng dữ liệu độc lập trên, phương án mong đợi là 09:00–10:00.
4. Ghi lại/chụp phương án A, đóng preview và **chưa lưu lịch**.

**Lượt B — Giữ đầu vào, chỉ đổi ưu tiên sang tối:**

1. Đổi **Buổi làm việc ưu tiên = Tối**, lưu tùy chọn.
2. Tạo lại đề xuất với cùng ngày bắt đầu, horizon và hai hệ số đang tắt.
3. Phương án mong đợi chuyển sang 19:00–20:00, trong khi 09:00–11:00 vẫn là một khoảng rảnh hợp lệ.
4. Ghi lại phương án B và **chưa lưu lịch**.

**Lời nói cho A/B:**

> “Cùng một công việc và cùng hai khoảng rảnh, em chỉ đổi giờ làm việc ưu tiên. Phương án thay đổi từ sáng sang tối dù khoảng sáng vẫn còn chỗ. Đây là ví dụ hệ thống đánh giá các ứng viên thay vì luôn chọn khoảng trống đầu tiên.”

**Lượt C — Deadline cứng thắng sở thích giờ làm:**

1. Giữ ưu tiên buổi tối, sửa deadline của công việc thành **ngày mai 12:00**, vẫn là deadline cứng.
2. Tạo lại đề xuất trên cùng phạm vi. Phương án mong đợi quay về 09:00–10:00; 19:00–20:00 không còn là ứng viên hợp lệ.
3. Đối chiếu ba phương án. Chỉ bấm **Lưu lịch này** nếu muốn lưu C ở cuối cảnh.

Không lưu A/B giúp tránh một phiên hiện có gây ảnh hưởng khi sửa deadline ở C. Nếu có thêm dữ liệu khác, cần đối chiếu lý do/cảnh báo thật thay vì khẳng định luôn ra đúng những giờ ví dụ.

**Lời nói cho C:**

> “Giờ làm việc ưa thích là mục tiêu mềm. Deadline cứng là ràng buộc bắt buộc. Dù người dùng thích làm buổi tối, hệ thống phải chọn một phương án còn kịp hạn, hoặc báo không thể xếp nếu không có khoảng phù hợp.”

**Lượt D — Kiểm tra trước khi nhận thêm việc, nếu còn thời gian:**

Mở **Thử nhận thêm việc**, nhập một công việc giả định 600 phút với deadline ngày mai 12:00 rồi bấm **Phân tích kịch bản**. Chỉ vùng **Trước và sau**, cảnh báo và **Xem lịch giả định**. Không bấm Tạo công việc từ kịch bản trong cảnh này. Với bộ dữ liệu chỉ có hai khoảng rảnh nêu trên, yêu cầu lớn này không có đủ thời gian; kết quả cần thể hiện thiếu hụt/cảnh báo, lịch thật vẫn giữ nguyên.

> “Ngoài tạo một phương án, hệ thống hỗ trợ đánh giá tác động trước khi nhận thêm việc. Khi nhu cầu vượt khả năng thực hiện, người dùng thấy cảnh báo trước khi quyết định.”

**Giải thích kỹ thuật khi hội đồng hỏi:**

1. Lấy giờ rảnh, trừ lịch bận và các khoảng phải bảo vệ, xét deadline và điều kiện của task.
2. Xác định các ứng viên, chấm điểm và ưu tiên ứng viên có penalty thấp theo heuristic hiện tại.
3. Phân bổ thời lượng và kiểm tra lại tính hợp lệ; trả đề xuất/cảnh báo trước khi áp dụng.

```text
Penalty = 10000 × LatenessPenalty
        +     2 × IdleTimePenalty
        +  1000 × FragmentationPenalty
        −   500 × PreferredPeriodBonus
```

`LatenessPenalty` còn có thành phần mức sát deadline; `IdleTimePenalty` xét khoảng dư khó sử dụng và mức chờ; các tên này không phải chỉ số hiệu suất đã hiệu chuẩn. Công việc khẩn hoặc bị dời do khẩn có thứ tự/chính sách ưu tiên riêng. Không suy diễn rằng mọi nhánh đều chạy đúng cùng một phép sắp điểm.

Giao diện hiện có phiên đề xuất, mã lý do và cảnh báo; chưa có bảng hiển thị toàn bộ điểm ứng viên hoặc tổng objective của cả lịch. Không chỉ vào UI rồi nói hệ thống đang hiển thị các con số penalty nếu thực tế chưa có.

Phát biểu chính xác: **heuristic lập lịch có hàm đánh giá đa tiêu chí**, chưa chứng minh tối ưu toàn cục hoặc tối ưu Pareto cho toàn bộ lịch.

### Cảnh 6 — NORA hỏi lại khi chưa đủ thông tin | 09:00–10:00

**Màn hình:** NORA, chế độ xem trước.

**Thao tác:** gửi P4. Chỉ câu hỏi làm rõ nếu thiếu thời lượng/deadline. Nếu cần, trả lời bằng đoạn bổ sung của P4 nhưng không áp dụng yêu cầu mới này.

**Lời nói:**

> “Với yêu cầu chưa có đủ thông tin, hệ thống cần hỏi lại trước khi xếp. Em muốn cho thấy ranh giới giữa hỗ trợ diễn giải và tự quyết định thay người dùng.
>
> Đây vẫn là phương án chưa lưu. Em có thể bổ sung dữ liệu hoặc dừng mà không làm thay đổi kế hoạch đang có.”

Nếu model diễn giải khác dự kiến, đọc đúng trạng thái thật trên màn hình; không khẳng định hành vi hỏi lại luôn xảy ra với mọi cách diễn đạt.

### Cảnh 7 — Tình huống khẩn cấp và xếp lại phần bị ảnh hưởng | 10:00–12:30

**Màn hình:** Lịch làm việc để chọn phiên, sau đó NORA.

**Thao tác:** lấy một phiên tương lai chưa khóa làm ví dụ, thay P5 bằng giờ thật và gửi. Đối chiếu phương án trước/sau, các mục bị dời, deadline và cảnh báo. Chỉ xác nhận nếu phương án khả thi; mở lịch kiểm tra sau khi lưu.

**Lời nói:**

> “Bây giờ có một cuộc họp khẩn trùng với phiên tự học. NORA cần ưu tiên yêu cầu mới và tìm chỗ khác cho phần việc linh hoạt bị ảnh hưởng.
>
> Em kiểm tra những gì sẽ bị đổi trước khi xác nhận. Những buổi học cố định, phiên đã khóa và deadline vẫn là các điều kiện phải được tôn trọng.
>
> Khi không còn phương án hợp lệ, hệ thống cần báo rõ để người dùng điều chỉnh, thay vì xác nhận một lịch mà thực tế không thực hiện được.”

**Nhánh mở rộng, nếu đủ thời gian:** dùng P6 với hai lịch cố định đã chuẩn bị. Chỉ các lựa chọn và bước cần xác nhận; không minh họa bằng việc tự xóa/dời một lớp học.

### Cảnh 8 — Countdown tự chuyển theo lịch | 12:30–14:00

**Màn hình:** Hôm nay.

**Thao tác:** mở thẻ Phiên tập trung. Nếu còn vài giây trước sự kiện mẫu, chỉ “BẮT ĐẦU SAU”; khi đến giờ, chỉ “ĐẾM NGƯỢC KẾT THÚC” và thanh tiến độ thời gian. Nếu sự kiện đã bắt đầu, trình bày ngay trạng thái hiện tại.

**Lời nói:**

> “Khung tập trung này bám theo thời gian của lịch. Đến giờ, hệ thống tự chuyển sang lịch đang diễn ra và đếm ngược tới lúc kết thúc.
>
> Đây là tiến độ thời gian của lịch. Việc đồng hồ chạy hết chưa chứng minh người dùng đã làm xong; kết quả thực tế được ghi nhận ở thao tác riêng.”

Không bấm Bắt đầu chỉ để làm đồng hồ theo lịch chạy. Timer ghi nhận thực thi thủ công là một cơ chế khác, chỉ mở khi cần giải thích sâu.

### Cảnh 9 — Ghi nhận phiên đã lỡ và tự tìm phiên bù | 14:00–16:00

**Màn hình:** Hôm nay, thẻ **Bạn đã làm được đến đâu?**.

**Thao tác:** chọn phiên mẫu đã kết thúc; bấm **Bị lỡ / Chưa làm**. Đọc thông báo thực tế. Nếu tìm được phiên bù, mở lịch đối chiếu giờ mới. Có thể giới thiệu thêm hai nút Đã hoàn thành và Hoàn thành một phần mà không bấm hết trên cùng phiên.

**Lời nói:**

> “Không phải lúc nào người dùng cũng mở ứng dụng và bấm timer. Với phiên đã qua nhưng chưa có kết quả, hệ thống cho phép xác nhận đã làm, làm một phần hoặc chưa làm.
>
> Trong ví dụ này em chọn chưa làm. PLANORA tìm khoảng phù hợp trong 48 giờ tới, vẫn giữ deadline và các cam kết khác. Việc xác nhận là dữ liệu do người dùng cung cấp; hệ thống không suy đoán rằng cứ hết giờ là đã hoàn thành.”

**Nếu không có chỗ bù:**

> “Ở dữ liệu hiện tại, chưa có khung đủ điều kiện. Hệ thống giữ nguyên deadline và báo cần điều chỉnh thay vì dời một cam kết khác.”

Đó là kết quả hợp lệ nhưng không thay thế nhánh demo thành công. Cần chạy thử dữ liệu trước để bảo đảm có một khoảng bù thực sự nếu muốn minh họa auto-heal.

### Cảnh 10 — Trung tâm thông báo và phân tích | 16:00–17:30

**Màn hình:** Thông báo → Hôm nay/Tiến độ.

**Thao tác:** mở chuông, lọc Chưa đọc, đánh dấu một mục đã đọc và chỉ số đếm cập nhật. Nếu dùng **Thử popup**, nói rõ đó là nút thử giao diện. Chuyển sang biểu đồ lịch bảy ngày, chỉ ngày có lịch và các mốc sắp tới.

**Lời nói:**

> “Thông báo tập trung những việc cần chú ý. Đánh dấu đã đọc chỉ ghi nhận đã xem, không tự hoàn thành công việc.
>
> Ở phần phân tích, người dùng thấy khối lượng đã có lịch và các mốc sắp tới. Khi chỉ có thời khóa biểu, hệ thống vẫn có thể hiển thị tổng quan lịch; độ khớp ước lượng cần dữ liệu thực tế của các phiên đã được ghi nhận.”

Phân biệt popup trong trang với thông báo nền trên điện thoại. Không trình bày Thử popup như bằng chứng của một lời nhắc thật đã tới hạn.

### Cảnh 11 — Google Calendar hai chiều | 17:30–21:00, có điều kiện

Chỉ thực hiện trực tiếp khi mục 2.4 đã được kiểm tra thành công.

**Màn hình:** Cài đặt → Kết nối; Lịch làm việc; Google Calendar.

**Thao tác:**

1. Chỉ Gmail và trạng thái Đã đồng bộ, không mở Client Secret hoặc token.
2. Trong PLANORA tạo sự kiện “Trao đổi đồ án demo”, ngày mai 15:00–15:30, ở khoảng không trùng lịch khác.
3. Khi trạng thái đã đồng bộ, mở đúng ngày và lịch PLANORA trên Google để đối chiếu.
4. Đổi sự kiện đó trên Google sang 15:30–16:00.
5. Chuyển sang cảnh quản trị hoặc tổng kết trong lúc đợi; sau khoảng một chu kỳ polling, trở lại PLANORA và đối chiếu. Có thể tải lại trang khi không có form đang nhập.
6. Nếu lịch nhắc điện thoại đã đến hạn trong buổi demo, cho thấy thông báo thật trên thiết bị. Nếu chưa đến, chỉ cấu hình lời nhắc và dùng video đã kiểm thử làm dự phòng.

**Lời nói:**

> “PLANORA có thể đồng bộ với một lịch riêng trong Google Calendar. Ở chiều thứ nhất, em tạo sự kiện tại PLANORA và đối chiếu bản đã xuất hiện trên Google.
>
> Ở chiều ngược lại, em đổi giờ trực tiếp trên Google. Sau khi đồng bộ, PLANORA cập nhật theo thay đổi đó.
>
> Bản local đang kiểm tra thay đổi theo chu kỳ nên cần thời gian chờ. Sau khi sự kiện và lời nhắc đã đến Google, ứng dụng Google Calendar trên điện thoại có thể nhắc dù website PLANORA đã đóng, với điều kiện thiết bị cho phép thông báo.”

Nếu muốn thử xóa, dùng chính sự kiện mẫu này, không xóa một task đang được dùng trong cảnh khác. Xóa Google event liên kết phiên không đồng nghĩa xóa công việc gốc.

Nếu hội đồng hỏi Google Tasks: bản hiện tại chưa có đồng bộ Google Tasks. Công việc được PLANORA quản lý; Calendar đang nhận sự kiện và phiên có giờ.

### Cảnh 12 — Quản trị, động lực sử dụng và kết luận | 21:00–24:00

**Màn hình:** Quản trị ở cửa sổ admin riêng, rồi trở về tài khoản sinh viên.

**Thao tác:** chỉ số liệu tổng hợp, trạng thái hệ thống và danh sách tài khoản. Nếu demo khóa/mở khóa, chỉ dùng một tài khoản mẫu riêng. Sau đó mở Cài đặt → Bảo mật để giới thiệu Tủ đồ NORA; tài khoản chưa đủ XP chỉ xem trạng thái khóa.

**Lời nói về admin:**

> “Quản trị viên xem tình trạng vận hành và quản lý tài khoản. Màn hình này không cung cấp nội dung ghi chú, công việc hoặc lịch riêng của từng người dùng.
>
> Các chỉ số CPU/RAM đang mô tả tiến trình ứng dụng theo dữ liệu hệ thống cung cấp; em không diễn giải chúng thành tài nguyên của riêng máy chủ database.”

**Lời nói về XP, nếu trình bày:**

> “Điểm kinh nghiệm và trang phục NORA tạo thêm động lực quay lại ứng dụng. XP được cấp khi đáp ứng điều kiện ghi nhận hoàn thành; đồng hồ hết giờ không tự cộng điểm. Phần này dùng thưởng nội bộ, không phải thanh toán tiền thật.”

**Lời kết:**

> “Qua tình huống vừa rồi, PLANORA đã thể hiện luồng từ đầu vào đến thực thi: nhận lịch học, chuyển ghi chú thành việc, đề xuất và lưu lịch, xử lý phát sinh, rồi ghi nhận lại kết quả để điều chỉnh kế hoạch.
>
> AI hỗ trợ diễn giải yêu cầu. Việc một thay đổi có được lưu hay không còn được kiểm tra bằng quy tắc nghiệp vụ phía máy chủ. Người dùng có thể đối chiếu trạng thái thực trên lịch và giữ quyền quyết định ở những thay đổi cần xác nhận.
>
> Nhóm em xin kết thúc phần demo và sẵn sàng trao đổi thêm về thuật toán, kiến trúc và các trường hợp ngoại lệ.”

## 5. Bản rút gọn 10 phút

| Thời gian | Nội dung bắt buộc |
| --- | --- |
| 0:00–0:45 | Nêu nhu cầu và tài khoản demo. |
| 0:45–2:00 | Giới thiệu đầu vào từ Note/ảnh và công việc đã chuẩn bị; chỉ nhanh giờ rảnh/ràng buộc. |
| 2:00–4:30 | Cảnh 5A: so sánh đổi giờ ưa thích, sau đó siết deadline cứng; đối chiếu trước khi lưu. |
| 4:30–6:30 | Yêu cầu khẩn trùng phiên linh hoạt, xem tác động và lưu phương án hợp lệ. |
| 6:30–7:30 | Countdown theo lịch và phân biệt với hoàn thành thực tế. |
| 7:30–9:00 | Ghi nhận phiên bị lỡ, chỉ phiên bù nếu tìm được. |
| 9:00–10:00 | Google Calendar đã chuẩn bị sẵn hoặc tổng quan phân tích, rồi kết luận. |

Không thêm đăng ký/OTP, đổi theme, tìm kiếm nâng cao và thanh toán vào bản 10 phút. Các tính năng này không giúp chứng minh trực tiếp câu chuyện lập lịch.

## 6. Các câu hỏi thường gặp khi bảo vệ

**“AI có tự quyết định và ghi thẳng vào database không?”**

> “AI giúp diễn giải yêu cầu thành dữ liệu có cấu trúc. Server kiểm tra quyền sở hữu, phiên bản, thời gian và các ràng buộc trước khi lưu. Có chế độ xem trước và bước xác nhận cho những thay đổi cần quyết định của người dùng.”

**“Thuật toán có phải tối ưu toàn cục không?”**

> “Hiện tại là heuristic có hàm chấm điểm ứng viên. Các ràng buộc cứng được kiểm tra trước; điểm phạt cân nhắc deadline, thời gian dư khó tận dụng, mức phân mảnh và giờ ưa thích. Nhóm chưa chứng minh nghiệm tối ưu toàn cục.”

Khi cần đối chiếu code: `ScheduleSlotCandidate.PenaltyScore = 10000 × LatenessPenalty + 2 × IdleTimePenalty + 1000 × FragmentationPenalty − 500 × PreferredPeriodBonus`. Các thành phần có cách chuẩn hóa riêng trong code; không coi chúng là các hệ số khoa học đã được hiệu chuẩn thực nghiệm.

**“Hệ thống học từ thực tế như thế nào?”**

> “Hệ thống có thể hiệu chỉnh ước lượng từ quan sát actual/estimate bằng EMA, ưu tiên dữ liệu mới hơn. Tài khoản ít mẫu dùng hệ số mặc định. Một lần tick hoàn thành hoặc đồng hồ chạy hết không đủ để chứng minh mô hình đã học chính xác.”

Theo code hiện tại, EMA dùng alpha 0,2 và giới hạn bias 0,5–3; dưới ba mẫu hợp lệ vẫn dùng bias 1. Nếu muốn trình bày biểu đồ học, chuẩn bị tài khoản có quan sát thực hiện mẫu đã ghi nhận và nói rõ đó là dữ liệu demo.

**“Khẩn cấp có được dời mọi lịch không?”**

> “Khẩn cấp được ưu tiên trong xử lý yêu cầu và tìm phương án. Những phiên linh hoạt có thể được xếp lại khi hợp lệ; lịch cố định, phiên khóa/đang thực hiện và deadline vẫn cần được bảo vệ hoặc có quyết định rõ ràng. Khi không thể đáp ứng, hệ thống báo chưa thực hiện.”

**“Hết giờ mà người dùng chưa làm thì sao?”**

> “Countdown biểu diễn thời gian theo lịch. Với phiên chưa có kết quả, người dùng có thể ghi nhận hoàn thành, một phần hoặc chưa làm; hệ thống tìm phiên bù khi còn điều kiện.”

**“Google Calendar đồng bộ toàn bộ lịch Google cá nhân chưa?”**

> “Bản đầu đồng bộ hai chiều trong lịch phụ PLANORA. Các lịch khác chưa được đọc mặc định. Lịch lặp/cả ngày được lấy về làm khoảng bận và chỉnh trên Google; những giới hạn này được nêu trong hướng dẫn.”

**“Nếu cả hai bên cùng sửa một sự kiện?”**

> “Hệ thống giữ bản nền và đối chiếu thay đổi. Những sửa độc lập có thể hợp nhất; cùng sửa một trường thì cần lựa chọn phiên bản. Google etag được dùng để tránh ghi đè một bản remote đã đổi.”

**“Hai người cùng lưu vào một tài khoản thì sao?”**

> “Workspace dùng kiểm soát phiên bản lạc quan. Một bản đã cũ nhận xung đột để tải lại/xem trước lại; không thông báo đã lưu khi transaction không thành công.”

**“Admin có xem được nội dung cá nhân không?”**

> “Các endpoint admin hiện tập trung vào thông tin tài khoản và số liệu vận hành tổng hợp. Vai trò admin không tự bỏ qua bộ lọc chủ sở hữu của workspace. Đây là kiểm soát truy cập của ứng dụng, không phải tuyên bố mọi dữ liệu đều được mã hóa đầu cuối.”

**“Các chức năng đã kiểm thử thế nào?”**

> “Nhóm có kiểm thử frontend, unit, integration và SQL Server; CI của commit nền demo đã thành công. Kiểm thử tự động nhiều phần dùng provider giả lập, nên việc nhận diện một ảnh thật hoặc nhận popup trên một điện thoại vẫn cần được thử thực tế.”

## 7. Phương án khi có sự cố

| Sự cố | Cách xử lý trên sân khấu | Cách trình bày trung thực |
| --- | --- | --- |
| AI phản hồi chậm | Chuyển sang ghi chú đã chuẩn bị; giới hạn chờ khoảng 15–20 giây trước khi chuyển nhánh. | “Lượt xử lý này phụ thuộc dịch vụ AI. Em chuyển sang dữ liệu đã chuẩn bị để tiếp tục minh họa phần kiểm tra và lập lịch.” |
| AI thiếu thông tin | Bổ sung đúng thời lượng/ngày giờ; không ép áp dụng khi dữ liệu chưa đủ. | “Hệ thống đang yêu cầu làm rõ trước khi lưu.” |
| Đề xuất thiếu thời gian | Chỉ cảnh báo, thêm giờ rảnh hoặc giảm phạm vi theo tình huống thật rồi xem trước lại. | “Đây là phương án chưa khả thi; lịch chưa được lưu.” |
| Lịch đã đổi nên preview cũ | Tạo lại đề xuất và kiểm tra. | “Bản xem trước đã cũ; server yêu cầu kiểm tra lại để tránh ghi đè lịch mới.” |
| Chưa có card ghi nhận bù | Chuyển sang tài khoản mẫu đã chuẩn bị hoặc bỏ cảnh; không tạo dữ liệu quá khứ trực tiếp để che lỗi. | “Tài khoản này hiện không có phiên đủ điều kiện cần xác nhận.” |
| Calendar chưa kết nối | Chỉ màn hình cấu hình; dùng video kiểm thử đã ghi rõ thời điểm nếu có. | “Phần kết nối thật chưa hoàn tất trên tài khoản này; em không trình bày như một lượt đồng bộ thành công.” |
| Chờ chiều Google → PLANORA | Chuyển sang giới thiệu quản trị/thuật toán rồi quay lại sau một chu kỳ. | “Local đang nhận thay đổi theo chu kỳ.” |
| Điện thoại không popup | Kiểm tra sự kiện đã tới Google và giờ nhắc; trình bày cấu hình hoặc video thử trước. | “Sự kiện đã đồng bộ, còn thông báo thiết bị phụ thuộc quyền và cài đặt của điện thoại.” |
| Mất mạng | Giữ màn hình đã tải; nếu API/database local còn hoạt động, tiếp tục các thao tác local được máy chủ xác nhận. Nếu mất cả kết nối API, dùng video dự phòng và không tuyên bố thay đổi mới đã được lưu. | “Dữ liệu đang xem là dữ liệu đã tải; thao tác mới chỉ được coi là đã lưu khi máy chủ xác nhận.” |

## 8. Những điều tránh nói quá trong demo

- Chưa đo thì không cam kết mọi yêu cầu dưới 100 ms, ảnh nào cũng đọc đúng hoặc đạt một tỷ lệ chính xác cố định.
- Không gọi việc kiểm tra slot/chấm điểm heuristic là chứng minh tối ưu toàn cục.
- Không nói countdown tự đo được sự tập trung, tự hoàn thành task hoặc tự cộng XP.
- Không nói popup thử là thông báo thật; popup trong trang khác thông báo của Google Calendar app.
- Không nói đã có Google Tasks, đồng bộ toàn bộ mọi lịch Google, thanh toán tiền thật hoặc tính năng xuất ICS trên giao diện chính.
- Không nói tạo được phiên bù nếu thực tế không tìm thấy chỗ hợp lệ.
- Không nói mọi nội dung cá nhân đã được mã hóa đầu cuối chỉ vì admin không có màn hình đọc nội dung.
- Không đổi dữ liệu đang chạy bằng DevTools/SQL rồi trình bày như hành vi tự động vừa xảy ra.

## 9. Checklist trước giờ trình diễn

- [ ] Chạy thử toàn bộ luồng trên cùng bộ dữ liệu, chỉ đổi các ngày giờ cần cập nhật.
- [ ] Ảnh đủ rõ, có buổi quá khứ và tương lai theo đúng ngày demo.
- [ ] Giờ rảnh còn đủ sau khi thêm lớp, việc, cuộc họp khẩn và phiên bù.
- [ ] Lịch bị dời là phiên linh hoạt; không dùng một phiên đã khóa/đang ghi nhận trong nhánh thành công chính.
- [ ] Có một phiên đã kết thúc, chưa có execution, thuộc task còn hoạt động cho cảnh ghi nhận bù.
- [ ] Countdown được căn vào thời gian trình bày, không đổi giờ hệ điều hành.
- [ ] Kết nối Google đã kiểm tra đủ hai chiều nếu đưa vào demo live.
- [ ] Lời nhắc điện thoại được đặt trước khi tạo sự kiện mới và đã đồng bộ sang Google.
- [ ] Có prompt, ảnh, bản nháp và video dự phòng sẵn.
- [ ] Không có cửa sổ credentials hoặc hộp thư cá nhân trên màn hình trình chiếu.

## 10. Tài liệu đối chiếu

- [Hướng dẫn người dùng](USER_GUIDE.md): một số mô tả luồng cũ còn tồn tại; đối chiếu nút thực tế khi trình diễn.
- [Luồng Note](../features/NOTE_FLOW_2026-10-25.md).
- [NORA và ảnh thời khóa biểu](../features/NORA_TIMETABLE_IMAGES.md).
- [Nhắc nhở và countdown theo lịch](../features/LIVE_REMINDERS_AND_SCHEDULE_FOCUS.md).
- [Kiến trúc, thuật toán và ghi nhận bù](../features/WORKSPACE_UPGRADE_PHASES_1_TO_4.md).
- [Google Calendar: cấu hình và giới hạn](../integrations/GOOGLE_CALENDAR_SETUP.md).
