import { descriptions } from "./descriptions.mjs";

export const descriptionsVi = {
  AspNetUsers:
    "Tài khoản người dùng, danh tính đăng nhập và trạng thái bảo mật tài khoản.",
  AspNetRoles: "Các vai trò phân quyền được sử dụng trong ứng dụng.",
  AspNetUserRoles: "Liên kết thành viên giữa người dùng và vai trò phân quyền.",
  AspNetUserClaims:
    "Các thuộc tính xác thực được gán trực tiếp cho người dùng.",
  AspNetRoleClaims:
    "Các thuộc tính xác thực được cấp thông qua vai trò phân quyền.",
  AspNetUserLogins:
    "Nhà cung cấp đăng nhập bên ngoài được liên kết với tài khoản người dùng.",
  AspNetUserTokens:
    "Mã thông báo của nhà cung cấp danh tính được liên kết với người dùng.",
  EmailOtps:
    "Mã dùng một lần gửi qua email để xác minh tài khoản và thực hiện các bước bảo mật.",
  RefreshTokens:
    "Bản ghi mã làm mới dùng để gia hạn phiên đăng nhập đã xác thực.",
  PasswordResetTickets:
    "Phiếu xác nhận cho phép thực hiện quy trình đặt lại mật khẩu.",
  PasswordChangeNotifications:
    "Bản ghi thông báo về việc thay đổi mật khẩu tài khoản.",
  SecurityAuditLogs:
    "Nhật ký sự kiện bảo mật phục vụ kiểm tra tài khoản và hoạt động xác thực.",
  SecurityRateLimits:
    "Bộ đếm và khoảng thời gian dùng để giới hạn các yêu cầu bảo mật lặp lại.",
  account_status_logs:
    "Lịch sử quản trị viên thay đổi trạng thái tài khoản, kèm người thực hiện và lý do.",
  user_profiles: "Thông tin hồ sơ hiển thị của người dùng và tùy chọn múi giờ.",
  WorkTasks:
    "Công việc với hạn chót, thời lượng ước tính, độ ưu tiên, tiến độ và tùy chọn xếp lịch. Công việc có thể thuộc một danh mục và có công việc cha.",
  TaskCategories:
    "Danh mục công việc thuộc người dùng và màu sắc hiển thị của từng danh mục.",
  TaskDependencies:
    "Liên kết điều kiện tiên quyết giữa công việc và công việc cần hoàn thành trước.",
  task_types:
    "Định nghĩa và tên hiển thị của các loại công việc dùng để phân loại.",
  WeeklyAvailability:
    "Các khoảng thời gian rảnh lặp lại hằng tuần có thể dùng để xếp lịch công việc.",
  AvailabilityExceptions:
    "Điều chỉnh theo ngày cụ thể đối với lịch rảnh lặp lại.",
  FixedEvents:
    "Các sự kiện cố định trên lịch giới hạn thời gian có thể dành cho công việc.",
  WorkPreferences:
    "Giới hạn lập kế hoạch, khung giờ ưu tiên, độ dài phiên làm việc, thời gian nghỉ và múi giờ của người dùng.",
  PlanProposals:
    "Các đề xuất điều chỉnh kế hoạch, phạm vi thời gian, sự kiện kích hoạt và trạng thái áp dụng.",
  plan_items: "Từng thay đổi xếp lịch công việc thuộc một đề xuất kế hoạch.",
  plan_item_reasons:
    "Mã lý do và thông tin giải thích cho các mục kế hoạch được đề xuất.",
  plan_warnings:
    "Các cảnh báo phát sinh cho kế hoạch, có thể liên quan đến một công việc.",
  WorkSessions:
    "Các khối thời gian làm việc đã xếp lịch, trạng thái thực hiện và tổng thời gian ghi nhận.",
  session_executions:
    "Bản ghi thực hiện phiên làm việc, gồm thời gian đo được và thời gian đã điều chỉnh.",
  execution_segments:
    "Từng đoạn thời gian bắt đầu và kết thúc thuộc một lần thực hiện phiên làm việc.",
  RecoveryCases:
    "Các trường hợp gián đoạn hoặc thay đổi cần xử lý để khôi phục kế hoạch.",
  RecoverySettings:
    "Thiết lập của người dùng cho quy trình khôi phục kế hoạch.",
  estimation_observations:
    "Dữ liệu quan sát sau khi hoàn thành công việc để so sánh ước tính với công sức thực tế.",
  personal_capacity_profiles:
    "Hệ số sai lệch ước tính và năng lực khả dụng được học riêng cho từng người dùng.",
  task_type_biases:
    "Hệ số điều chỉnh ước tính và số mẫu quan sát theo từng người dùng, từng loại công việc.",
  capacity_snapshots:
    "Ảnh chụp theo tuần về thời gian khả dụng và lượng công việc đã hoàn thành theo kế hoạch.",
  risk_assessments:
    "Điểm số, mức độ rủi ro của công việc hoặc khối lượng công việc và trạng thái đánh giá hiện hành.",
  risk_reasons: "Mã lý do và thông tin giải thích cho một đánh giá rủi ro.",
  InAppNotifications:
    "Nội dung thông báo cho người dùng, các đối tượng công việc liên quan và trạng thái gửi, đã đọc.",
  notification_settings:
    "Tùy chọn thông báo và thời gian nhắc trước của người dùng.",
  system_job_runs:
    "Trạng thái, thời lượng và chi tiết lỗi của các lần chạy tác vụ hệ thống.",
  PlanningNotes:
    "Ghi chú của người dùng cung cấp ngữ cảnh cho việc lập kế hoạch và trợ lý.",
  AgentConversations:
    "Bản ghi hội thoại dùng để nhóm các tương tác với trợ lý lập kế hoạch.",
  AgentTurns: "Từng lượt tương tác với trợ lý thuộc một cuộc hội thoại.",
  AiUsageRecords:
    "Bản ghi mức sử dụng AI phục vụ theo dõi việc sử dụng trong ứng dụng.",
};

