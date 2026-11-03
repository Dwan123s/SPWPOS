# XP pet và thanh toán skin qua payOS

PLANORA dùng NORA làm pet mặc định. Hoàn thành task đúng deadline nhận **25 XP một lần/task**, kể cả khi nhập trạng thái thủ công hoặc hoàn thành từ checklist. Mở lại rồi hoàn thành cùng task không nhận thưởng lần nữa. Task nhóm nhận thưởng sau khi trưởng nhóm duyệt, xét thời điểm nộp hoàn thành. Các XP thưởng phiên đúng hạn đã có vẫn được giữ. XP không phải tiền và không dùng mua skin MILO.

Mỗi 100 XP tăng một cấp: cấp 5 ở 400 XP có đôi cánh và hiệu ứng mới; cấp 10 ở 900 XP thêm vương miện. Ngoại hình áp dụng cho NORA và skin cáo MILO. Mục **Cài đặt → Pet & XP** hiển thị các mốc, XP, kho skin và giao dịch. Header chung có pet, cấp độ và tiến độ XP. Trong chat nhóm, pet được trang bị của từng người nằm phía trên bong bóng tin nhắn, kèm cấp độ và hình dáng tiến hóa. API chỉ bổ sung skin đang dùng và cấp độ vào danh tính tác giả; MILO chỉ hiển thị khi tài khoản đã sở hữu skin. Metadata tác giả của 50 tin gần nhất được cập nhật khi polling, kể cả khi không có tin mới.

## Tạo kênh payOS

