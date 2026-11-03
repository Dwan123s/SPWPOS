import { Slot } from "./react-ui.jsx";
import { Loading } from "./loading.jsx";
import { button } from "./ui.jsx";

export function adminPage() {
  return (
    <section className="card" aria-label="Quản lý người dùng">
      <div className="card-header">
        <div>
          <h2>Danh sách người dùng</h2>
          <p>Tìm tài khoản, xem lịch sử xử lý và quản lý quyền truy cập.</p>
        </div>
        {button("Cập nhật", "admin-refresh", "small secondary", "", "refresh")}
      </div>
      <form data-form="admin-search" className="filters">
        <input
          className="filter search"
          name="search"
          placeholder="Tìm theo email, tên hoặc tên đăng nhập"
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
  );
}
export function adminSystemPage() {
  return (
    <>
      <section className="card section-gap">
        <div className="card-header">
          <div>
            <h2>Tình trạng vận hành</h2>
            <p>
              Số liệu vận hành tổng hợp. Không truy cập nội dung công việc, ghi
              chú hoặc lịch cá nhân.
            </p>
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
      <section className="card spaced">
        <h2>Lịch sử xử lý hệ thống</h2>
        <Slot className="table-scroll" id="admin-jobs" as="div">
          <Loading label="Đang tải lịch sử xử lý…" />
        </Slot>
      </section>
    </>
  );
}