export const messagesVi = {
  "Accounts & security": "Tài khoản và bảo mật",
  "Availability & preferences": "Thời gian rảnh và tùy chọn",
  "Tasks & categories": "Công việc và danh mục",
  "Planning & execution": "Lập kế hoạch và thực thi",
  "Intelligence & workload": "Phân tích và khối lượng công việc",
  "Notifications & operations": "Thông báo và vận hành",
  "Notes & AI assistant": "Ghi chú và trợ lý AI",
  "Other tables": "Các bảng khác",
  "/ Schema explorer": "/ Khám phá cơ sở dữ liệu",
  "SQL Server · database catalog snapshot":
    "SQL Server · bản chụp cấu trúc cơ sở dữ liệu",
  tables: "bảng",
  columns: "cột",
  "foreign keys": "khóa ngoại",
  Help: "Hướng dẫn",
  Language: "Ngôn ngữ",
  "Explore the schema": "Khám phá cấu trúc dữ liệu",
  "Object explorer": "Danh sách bảng",
  "Table navigator": "Điều hướng bảng",
  "Find table or column…": "Tìm bảng hoặc cột…",
  "Find table or column": "Tìm bảng hoặc cột",
  Domain: "Nhóm chức năng",
  "All domains": "Tất cả nhóm chức năng",
  "Database diagram": "Sơ đồ cơ sở dữ liệu",
  "Relationship map": "Sơ đồ quan hệ",
  "All tables": "Tất cả bảng",
  "Zoom out": "Thu nhỏ",
  "Zoom in": "Phóng to",
  "Fit all": "Xem toàn bộ",
  "Locate selection": "Đến bảng đã chọn",
  Clear: "Bỏ chọn",
  "Compact map with every table and foreign key":
    "Sơ đồ gọn chứa đầy đủ bảng và khóa ngoại",
  "Export map ↗": "Xuất sơ đồ ↗",
  "Foreign key connections": "Các kết nối khóa ngoại",
  "Scrollable schema map. Shift and left mouse drag pans over tables and connections.":
    "Sơ đồ có thể cuộn. Giữ Shift và kéo chuột trái để di chuyển trên bảng và kết nối.",
  "Child → parent": "Bảng con → bảng cha",
  "Selected relationship": "Quan hệ được chọn",
  "Shift + left-drag to pan · Ctrl/⌘-click to add connections":
    "Shift + kéo chuột trái để di chuyển · Ctrl/⌘ + nhấp để chọn thêm kết nối",
  "Map overview": "Tổng quan sơ đồ",
  OVERVIEW: "TỔNG QUAN",
  "Fit every table in view": "Hiển thị toàn bộ bảng trong khung nhìn",
  "Click a table to locate it": "Nhấp vào bảng để di chuyển đến bảng đó",
  "Table information": "Thông tin bảng",
  "Table details": "Chi tiết bảng",
  "Read-only schema snapshot": "Bản chụp cấu trúc chỉ đọc",
  "Full columns SVG": "SVG đầy đủ cột",
  Metadata: "Siêu dữ liệu",
  Manifest: "Thông tin bản chụp",
  "Select a table": "Chọn một bảng",
  "Click a table on the map or in Object explorer to view its purpose, every column, keys, and incoming and outgoing connections.":
    "Nhấp vào bảng trên sơ đồ hoặc trong danh sách để xem chức năng, toàn bộ cột, các khóa và kết nối vào, ra.",
  "No matching table or column.": "Không tìm thấy bảng hoặc cột phù hợp.",
  "All tables remain on the map.": "Mọi bảng vẫn được giữ trên sơ đồ.",
  Relationships: "Quan hệ",
  Columns: "Cột",
  Keys: "Khóa",
  "Outgoing references": "Tham chiếu ra",
  "Incoming references": "Tham chiếu vào",
  "Self reference": "Tự tham chiếu",
  "Outgoing · references parent": "Tham chiếu ra · đến bảng cha",
  "Incoming · referenced by": "Tham chiếu vào · được tham chiếu bởi",
  "Trace connection": "Làm nổi bật kết nối",
  "Tracing connection": "Đang làm nổi bật kết nối",
  "No outgoing foreign keys.": "Không có khóa ngoại tham chiếu ra.",
  "No other table references this table.":
    "Không có bảng khác tham chiếu đến bảng này.",
  "Every captured column, in database ordinal order. Expand a row for definitions.":
    "Toàn bộ cột theo đúng thứ tự trong cơ sở dữ liệu. Mở rộng từng dòng để xem định nghĩa.",
  Ordinal: "Thứ tự",
  Identity: "Tự tăng",
  Computed: "Cột tính toán",
  Default: "Giá trị mặc định",
  Expression: "Biểu thức",
  Yes: "Có",
  No: "Không",
  "Filter:": "Bộ lọc:",
  "Captured primary keys and unique indexes.":
    "Các khóa chính và chỉ mục duy nhất trong bản chụp.",
  "Primary key": "Khóa chính",
  "Unique index": "Chỉ mục duy nhất",
  "No primary key or unique index declared.":
    "Không khai báo khóa chính hoặc chỉ mục duy nhất.",
  "Outgoing FKs": "Khóa ngoại ra",
  "Incoming FKs": "Khóa ngoại vào",
  "Table detail sections": "Các mục thông tin bảng",
  "Application description · structure from SQL catalog":
    "Mô tả chức năng ứng dụng · cấu trúc lấy từ danh mục SQL",
  "No application description has been documented for this table yet.":
    "Chưa có tài liệu mô tả chức năng của bảng này.",
  "Minimum child count is zero. Cardinality uses nullable FK columns and enabled, unfiltered unique indexes. Disabled or untrusted constraints may not hold for existing data.":
    "Số bản ghi con tối thiểu là 0. Bội số quan hệ dựa trên khả năng nhận NULL của cột khóa ngoại và chỉ mục duy nhất đang bật, không có bộ lọc. Ràng buộc bị tắt hoặc chưa được xác thực có thể không đúng với dữ liệu hiện có.",
  "Selection cleared. All table relationships shown.":
    "Đã bỏ chọn. Hiển thị toàn bộ quan hệ giữa các bảng.",
  "PK = primary key · FK = foreign key · ID = identity · ? = nullable. Arrows point from child (FK) to referenced parent. Only declared constraints are drawn; disabled or untrusted constraints are dashed. Descriptions are application notes; columns and keys come from the captured catalog. EF migration history and SSMS diagram storage are excluded.":
    "PK = khóa chính · FK = khóa ngoại · ID = tự tăng · ? = cho phép NULL. Mũi tên đi từ bảng con đến bảng cha được tham chiếu. Chỉ vẽ các ràng buộc đã khai báo; nét đứt biểu thị ràng buộc bị tắt hoặc chưa được xác thực. Mô tả là ghi chú chức năng ứng dụng; cột và khóa lấy từ danh mục SQL. Không bao gồm lịch sử migration của EF và bảng lưu sơ đồ SSMS.",
  "Every connection starts visible. Click any table to smoothly center it, inspect it, and highlight incoming and outgoing relationships. Ctrl/⌘-click connections to highlight several; unselected connections dim without disappearing. Search locates tables without removing them from the canvas. Scroll inside a table to read every column. Side connections follow their mapped column; offscreen columns connect at the footer. Drag empty canvas space, or hold Shift + the left mouse button to pan anywhere on the map, including over tables. Use scrollbars to reach all content or the minimap to move smoothly. Ctrl/⌘ + wheel zooms. Escape clears selection. Keyboard: Tab to a table or relationship, then Enter.":
    "Mọi kết nối đều hiển thị khi mở sơ đồ. Nhấp vào bảng để di chuyển mượt đến giữa khung nhìn, xem thông tin và làm nổi bật các quan hệ vào, ra. Giữ Ctrl/⌘ và nhấp để chọn nhiều kết nối; các kết nối khác được làm mờ nhưng vẫn hiển thị. Tìm kiếm không loại bảng khỏi sơ đồ. Cuộn bên trong bảng để xem toàn bộ cột. Điểm kết nối bám theo cột; cột ngoài vùng nhìn được nối tại chân bảng. Kéo vùng trống hoặc giữ Shift và kéo chuột trái để di chuyển ở bất kỳ vị trí nào, kể cả trên bảng. Dùng thanh cuộn để đến mọi nội dung hoặc sơ đồ thu nhỏ để di chuyển mượt. Ctrl/⌘ + con lăn để phóng to, thu nhỏ. Escape để bỏ chọn. Dùng Tab để đến bảng hoặc kết nối rồi nhấn Enter.",
};
for (const [name, text] of Object.entries(descriptions)) {
  if (descriptionsVi[name]) messagesVi[text] = descriptionsVi[name];
}

