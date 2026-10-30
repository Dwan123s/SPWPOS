# Build frontend/backend độc lập, giữ chung một địa chỉ

Quyết định 28/10/2026: trình duyệt truy cập cùng origin cho giao diện và `/api`. Không cần thay đổi CORS, cookie SameSite hoặc CSRF. Cả hai phần có thể chạy trên hạ tầng riêng phía sau reverse proxy.

## Build

Từ repository root, backend không yêu cầu Node và không gói React vào publish mặc định:

```powershell
dotnet restore SPWPOS.sln --locked-mode
dotnet publish src/SPWPOS.Api -c Release -o artifacts/api
```

Frontend từ thư mục `frontend`, dùng Node theo `package.json`:

```powershell
npm ci
$env:PLANORA_STANDALONE = "true"
npm run build
```

Output là `frontend/dist`, base URL `/`, bao gồm assets mascot và brand. GitHub Actions build/upload `frontend-dist` độc lập với backend. CI backend vẫn chạy migration/concurrency trên SQL Server tạm.

Luồng phát triển cũ vẫn dùng `scripts/Build-Frontend.ps1` hoặc build frontend không đặt `PLANORA_STANDALONE`; frontend nằm trong API wwwroot để chạy chung. Nếu muốn publish gói cũ, chỉ định `-p:BundleLegacyFrontend=true` (và có Node/npm). Mặc định không gói output đó vào backend.

## Reverse proxy

Một origin HTTPS, ví dụ `https://planora.example`, phục vụ:

| Đường dẫn | Đích |
| --- | --- |
| `/api/*` | ASP.NET Core backend |
| `/assets/*`, `/brand/*`, `/index.html` và phần còn lại | frontend dist |

Reverse proxy phải giữ nguyên Host và truyền `X-Forwarded-Proto`/`X-Forwarded-For` từ đầu vào đáng tin cậy. Khai báo IP của proxy trong `Security:TrustedProxyAddresses`; không tin mọi proxy tùy ý. Backend vẫn yêu cầu HTTPS và SMTP TLS ở production. Không trả SPA HTML cho lỗi `/api/*`.

Đặt cache dài cho Vite assets có hash; `index.html` cần revalidate. Không cache response API riêng tư. Giữ CSP tương đương chính sách hiện tại của API khi static frontend được phục vụ bởi proxy; chỉ bổ sung Google/Firebase origins nếu thực sự bật tính năng đó.

Backend publish có thể chạy trên ASP.NET Core 8 App Service hoặc container dùng ASP.NET Core 8. Cấp connection string/AI/SMTP/JWT bằng secret manager; lưu bền Data Protection keys khi có nhiều replica. Áp dụng migration có kiểm soát trước khi chuyển traffic. Tài liệu này không thực hiện deploy hoặc đổi domain thật.

## Kiểm tra trước phát hành

Đăng nhập, refresh cookie, đăng xuất, CSRF rejection, upload/paste ảnh NORA, tải `.ics`, assets mascot/font và mở lại URL sau refresh. Kiểm tra `artifacts/api` không có `wwwroot/app` hay `wwwroot/index.html` khi publish mặc định. Chạy `/api/admin/health` với Admin, kiểm tra người dùng thường nhận 403. Không coi một build thành công là đã xác minh hạ tầng production.
