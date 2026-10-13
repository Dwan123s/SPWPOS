# Nghiên cứu và thiết kế Getting Started cho PLANORA

Khảo sát ngày 13/10/2026, chọn 8 trang chính thức có sản phẩm gần với quản lý công việc, ghi chú hoặc lập lịch. Phạm vi là cách trình bày nội dung, minh họa và đường dẫn bắt đầu/đăng nhập. Các nhận định thiết kế dưới đây là đánh giá để áp dụng cho PLANORA, không phải kiểm toán công nghệ frontend của các website tham khảo.

## Nguồn và ý tưởng

| Website | Quan sát từ trang chính | Áp dụng cho PLANORA |
| --- | --- | --- |
| [Linear](https://linear.app/) | Tách Get started và Open app; giới thiệu sản phẩm theo các công đoạn tiếp nhận, lập kế hoạch, AI và thực hiện. | Trình bày một luồng từ ghi chú đến làm việc, thay vì danh sách tính năng rời rạc. |
| [Notion](https://www.notion.com/) | Có Log in, Get Notion free, demo và product tours; nhóm công dụng theo Capture, Find, Automate. | Đăng nhập là lựa chọn riêng. Người mới được khám phá ví dụ trước khi tạo tài khoản. |
| [Todoist](https://www.todoist.com/) | Đặt lời hứa về sự rõ ràng gần CTA; minh họa ứng dụng và việc nhập bằng ngôn ngữ tự nhiên. | Hero nói về một ngày rõ ràng, bên dưới có ghi chú mẫu cụ thể. |
| [Reclaim](https://reclaim.ai/) | Giải thích thời gian tập trung, buffer và lịch; có demo và FAQ về cách lập lịch. | Hiển thị khoảng nghỉ, giờ rảnh và trả lời trường hợp lịch thiếu chỗ. |
| [Motion](https://www.usemotion.com/) | Giới thiệu các nhóm AI Tasks, Calendar, Docs bằng từng công dụng và hình minh họa. | Giải thích đúng việc AI làm trong PLANORA: đọc note và tạo việc; không thêm công dụng chưa có. |
| [Sunsama](https://www.sunsama.com/) | Nội dung xoay quanh kế hoạch ngày, khối lượng thực tế, bảo vệ tập trung và hướng dẫn lập kế hoạch. | Nhấn mạnh nhịp làm việc, tutorial và khả năng nghỉ giữa các phiên. |
| [Akiflow](https://akiflow.com/) | Trình bày AI lập kế hoạch, ưu tiên, lịch và khả năng người dùng kiểm soát. | Làm rõ lựa chọn xem trước hoặc tự lưu lịch, thay vì mô tả AI như một hộp đen. |
| [Asana](https://asana.com/) | Phân biệt Log in, Get started, Launch Asana và demo; có nhiều nhóm giải pháp/đối tượng. | Giữ điểm vào rõ ràng, chọn ít mục menu phù hợp với quy mô PLANORA. |

## Quyết định thiết kế

1. Tương tác chính là chạm vào đồng hồ trên khăn của linh vật. Đồng hồ có quầng sáng nhẹ; khi hover hoặc focus, linh vật dừng chuyển động nổi để thao tác dễ hơn. Sau khi chạm, ánh sáng mở thành một vòng elip từ vị trí đồng hồ, rồi chuyển sang login. Bàn phím dùng Tab và Enter/Space. Đăng nhập trực tiếp và đăng ký vẫn có sẵn.
2. Bố cục cuối cùng là một cảnh gọn trong màn hình: chữ PLANORA lớn phía sau và linh vật cú xanh ở trung tâm, theo bố cục ảnh tham khảo mà người dùng cung cấp. Màu và chất liệu vẫn theo login.
3. Thông tin nằm bên dưới hero để cuộn xem: giới thiệu PLANORA, demo Cách hoạt động, FAQ và lời mời tạo tài khoản. Menu cuộn đến đúng phần và đặt focus vào phần đó; không mở modal hay đổi hash của router.
4. Giữ nhận diện của PLANORA: màu xanh, hình elip/ribbon, ánh sáng, vật liệu kính và animation native từ login. Hai mắt của linh vật nhìn theo cursor bằng lớp SVG lấy chính texture mắt từ ảnh, thân/đầu không xoay theo cursor.
5. Không đưa logo khách hàng, đánh giá, số người dùng, mức tiết kiệm thời gian hoặc giá bán chưa được xác minh cho PLANORA vào trang.

## Luồng trang

- Khách chưa đăng nhập mở `/`: Getting Started.
- Bấm Đăng nhập: `#login`, dùng form login hiện có.
- Bấm Bắt đầu: `#register`, dùng form đăng ký hiện có.
- Chạm đồng hồ: hiệu ứng portal elip và View Transitions API nếu trình duyệt hỗ trợ, rồi `#login`. Không gọi AI hay lưu công việc; mỗi lượt chỉ chuyển trang một lần.
- Login có liên kết Giới thiệu PLANORA để trở về `#getting-started`.
- Link login/register mở trực tiếp và tải lại được; phiên đăng nhập còn hiệu lực tiếp tục vào ứng dụng như trước. Người đã đăng nhập vẫn có thể mở `#getting-started` và dùng nút Vào ứng dụng.

## Công nghệ và animation

React/Vite và CSS hiện có, không thêm thư viện animation. `LoginBackground` dùng chung chất liệu nền với login; artwork của login được giữ trong `LoginArtwork`. Web Animations API làm hiệu ứng xuất hiện và mở vòng elip; IntersectionObserver kích hoạt theo vùng nhìn thấy. Demo chỉ đổi trạng thái React tại trình duyệt.

Trang dùng linh vật cú đã tách nền. `mascot-eyes.jsx` ghép phần lòng trắng đã làm sạch dưới texture mống mắt thật, rồi cắt theo đường viền mắt cố định để không còn mống mắt cũ phía dưới tạo viền kép. Chỉ vùng mắt lấy từ ảnh base; thân và các chi tiết còn lại vẫn dùng ảnh cutout ban đầu. requestAnimationFrame dịch chuyển tối đa 8px ngang/6px dọc theo tọa độ gốc 1254px, giới hạn theo elip khi trỏ tới góc xa. Rời cảnh, tab ẩn, tạm dừng hoặc giảm chuyển động sẽ đưa mắt về giữa và dừng cập nhật. Trên điện thoại, mắt phản hồi điểm chạm.

Nút đồng hồ bám theo tọa độ ảnh (46.4% ngang, 55.57% dọc), có vùng chạm tối thiểu 44px. Overlay chuyển cảnh được portal ra body để không bị giới hạn bởi transform của linh vật. Nếu tắt animation hoặc dùng prefers-reduced-motion, chạm đồng hồ vào login ngay.

Có nút tạm dừng animation, nhớ lựa chọn trên thiết bị, dừng khi tab ẩn và tuân theo prefers-reduced-motion. Bấm/gõ kết thúc hiệu ứng xuất hiện để nút và nội dung dùng được ngay. Backdrop blur dùng khai báo prefix trước thuộc tính chuẩn để giữ hiệu ứng trên bản build.

## Tệp chính

- `frontend/src/getting-started.jsx`: trang, menu, demo, FAQ và CTA.
- `frontend/src/getting-started-motion.jsx`: điều khiển chuyển động.
- `frontend/src/start-entry.jsx`: tương tác trước login và chuyển cảnh; khi tạm dừng/giảm chuyển động, vẫn vào login mà không chạy hiệu ứng.
- `frontend/src/mascot-eyes.jsx`: hai lớp mắt nhìn theo cursor.
- `frontend/src/styles/getting-started.css`: bố cục desktop/mobile và vật liệu.
- `frontend/src/login-scene.jsx`: nền login dùng chung và artwork login.
- `frontend/src/controller.jsx`: lựa chọn trang public và workspace.

Kiểm tra dùng API giả và dữ liệu minh họa, không tạo tài khoản hay gọi AI thật. Bao gồm root trước login, link trực tiếp, chuyển trang/tải lại, cuộn phần thông tin, demo 4 bước, menu/FAQ, animation và màn hình 1920, 1440, 1280, 1024, 768, 390, 320px.

## Asset linh vật

Nguồn là ảnh linh vật người dùng đính kèm. Bản tham chiếu được lưu tại `src/SPWPOS.Api/wwwroot/assets/start/planora-owl-reference.png`; bản alpha dùng trên trang là `src/SPWPOS.Api/wwwroot/assets/start/planora-owl-v1.png`. Công cụ dùng: imagegen tích hợp, chế độ edit/background-extraction, transparent_background=true. Không thay đổi ảnh gốc.

Prompt cuối đã dùng:

> Use case: background-extraction. Asset type: transparent mascot cutout for the existing PLANORA website. Input image 1 is the EDIT TARGET. Remove only the pale blue studio background and floor, producing actual transparent alpha. Preserve exactly the same full-body cheerful fluffy blue-and-white baby owl, huge glossy blue eyes, peach beak and feet, blue and cyan flowing scarf/ribbon, clock brooch, calendar with cyan check mark, face expression, pose, proportions, lighting, soft fur, colors and 3D rendered materials. Keep every feather tip, scarf/ribbon extremity, the entire calendar and both feet intact. No new objects, no redesign, no additional text, no painted or checkerboard background. Keep the owl centered with a small transparent margin around its complete silhouette. Preserve the reference character identity rigorously.

Asset phụ dùng để ghép mắt: `src/SPWPOS.Api/wwwroot/assets/start/planora-owl-eye-base-v1.png`. Dùng imagegen tích hợp, chế độ edit/precise-object-edit với transparent_background=true. Nội dung prompt: xóa duy nhất hai mống mắt, đồng tử và điểm phản sáng, thay bằng lòng trắng ngà có cùng ánh sáng; giữ nguyên viền mí, lông, biểu cảm, thân, phụ kiện, alpha và bố cục 1254×1254. Asset chỉ được hiển thị trong hai vùng cắt của mắt, không thay ảnh toàn thân.
