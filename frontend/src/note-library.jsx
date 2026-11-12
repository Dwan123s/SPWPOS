import { useState } from "react";
import { dateFmt, icon } from "./ui.jsx";

export function noteState(note) {
  if (note.planId && note.analysis?.activities)
    return { key: "created", label: "Đã lưu lịch" };
  if (note.createdTaskIds?.length)
    return { key: "created", label: "Đã tạo việc" };
  if (note.analysis) return { key: "reviewed", label: "Đã kiểm tra" };
  return { key: "draft", label: "Bản nháp" };
}
const fold = (value) =>
  String(value || "")
    .toLocaleLowerCase("vi")
    .normalize("NFD")
    .replace(/\p{M}/gu, "")
    .replace(/đ/g, "d");

export function NoteLibrary({
  notes,
  currentId,
  busy,
  open,
  setOpen,
  select,
  onDelete,
  hidden,
}) {
  const [search, setSearch] = useState("");
  const [filter, setFilter] = useState("all");
  const matches = notes.filter(
    (item) =>
      (filter === "all" || noteState(item).key === filter) &&
      fold(item.title + " " + item.content).includes(fold(search.trim())),
  );
  const filtered = filter !== "all" || !!search.trim();
  const create = () => {
    setSearch("");
    setFilter("all");
    select({ title: "", content: "" });
  };
  return (
    <section
      className="card notes-library"
      aria-labelledby="note-library-title"
      hidden={hidden}
    >
      <div className="note-library-heading">
        <button
          type="button"
          className="note-library-toggle"
          aria-expanded={open}
          aria-controls="saved-notes"
          onClick={() => setOpen(!open)}
        >
          <span id="note-library-title">Ghi chú của bạn</span>
          <span className="note-count">{notes.length}</span>
          <span className="note-library-chevron" aria-hidden="true">
            {icon("back")}
          </span>
        </button>
        <button
          type="button"
          className="btn primary note-create"
          aria-label="Ghi chú mới"
          disabled={!!busy}
          onClick={create}
        >
          {icon("plus")}
          <span>Ghi chú mới</span>
        </button>
      </div>
      <div id="saved-notes" hidden={!open}>
        <label className="note-search">
          {icon("search")}
          <input
            type="search"
            aria-label="Tìm ghi chú"
            placeholder="Tìm ghi chú…"
            value={search}
            onChange={(event) => setSearch(event.target.value)}
          />
        </label>
        <div className="note-library-meta">
          <span>{notes.length} ghi chú</span>
          <select
            aria-label="Lọc ghi chú"
            value={filter}
            onChange={(event) => setFilter(event.target.value)}
          >
            <option value="all">Tất cả trạng thái</option>
            <option value="draft">Bản nháp</option>
            <option value="reviewed">Đã kiểm tra</option>
            <option value="created">Đã tạo việc</option>
          </select>
        </div>
        <div className="notes-list">
          {matches.map((item) => {
            const status = noteState(item);
            return (
              <div className="note-list-row" key={item.id}>
                <button
                  type="button"
                  className={
                    "note-list-item " + (currentId === item.id ? "active" : "")
                  }
                  disabled={!!busy}
                  aria-label={item.title}
                  aria-describedby={"note-meta-" + item.id}
                  aria-pressed={currentId === item.id}
                  onClick={() => {
                    select(item);
                    if (window.matchMedia("(max-width: 800px)").matches)
                      setOpen(false);
                  }}
                >
                  <span
                    className="note-list-topline"
                    id={"note-meta-" + item.id}
                  >
                    <span className={"note-list-state state-" + status.key}>
                      <i aria-hidden="true" />
                      {status.label}
                    </span>
                    <time dateTime={item.updatedAt}>
                      {dateFmt(item.updatedAt)}
                    </time>
                  </span>
                  <strong>{item.title}</strong>
                  <span className="note-list-excerpt">
                    {item.content
                      .split("\n")
                      .find((line) => line.trim())
                      ?.slice(0, 120)}
                  </span>
                </button>
                <NoteDeleteButton item={item} busy={busy} onDelete={onDelete} />
              </div>
            );
          })}
          {!notes.length && !busy && (
            <div className="note-library-empty">
              {icon("edit")}
              <strong>Bắt đầu một ghi chú</strong>
              <p>Viết ý tưởng và việc cần làm trong vùng soạn thảo.</p>
            </div>
          )}
          {!!notes.length && !matches.length && (
            <div className="note-library-no-results">
              <p>Không tìm thấy ghi chú.</p>
              {filtered && (
                <button
                  type="button"
                  onClick={() => {
                    setSearch("");
                    setFilter("all");
                  }}
                >
                  Bỏ bộ lọc
                </button>
              )}
            </div>
          )}
        </div>
      </div>
    </section>
  );
}
// Keep selection and deletion as separate buttons for keyboard and touch users.
function NoteDeleteButton({ item, busy, onDelete }) {
  return (
    <button
      type="button"
      className="note-list-delete"
      aria-label={"Xóa ghi chú " + item.title}
      title={"Xóa ghi chú " + item.title}
      disabled={!!busy}
      onClick={() => onDelete(item)}
    >
      {icon("trash")}
    </button>
  );
}
