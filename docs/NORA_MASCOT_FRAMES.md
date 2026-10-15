# NORA walking frames

Asset: `src/SPWPOS.Api/wwwroot/assets/nora/nora-walk-v1.png`.

Được tạo bằng công cụ imagegen tích hợp, không dùng CLI. Tham chiếu là mascot gốc `src/SPWPOS.Api/wwwroot/assets/start/planora-owl-v1.png`; file gốc được giữ nguyên.

PNG RGBA nền trong suốt, 1774×887px; 4 cột × 2 hàng, 8 frame theo thứ tự trái sang phải trên xuống dưới. CSS giữ tỷ lệ ô vuông và dùng `background-size: 400% 200%`, chuyển frame trong 0.88s. Sprite 46px đi trên thanh khi có yêu cầu đang streaming/xử lý; dừng ở trạng thái hoàn tất hoặc hủy, theo tùy chọn pause và reduced motion. Vite đóng gói asset cùng bản frontend.

Prompt chính xác đã dùng:

> Use case: identity-preserve. Asset type: production animation sprite sheet for NORA, a personal planning assistant web app. Reference image: exact identity/style reference for the existing NORA mascot. Create ONE genuinely transparent PNG sprite atlas of an eight-frame seamless WALK CYCLE: exactly FOUR equal-width columns and TWO equal-height rows, frame order left-to-right top row 1-4 then bottom row 5-8. Every cell shows the SAME full-body fluffy blue and white baby owl with large blue eyes, orange beak and orange feet, turquoise scarf with a small clock brooch, holding the same small blue calendar with a turquoise check mark. Preserve the plush photorealistic 3D rendering, face proportions, colors, fluffy materials and friendly expression of the reference. NORA faces toward screen right in a slight three-quarter view while walking in place. Animate actual alternating left/right foot contact, lift, pass and landing poses with a subtle body bob and scarf swing across eight distinct consecutive frames. Camera, character scale, center position, lighting and foot ground baseline MUST stay consistent within all eight cells. All cells have generous identical transparent margins and entire character visible; keep scarf and calendar wholly inside each cell. No background, no floor, no baked ground shadow, no text, no labels, no numbered frames, no grid lines, no watermark, no border, no alternate characters. The sheet must be usable immediately as a CSS sprite atlas with four columns and two rows.
