# Database documentation / Tài liệu cơ sở dữ liệu

| Resource / Tài nguyên | Purpose / Mục đích |
| --- | --- |
| [Current explorer](current/index.html) | Actual SQL Server catalog snapshot; interactive English/Vietnamese explorer. / Bản chụp danh mục SQL Server, hỗ trợ tương tác và hai ngôn ngữ. |
| [PNG image gallery](current/images/README.md) | Grouped map, all-column export, and function details. / Sơ đồ nhóm, đầy đủ cột và chi tiết chức năng. |
| [Snapshot notes](current/README.md) | Capture timestamp, coverage, SVGs, and metadata. / Thời điểm chụp, phạm vi, SVG và dữ liệu danh mục. |
| [Generator guide](../../scripts/database-diagram/README.md) | Reproducible SQL capture, offline rendering, and image export commands. / Lệnh chụp cấu trúc, tạo sơ đồ và xuất ảnh. |
| [Migration SQL](schema.sql) | Previously generated migration script; not the live catalog snapshot. / Script migration đã tạo trước đó, không phải bản chụp cấu trúc hiện tại. |
| [UML alignment](UML_ALIGNMENT.md) | Historical mapping between implementation and UML v2. / Đối chiếu lịch sử giữa triển khai và UML v2. |
| [Original UML v2](../reference/UML-v2/) | Preserved reference bundle, including its original ER images and checksum manifest. / Bộ tài liệu tham khảo gốc, gồm ảnh ER và danh sách checksum. |

Use the current export for database structure. Regenerate after migrations; the explorer and images do not update automatically. Exporting reads catalog metadata and does not apply migration scripts or modify application records.

Dùng bản xuất hiện tại để xem cấu trúc database. Tạo lại sau khi áp dụng migration; sơ đồ và ảnh không tự cập nhật liên tục. Quá trình xuất chỉ đọc metadata, không chạy migration hoặc sửa dữ liệu ứng dụng.

[All documentation / Toàn bộ tài liệu](../README.md)
