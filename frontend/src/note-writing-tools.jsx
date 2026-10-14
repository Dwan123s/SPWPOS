import { icon } from "./ui.jsx";
import {
  defaultNoteAppearance,
  noteFonts,
  noteInkColors,
  notePaperColors,
  noteSizes,
} from "./note-appearance.js";

export function NoteWritingTools({
  busy,
  hidden,
  imported,
  typeMenu,
  appearance,
  setAppearance,
  insertList,
}) {
  const change = (key, value) => setAppearance({ ...appearance, [key]: value });
  return (
    <fieldset
      className="note-writing-tools"
      disabled={!!busy}
      hidden={hidden}
      aria-label="Công cụ ghi chú"
    >
      <details
        className="note-type-menu"
        ref={typeMenu}
        onKeyDown={(event) => {
          if (event.key === "Escape") {
            event.currentTarget.open = false;
            event.currentTarget.querySelector("summary")?.focus();
          }
        }}
        onBlur={(event) => {
          if (!event.currentTarget.contains(event.relatedTarget))
            event.currentTarget.open = false;
        }}
      >
        <summary aria-label="Kiểu chữ và màu giấy" title="Kiểu chữ và màu giấy">
          Aa <i style={{ background: appearance.ink }} aria-hidden="true" />
        </summary>
        <div className="note-appearance-panel">
          <div className="note-appearance-heading">
            <strong>Phong cách ghi chú</strong>
            <span>Chọn cách viết của bạn</span>
          </div>
          <div className="note-appearance-type">
            <label>
              Font chữ
              <select
                value={appearance.font}
                onChange={(event) => change("font", event.target.value)}
              >
                {noteFonts.map((font) => (
                  <option key={font.value} value={font.value}>
                    {font.label}
                  </option>
                ))}
              </select>
            </label>
            <label>
              Cỡ chữ
              <select
                value={appearance.size}
                onChange={(event) => change("size", Number(event.target.value))}
              >
                {noteSizes.map((size) => (
                  <option key={size} value={size}>
                    {size} px
                  </option>
                ))}
              </select>
            </label>
          </div>
          <NotePalette
            label="Màu chữ"
            colors={noteInkColors}
            value={appearance.ink}
            onChange={(value) => change("ink", value)}
          />
          <NotePalette
            label="Màu giấy"
            colors={notePaperColors}
            value={appearance.paper}
            onChange={(value) => change("paper", value)}
          />
          <div className="note-appearance-footer">
            <p>Lưu riêng cho ghi chú trên trình duyệt này.</p>
            <button
              type="button"
              onClick={() => setAppearance({ ...defaultNoteAppearance })}
            >
              Khôi phục mặc định
            </button>
          </div>
        </div>
      </details>
      <span className="note-tool-divider" aria-hidden="true" />
      <button
        type="button"
        className="note-tool"
        hidden={imported}
        title="Thêm dòng danh sách"
        aria-label="Thêm dòng danh sách"
        onClick={() => insertList("• ")}
      >
        {icon("tasks")}
      </button>
      <button
        type="button"
        className="note-tool"
        hidden={imported}
        title="Thêm dòng checklist"
        aria-label="Thêm dòng checklist"
        onClick={() => insertList("☐ ")}
      >
        {icon("check")}
      </button>
      <button
        type="button"
        className="note-tool"
        title="Giấy kẻ dòng"
        aria-label="Giấy kẻ dòng"
        aria-pressed={appearance.ruled}
        onClick={() => change("ruled", !appearance.ruled)}
      >
        {icon("menu")}
      </button>
    </fieldset>
  );
}

function NotePalette({ label, colors, value, onChange }) {
  return (
    <fieldset className="note-palette">
      <legend>{label}</legend>
      <div className="note-palette-swatches">
        {colors.map(([color, name]) => (
          <label
            key={color}
            title={name}
            className="note-color-swatch"
            style={{ "--swatch": color }}
          >
            <input
              type="radio"
              name={label}
              aria-label={`${label}: ${name}`}
              checked={value === color}
              onChange={() => onChange(color)}
            />
            <span aria-hidden="true">{value === color && icon("check")}</span>
          </label>
        ))}
        <label
          className="note-custom-color"
          title={`Tùy chọn ${label.toLowerCase()}`}
        >
          <input
            type="color"
            aria-label={`Tùy chọn ${label.toLowerCase()}`}
            value={value}
            onChange={(event) => onChange(event.target.value)}
          />
          <span aria-hidden="true">+</span>
        </label>
      </div>
    </fieldset>
  );
}
