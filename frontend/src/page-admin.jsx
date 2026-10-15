import { Select, Slot, nodeList } from "./react-ui.jsx";
import { Loading } from "./loading.jsx";
import {
  state,
  button,
  badge,
  icon,
  riskLabels,
  reasons,
  typeLabels,
  dateFmt,
  timeFmt,
  dayKey,
  minutes,
  stat,
  field,
  empty,
  modal,
  loadLabel,
} from "./ui.jsx";
export function adminPage() {
  return (
    <>
      <section className="card section-gap">
        <div className="card-header">
          <div>
            <h2>Trạng thái hệ thống</h2>
            <p>Database, hoạt động và tác vụ thông báo.</p>
          </div>
          {button(
            "Cập nhật",
            "admin-refresh",
            "small secondary",
            "",
            "refresh",
          )}
        </div>
        <Slot id="system-status" className="admin-grid" as="div">
          <Loading label="Đang tải trạng thái hệ thống…" />
        </Slot>
      </section>
      <section className="card">
        <div className="card-header">
          <h2>Quản lý người dùng</h2>
        </div>
        <form data-form="admin-search" className="filters">
          <input
            className="filter search"
            name="search"
            placeholder="Tìm theo email hoặc tên"
            maxLength="100"
            aria-label="Tìm người dùng"
          />
          <button type="submit" className="btn secondary">
            Tìm kiếm
          </button>
        </form>
        <Slot className="table-scroll" id="admin-users" as="div">
          <Loading label="Đang tải danh sách người dùng…" />
        </Slot>
      </section>
      <section className="card spaced">
        <h2>Lịch sử xử lý hệ thống</h2>
        <Slot className="table-scroll" id="admin-jobs" as="div">
          <Loading label="Đang tải lịch sử xử lý…" />
        </Slot>
      </section>
    </>
  );
}