export const patternsVi = [
  ["^(\\d+) cols · (\\d+) FKs$", "$1 cột · $2 FK"],
  ["^(\\d+) tables · (\\d+) columns$", "$1 bảng · $2 cột"],
  ["^(\\d+) tables · (\\d+) foreign keys$", "$1 bảng · $2 khóa ngoại"],
  [
    "^All (\\d+) table objects stay on the map\\.$",
    "Toàn bộ $1 bảng vẫn có trên sơ đồ.",
  ],
  [
    "^All (\\d+) columns of (.+); scroll for more$",
    "Toàn bộ $1 cột của $2; cuộn để xem thêm",
  ],
  ["^Inspect (.+)$", "Xem thông tin $1"],
  ["^Locate (.+)$", "Di chuyển đến $1"],
  ["^(.+) · USER TABLE$", "$1 · BẢNG NGƯỜI DÙNG"],
  [
    "^Tracing (\\d+) foreign keys? · all connections retained$",
    "Đang làm nổi bật $1 khóa ngoại · giữ toàn bộ kết nối",
  ],
  [
    "^(\\d+) connected foreign keys · all tables retained$",
    "$1 khóa ngoại liên quan · giữ toàn bộ bảng",
  ],
  ["^(.+) · all tables retained$", "$1 · giữ toàn bộ bảng"],
  [
    "^(\\d+) tables and (\\d+) declared relationships are available on this map\\.$",
    "Sơ đồ có $1 bảng và $2 quan hệ đã khai báo.",
  ],
  [
    "^(\\d+) connections? highlighted · Ctrl/⌘-click to add or remove\\.$",
    "Đã làm nổi bật $1 kết nối · Ctrl/⌘ + nhấp để thêm hoặc bỏ chọn.",
  ],
  [
    "^(.+) selected\\. (\\d+) relationships highlighted\\.$",
    "Đã chọn $1. Làm nổi bật $2 quan hệ.",
  ],
  ["^(.+)\\. (.+) references (.+)\\.$", "$1. $2 tham chiếu đến $3."],
  ["^Child ([^:]+) : parent ([^·]+)", "Bảng con $1 : bảng cha $2"],
  ["^Primary key(.*)$", "Khóa chính$1"],
  ["^Unique index(.*)$", "Chỉ mục duy nhất$1"],
  [" · DISABLED$", " · ĐÃ TẮT"],
  [" · NOT TRUSTED$", " · CHƯA XÁC THỰC"],
  [
    "; child ([^,;]+), parent ([^;]+); delete ([^;]+); enabled / trusted$",
    "; bảng con $1, bảng cha $2; khi xóa $3; đang bật / đã xác thực",
  ],
  [
    "; child ([^,;]+), parent ([^;]+); delete ([^;]+); (DISABLED|NOT TRUSTED)$",
    "; bảng con $1, bảng cha $2; khi xóa $3; ràng buộc bị tắt hoặc chưa xác thực",
  ],
  ["^Captured (.+)$", "Chụp lúc $1"],
  ["^Filter: (.+)$", "Bộ lọc: $1"],
];

export function translate(message, language = "en") {
  if (language !== "vi") return message;
  if (messagesVi[message]) return messagesVi[message];
  let value = message;
  for (const [source, replacement] of patternsVi)
    value = value.replace(new RegExp(source), replacement);
  return value;
}