1. Mở [my.payos.vn](https://my.payos.vn), tạo tài khoản, hoàn tất các bước xác thực cá nhân hoặc doanh nghiệp theo yêu cầu của payOS.
2. Liên kết tài khoản ngân hàng nhận tiền và tạo kênh thanh toán. Xem [hướng dẫn chính thức](https://payos.vn/docs/).
3. Lấy **Client ID**, **API Key**, **Checksum Key** của kênh. Không gửi khóa vào chat hoặc đưa vào Git, JavaScript, ảnh demo hay file cấu hình công khai.
4. Chuẩn bị một origin HTTPS công khai cho ứng dụng, ví dụ `https://planora.example/`. Đây chỉ là ví dụ; thay bằng địa chỉ của bạn. `https://localhost:7043` chưa thể nhận webhook từ payOS.

Nếu đang phát triển local, dùng tunnel HTTPS có địa chỉ công khai hoặc triển khai app lên máy chủ HTTPS. Cấu hình tunnel/reverse proxy chuyển yêu cầu đến API local. Nếu proxy chuyển tiếp HTTP, khai báo đúng IP proxy tại `Security:TrustedProxyAddresses` và giữ kiểm tra HTTPS; không tắt xác minh chứng chỉ hay kiểm tra nguồn yêu cầu. Địa chỉ tunnel đổi thì cập nhật cả PublicBaseUrl và webhook. Không tự công khai SQL Server, mail hay công cụ phát triển.

## Nhập cấu hình trên máy

### Khi chạy local

Không cần mua tên miền để thử webhook. Cài `cloudflared` từ [trang tải chính thức](https://developers.cloudflare.com/tunnel/downloads/), mở lại PowerShell và giữ PLANORA đang chạy ở `https://localhost:7043`. Trong thư mục dự án, chạy:

```powershell
powershell.exe -NoProfile -ExecutionPolicy Bypass -File .\scripts\Start-PayOsTunnel.ps1
```

Script kiểm tra chứng chỉ HTTPS bằng cơ chế xác minh thông thường, xuất **chứng chỉ công khai** vào `artifacts/payos-tunnel` và mở một Quick Tunnel. Không xuất private key, không tắt kiểm tra TLS. Cloudflare in địa chỉ dạng `https://...trycloudflare.com`; giữ cửa sổ này mở và dùng chính địa chỉ đó làm PublicBaseUrl ở bước tiếp theo. Script chỉ mở tunnel khi bạn chạy, không tự cài công cụ hay đăng ký tài khoản.

Script mặc định dùng HTTP/2 qua IPv4 để tránh lỗi QUIC/UDP timeout trên một số mạng. Nếu cửa sổ tunnel cũ báo `failed to accept QUIC stream` hoặc `no recent network activity`, nhấn Ctrl+C rồi chạy lại script. Kết nối thành công thường có dòng `Registered tunnel connection` cùng `protocol=http2`. Nếu HTTP/2 cũng timeout, kiểm tra mạng có cho phép Cloudflare Tunnel kết nối ra ngoài qua TCP 7844 hoặc thử mạng khác; không cần tắt firewall hay bỏ xác minh TLS. Xem [tham số giao thức](https://developers.cloudflare.com/tunnel/reference/run-parameters/).

Theo [tài liệu Quick Tunnels](https://developers.cloudflare.com/tunnel/get-started/quick-tunnels/), URL này tạm thời, thay đổi mỗi lần mở lại tunnel và không hỗ trợ SSE. Tiếp tục dùng giao diện NORA tại localhost; dùng tunnel để thử thanh toán/webhook. Khi URL đổi, cập nhật PublicBaseUrl và webhook của kênh payOS. Người có URL có thể truy cập trang đăng nhập PLANORA trong khi tunnel chạy; đăng nhập và kiểm tra quyền của ứng dụng vẫn được giữ.

Trong PowerShell ở thư mục dự án:

```powershell
powershell.exe -NoProfile -ExecutionPolicy Bypass -File .\scripts\Configure-PayOs.ps1 -PublicBaseUrl 'https://TEN-MIEN-CUA-BAN/'
```

Script hỏi lần lượt Client ID, API Key và Checksum Key, ẩn ký tự nhập và lưu bằng User Secrets qua stdin. Script bổ sung hostname vào AllowedHosts. Không tạo giao dịch và không đăng ký webhook thay người dùng. Sau đó khởi động lại API bằng quy trình development thông thường:

```powershell
powershell.exe -NoProfile -ExecutionPolicy Bypass -File .\scripts\Start-Development.ps1
```

Ở môi trường triển khai, đặt các biến môi trường tương ứng, không dùng User Secrets development:

| Biến | Giá trị |
|---|---|
| `PayOs__Enabled` | `true` |
| `PayOs__ClientId` | Client ID của kênh |
| `PayOs__ApiKey` | API Key của kênh |
| `PayOs__ChecksumKey` | Checksum Key của kênh |
| `PayOs__PublicBaseUrl` | Origin HTTPS công khai có dấu `/` cuối |
| `AllowedHosts` | Hostname triển khai và các hostname hợp lệ cần giữ |

## Đăng ký webhook

Trong cấu hình kênh payOS, đăng ký:

```text
https://TEN-MIEN-CUA-BAN/api/commerce/payos/webhook
```

App phải chạy trên địa chỉ này trước khi đăng ký; payOS gửi một mẫu đã ký để kiểm tra. Endpoint công khai không yêu cầu đăng nhập nhưng bắt buộc xác minh chữ ký. Mẫu không khớp đơn hàng thật được xác nhận mà không cấp skin. Tham khảo [API và webhook](https://payos.vn/docs/api/) và [cách kiểm tra chữ ký](https://payos.vn/docs/tich-hop-webhook/kiem-tra-du-lieu-voi-signature/).

## Mua và nhận skin

1. Đăng nhập, vào **Cài đặt → Pet & XP**. Khi cấu hình đủ, nút **Mua skin · 10.000đ** khả dụng.
2. Bấm mua để mở **popup thanh toán ngay trong PLANORA**, với skin MILO, giá **10.000đ** và mã QR qua payOS. Giá do backend ấn định là **10.000 VND**, không lấy từ giá gửi lên bởi trình duyệt. Không mua XP hoặc tự tăng cấp.
3. Quét QR trong popup bằng ứng dụng ngân hàng. Popup nhúng [SDK chính thức payOS](https://payos.vn/docs/sdks/front-end/script-js/), tải trực tiếp từ CDN payOS; CSP chỉ cho phép script và frame của các địa chỉ payOS đã chỉ định khi kênh thanh toán được cấu hình. Có thể đóng popup, rồi bấm **Tiếp tục thanh toán** để mở lại cùng đơn, không tạo đơn mới. Sau khi nhận webhook hợp lệ, app đánh dấu đơn PAID và cấp quyền sở hữu MILO trong cùng giao dịch database. Sự kiện thành công từ iframe chỉ yêu cầu kiểm tra backend, không tự cấp skin.
4. Quay lại PLANORA và bấm **Kiểm tra thanh toán** nếu cần. Trang tự đối soát với payOS mỗi 7 giây khi đơn đang chờ và trang đang hiển thị. Nếu webhook bị mất do tunnel gián đoạn, backend gọi merchant API bằng khóa của kênh, kiểm tra chữ ký HMAC của phản hồi và toàn bộ giao dịch trước khi cấp MILO. Đơn đã hết hạn local vẫn có thể kiểm tra để khôi phục giao dịch đã thanh toán. Chọn **Sử dụng** để đổi sang MILO; có thể đổi về NORA.

Đơn có mã yêu cầu để thử lại, link có thời hạn 15 phút, trạng thái được khôi phục sau tải lại trang. Mất phản hồi khi tạo link thì lần thử lại tra cứu cùng orderCode, không tự tạo giao dịch mới. Giao dịch chỉ mở khóa khi chữ ký, mã đơn, amount, currency và paymentLinkId khớp. `accountNumber` trả về khi tạo mã QR có thể là tài khoản ảo, khác tài khoản thực trong giao dịch ([FAQ payOS](https://payos.vn/docs/cau-hoi-thuong-gap/)); khi webhook có tài khoản khác, hệ thống bắt buộc tra cứu trạng thái PAID có chữ ký từ merchant API và đối chiếu link/mã đơn/số tiền trước khi cấp, thay vì so sánh hai loại tài khoản như cùng một giá trị. URL return/cancel, nội dung chat hoặc thao tác kiểm tra phía client không cấp quyền. Gửi webhook lặp lại không cấp skin lần hai. Chủ sở hữu không cần mua lại.

Nếu chuyển khoản thiếu tiền hoặc bằng chứng từ payOS không khớp, app chưa cấp skin. Đối soát chỉ cấp khi trạng thái PAID, đủ tiền, không còn dư nợ, tổng các giao dịch khớp số tiền đã nhận, mã đơn/link đúng kênh và chữ ký đúng. Số tài khoản thực của giao dịch không bị buộc bằng alias tài khoản ảo của QR. Webhook và đối soát dùng cùng transaction/khóa tài khoản, nên kiểm tra lặp lại hoặc webhook đến sau không cấp skin lần hai. Không có luồng hoàn tiền tự động ở phiên bản này.

## Triển khai database và kiểm thử

Popup lấy `returnUrl` từ backend, dùng cùng URL mà gateway đã gửi khi tạo đơn (`PublicBaseUrl/?payment=returned`). Không thay bằng URL localhost hiện tại: payOS từ chối đường dẫn nhúng không khớp URL của đơn. Giá trị được encode trước khi truyền SDK để giữ nguyên query/hash. Khi dùng localhost với tunnel, trạng thái đơn vẫn tự kiểm tra qua merchant API mỗi 7 giây; webhook hợp lệ hoặc phản hồi tra cứu có chữ ký hợp lệ mới mở khóa skin, kể cả khi trình duyệt không nhận callback khác origin. Khi đổi tunnel, cập nhật cấu hình và webhook; kiểm tra giao dịch cũ trước khi tạo đơn mới, không yêu cầu người đã thanh toán trả lại lần nữa.

Chủ hệ thống có thể chạy API local ở chế độ bảo trì `--reconcile-skin-payment <purchase-id>` từ thư mục `src/SPWPOS.Api`, với cấu hình development hiện có. Chế độ này dùng đúng service đối soát, xác minh payOS và ghi đơn/quyền sở hữu nguyên tử; không tự đặt PAID hoặc cấp skin nếu chưa có bằng chứng hợp lệ. Chỉ in mã đơn, trạng thái và quyền sở hữu, không in khóa hay thông tin ngân hàng. Không mở HTTP endpoint quản trị mới.

Migration `20261101195743_PetExperienceAndSkinPurchases` thêm cờ thưởng task, đơn hàng và quyền sở hữu skin. Task đã hoàn thành trước nâng cấp được đánh dấu đã xét thưởng để không được cộng mới khi mở lại. XP đã có được giữ nguyên. Tủ đồ chỉ còn NORA mặc định và MILO trả phí; tài khoản dùng các skin màu cũ hiển thị NORA mặc định. Dùng script development để cập nhật migration hoặc quy trình triển khai migration của môi trường.

Kiểm thử tự động dùng provider giả và chữ ký tổng hợp, không chuyển tiền. Chỉ thực hiện giao dịch thật sau khi người dùng tự cấu hình kênh, origin công khai và đăng ký webhook. Khi chưa có cấu hình, XP và pet vẫn dùng được; cửa hàng báo chưa kết nối payOS.
