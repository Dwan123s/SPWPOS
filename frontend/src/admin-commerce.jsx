import { useEffect, useId, useRef, useState } from "react";
import { api } from "./api.js";
import { icon } from "./ui.jsx";
import "./admin-commerce.css";

const money = (n) =>
  new Intl.NumberFormat("vi-VN", {
    style: "currency",
    currency: "VND",
    maximumFractionDigits: 0,
  }).format(n ?? 0);
const date = (d) =>
  new Intl.DateTimeFormat("vi-VN", {
    timeZone: "Asia/Ho_Chi_Minh",
    day: "2-digit",
    month: "2-digit",
    year: "numeric",
  }).format(new Date(`${d}T00:00:00+07:00`));
const timestamp = (d) =>
  d
    ? new Intl.DateTimeFormat("vi-VN", {
        timeZone: "Asia/Ho_Chi_Minh",
        dateStyle: "short",
        timeStyle: "short",
      }).format(new Date(d))
    : "—";
const statuses = {
  PAID: "Đã thanh toán",
  PENDING: "Chờ thanh toán",
  CREATING: "Đang tạo đơn",
  EXPIRED: "Hết hạn",
  CANCELLED: "Đã hủy",
};
function todayKey() {
  const parts = new Intl.DateTimeFormat("en-CA", {
    timeZone: "Asia/Ho_Chi_Minh",
    year: "numeric",
    month: "2-digit",
    day: "2-digit",
  }).formatToParts(new Date());
  return ["year", "month", "day"]
    .map((key) => parts.find((p) => p.type === key).value)
    .join("-");
}
function addDays(value, days) {
  const d = new Date(`${value}T12:00:00Z`);
  d.setUTCDate(d.getUTCDate() + days);
  return d.toISOString().slice(0, 10);
}
export function revenuePreset(preset) {
  const to = todayKey();
  let from = to;
  if (preset === "week")
    from = addDays(to, -((new Date(`${to}T12:00:00Z`).getUTCDay() + 6) % 7));
  if (preset === "month") from = `${to.slice(0, 7)}-01`;
  if (preset === "year") from = `${to.slice(0, 4)}-01-01`;
  return { from, to };
}
function query(values) {
  return new URLSearchParams(
    Object.entries(values).filter(([, value]) => value !== ""),
  ).toString();
}
function useReport(path, reload) {
  const [result, setResult] = useState({
    data: null,
    loading: true,
    error: "",
  });
  useEffect(() => {
    let current = true;
    setResult({ data: null, loading: true, error: "" });
    api(path)
      .then((data) => {
        if (current) setResult({ data, loading: false, error: "" });
      })
      .catch((e) => {
        if (current)
          setResult({ data: null, loading: false, error: e.message });
      });
    return () => {
      current = false;
    };
  }, [path, reload]);
  return result;
}
function Status({ value }) {
  return (
    <span className={`commerce-status commerce-status-${value.toLowerCase()}`}>
      {statuses[value] || value}
    </span>
  );
}
function RevenueChart({ points }) {
  const [active, setActive] = useState(null);
  const max = Math.max(0, ...points.map((p) => p.amountVnd));
  if (!max)
    return (
      <div className="commerce-empty">
        {icon("insights")}
        <strong>Chưa phát sinh doanh thu trong khoảng này</strong>
        <span>Đơn hàng được ghi nhận sau khi payOS xác nhận thanh toán.</span>
      </div>
    );
  const selected =
    points[active ?? points.findIndex((p) => p.amountVnd === max)] || points[0];
  return (
    <>
      <div className="commerce-chart-detail" aria-live="polite">
        <span>
          {date(selected.from)}
          {selected.to !== selected.from ? ` – ${date(selected.to)}` : ""}
        </span>
        <strong>{money(selected.amountVnd)}</strong>
        <span>{selected.paidOrders} đơn thanh toán</span>
      </div>
      <div className="commerce-chart">
        <div className="commerce-chart-scale" aria-hidden="true">
          {[1, 0.75, 0.5, 0.25, 0].map((ratio) => (
            <span key={ratio}>
              {new Intl.NumberFormat("vi-VN", {
                notation: "compact",
                maximumFractionDigits: 1,
              }).format(max * ratio)}
            </span>
          ))}
        </div>
        <div
          className="commerce-chart-scroll"
          tabIndex={0}
          aria-label="Biểu đồ doanh thu, cuộn ngang để xem thêm"
        >
          <div
            className="commerce-bars"
            style={{ minWidth: Math.max(260, points.length * 40) }}
          >
            {points.map((p, i) => (
              <button
                type="button"
                key={p.from}
                className={`commerce-bar ${p === selected ? "is-active" : ""}`}
                onMouseEnter={() => setActive(i)}
                onFocus={() => setActive(i)}
                onClick={() => setActive(i)}
                aria-label={`${date(p.from)}${p.to !== p.from ? ` đến ${date(p.to)}` : ""}: ${money(p.amountVnd)}, ${p.paidOrders} đơn`}
              >
                <span className="commerce-bar-track">
                  <i style={{ height: `${(p.amountVnd / max) * 100}%` }} />
                </span>
                <span className="commerce-bar-label">
                  {p.from.slice(8, 10)}/{p.from.slice(5, 7)}
                </span>
              </button>
            ))}
          </div>
        </div>
      </div>
    </>
  );
}
function PurchaseDetail({ purchase: p, onClose }) {
  const ref = useRef(null);
  const id = useId();
  useEffect(() => {
    const opener = document.activeElement;
    const dialog = ref.current;
    dialog.showModal();
    return () => {
      dialog.close();
      if (opener?.isConnected) opener.focus();
    };
  }, []);
  return (
    <dialog
      ref={ref}
      className="commerce-dialog"
      aria-labelledby={id}
      onCancel={(e) => {
        e.preventDefault();
        onClose();
      }}
    >
      <header>
        <div>
          <span className="commerce-eyebrow">CHI TIẾT ĐƠN HÀNG</span>
          <h2 id={id}>#{p.orderCode}</h2>
        </div>
        <button
          type="button"
          className="btn secondary small"
          onClick={onClose}
          aria-label="Đóng chi tiết đơn hàng"
        >
          {icon("close")}
        </button>
      </header>
      <div className="commerce-receipt-total">
        <Status value={p.status} />
        <strong>{money(p.amountVnd)}</strong>
        <span>{p.productName} · payOS</span>
      </div>
      <dl>
        {[
          ["Người mua", p.buyerName],
          ["Email", p.email],
          ["Sản phẩm", p.productName],
          ["Ngày tạo", timestamp(p.createdAt)],
          ["Ngày xác nhận thanh toán", timestamp(p.paidAt)],
          ["Hạn thanh toán", timestamp(p.expiresAt)],
          ["Mã giao dịch đối soát", p.receiptReference || "Chưa có"],
          ["ID đơn hàng", p.id],
        ].map(([label, value]) => (
          <div key={label}>
            <dt>{label}</dt>
            <dd>{value}</dd>
          </div>
        ))}
      </dl>
      <p>
        Trạng thái được cập nhật từ thanh toán đã xác minh. Doanh thu chỉ ghi
        nhận đơn đã thanh toán.
      </p>
    </dialog>
  );
}
export function AdminCommerce() {
  const [range, setRange] = useState(() => revenuePreset("month"));
  const [draft, setDraft] = useState(range);
  const [preset, setPreset] = useState("month");
  const [groupBy, setGroupBy] = useState("day");
  const [reload, setReload] = useState(0);
  const [filter, setFilter] = useState({ status: "", search: "", page: 1 });
  const [search, setSearch] = useState("");
  const [error, setError] = useState("");
  const [detail, setDetail] = useState(null);
  const report = useReport(
    `/api/admin/commerce/revenue?${query({ ...range, groupBy })}`,
    reload,
  );
  const history = useReport(
    `/api/admin/commerce/purchases?${query({ ...range, ...filter, pageSize: 20 })}`,
    reload,
  );
  const busy = report.loading || history.loading;
  function choosePreset(value) {
    const next = revenuePreset(value);
    setRange(next);
    setDraft(next);
    setPreset(value);
    setError("");
    setFilter((f) => ({ ...f, page: 1 }));
  }
  function applyRange(e) {
    e.preventDefault();
    const days = (Date.parse(draft.to) - Date.parse(draft.from)) / 86400000;
    if (
      !draft.from ||
      !draft.to ||
      !Number.isFinite(days) ||
      days < 0 ||
      days >= 366
    ) {
      setError("Chọn khoảng ngày hợp lệ, tối đa 366 ngày.");
      return;
    }
    setRange(draft);
    setPreset("");
    setError("");
    setFilter((f) => ({ ...f, page: 1 }));
  }
  const data = report.data;
  const orders = history.data;
  const pages = Math.max(1, Math.ceil((orders?.total || 0) / 20));
  useEffect(() => {
    if (orders && filter.page > pages)
      setFilter((current) => ({ ...current, page: pages }));
  }, [orders, filter.page, pages]);
  return (
    <section
      className="admin-commerce section-gap"
      aria-label="Quản lý doanh thu và mua hàng"
    >
      <header className="commerce-header">
        <div>
          <span className="commerce-eyebrow">
            PLANORA · QUẢN TRỊ THANH TOÁN
          </span>
          <h2>Doanh thu & đơn hàng</h2>
          <p>Theo dõi tiền đã nhận và toàn bộ lịch sử mua hàng ở một nơi.</p>
        </div>
        <button
          type="button"
          className="btn secondary small"
          disabled={busy}
          onClick={() => setReload((n) => n + 1)}
        >
          {icon("refresh")}Làm mới doanh thu
        </button>
      </header>
      <div className="commerce-kpis">
        {[
          ["Doanh thu hôm nay", data?.today, "Từ 00:00 hôm nay"],
          ["Doanh thu tuần này", data?.week, "Từ thứ Hai đến hôm nay"],
          ["Doanh thu tháng này", data?.month, "Từ đầu tháng đến hôm nay"],
          ["Tổng doanh thu", data?.allTime, "Toàn bộ thời gian"],
        ].map(([label, value, hint], i) => (
          <article
            key={label}
            className={`commerce-kpi ${i === 0 ? "is-primary" : ""}`}
          >
            <span>{label}</span>
            <strong>{value ? money(value.amountVnd) : "—"}</strong>
            <small>
              {value ? `${value.paidOrders} đơn đã thanh toán` : "Đang tải…"}
            </small>
            <span className="commerce-kpi-hint">{hint}</span>
          </article>
        ))}
      </div>
      <div className="commerce-panel">
        <div className="commerce-panel-heading">
          <div>
            <h3>Phân tích doanh thu</h3>
            <p>VND · Múi giờ Việt Nam (UTC+7)</p>
          </div>
          <div className="commerce-segment" aria-label="Khoảng thời gian">
            {[
              ["today", "Hôm nay"],
              ["week", "Tuần này"],
              ["month", "Tháng này"],
              ["year", "Năm nay"],
            ].map(([value, label]) => (
              <button
                type="button"
                key={value}
                aria-pressed={preset === value}
                onClick={() => choosePreset(value)}
              >
                {label}
              </button>
            ))}
          </div>
        </div>
        <form className="commerce-range" onSubmit={applyRange}>
          <label>
            Từ ngày
            <input
              type="date"
              required
              min="2000-01-01"
              max="2100-12-31"
              value={draft.from}
              onChange={(e) => setDraft({ ...draft, from: e.target.value })}
            />
          </label>
          <label>
            Đến ngày
            <input
              type="date"
              required
              min="2000-01-01"
              max="2100-12-31"
              value={draft.to}
              onChange={(e) => setDraft({ ...draft, to: e.target.value })}
            />
          </label>
          <button type="submit" className="btn secondary">
            Áp dụng
          </button>
          <label className="commerce-group">
            Biểu đồ theo
            <select
              aria-label="Biểu đồ theo"
              value={groupBy}
              onChange={(e) => setGroupBy(e.target.value)}
            >
              <option value="day">Ngày</option>
              <option value="week">Tuần</option>
              <option value="month">Tháng</option>
            </select>
          </label>
        </form>
        {error && (
          <p role="alert" className="commerce-error">
            {error}
          </p>
        )}
        {report.error && (
          <p role="alert" className="commerce-error">
            {report.error} Bấm “Làm mới doanh thu” để thử lại.
          </p>
        )}
        {report.loading && (
          <div role="status" className="commerce-loading">
            Đang tổng hợp doanh thu…
          </div>
        )}
        {data && (
          <>
            <div className="commerce-range-summary">
              <div>
                <span>
                  Doanh thu {date(data.from)} – {date(data.to)}
                </span>
                <strong>{money(data.selected.amountVnd)}</strong>
              </div>
              <div>
                <b>{data.selected.paidOrders}</b>
                <span>Đơn đã thanh toán</span>
              </div>
              <div>
                <b>{data.payingCustomers}</b>
                <span>Khách mua hàng</span>
              </div>
              <div>
                <b>
                  {money(
                    data.selected.paidOrders
                      ? data.selected.amountVnd / data.selected.paidOrders
                      : 0,
                  )}
                </b>
                <span>Trung bình / đơn</span>
              </div>
            </div>
            <RevenueChart
              key={`${data.from}-${data.to}-${groupBy}`}
              points={data.points}
            />
            <p className="commerce-footnote">
              Chỉ tính đơn đã xác nhận thanh toán. Đơn chờ, hết hạn hoặc hủy
              không tính vào doanh thu. Tuần bắt đầu từ thứ Hai.
            </p>
          </>
        )}
      </div>
      <div className="commerce-panel">
        <div className="commerce-panel-heading">
          <div>
            <h3>Lịch sử mua hàng</h3>
            <p>
              Trong khoảng {date(range.from)} – {date(range.to)} ·{" "}
              {orders?.total ?? "—"} đơn
            </p>
          </div>
        </div>
        <form
          className="commerce-history-filters"
          onSubmit={(e) => {
            e.preventDefault();
            setFilter({ ...filter, search: search.trim(), page: 1 });
          }}
        >
          <label className="commerce-search">
            Tìm đơn hàng
            <input
              type="search"
              value={search}
              maxLength={100}
              onChange={(e) => setSearch(e.target.value)}
              placeholder="Email, người mua, mã đơn hoặc mã giao dịch"
            />
          </label>
          <label>
            Trạng thái
            <select
              aria-label="Trạng thái"
              value={filter.status}
              onChange={(e) =>
                setFilter({ ...filter, status: e.target.value, page: 1 })
              }
            >
              <option value="">Tất cả trạng thái</option>
              {Object.entries(statuses).map(([value, label]) => (
                <option key={value} value={value}>
                  {label}
                </option>
              ))}
            </select>
          </label>
          <button type="submit" className="btn secondary">
            Tìm kiếm đơn
          </button>
        </form>
        <p className="commerce-footnote">
          Đơn đã thanh toán lọc theo ngày xác nhận tiền; các đơn còn lại lọc
          theo ngày tạo.
        </p>
        {history.error && (
          <p role="alert" className="commerce-error">
            {history.error} Bấm “Làm mới doanh thu” để thử lại.
          </p>
        )}
        {history.loading && (
          <p role="status" className="commerce-loading">
            Đang tải lịch sử mua hàng…
          </p>
        )}
        {orders && (
          <>
            {!orders.items.length ? (
              <div className="commerce-empty">
                <strong>Không tìm thấy đơn hàng</strong>
                <span>
                  Thử thay đổi khoảng ngày, trạng thái hoặc từ khóa tìm kiếm.
                </span>
              </div>
            ) : (
              <div
                className="commerce-table-scroll"
                tabIndex={0}
                aria-label="Bảng lịch sử mua hàng"
              >
                <table className="commerce-table">
                  <thead>
                    <tr>
                      {[
                        "Đơn hàng",
                        "Người mua",
                        "Sản phẩm",
                        "Số tiền",
                        "Trạng thái",
                        "Ngày ghi nhận",
                        "Chi tiết",
                      ].map((label) => (
                        <th key={label} scope="col">
                          {label}
                        </th>
                      ))}
                    </tr>
                  </thead>
                  <tbody>
                    {orders.items.map((p) => (
                      <tr key={p.id}>
                        <td>
                          <b>#{p.orderCode}</b>
                          <small>payOS</small>
                        </td>
                        <td>
                          <b>{p.buyerName}</b>
                          <small>{p.email}</small>
                        </td>
                        <td>{p.productName}</td>
                        <td className="commerce-amount">
                          {money(p.amountVnd)}
                        </td>
                        <td>
                          <Status value={p.status} />
                        </td>
                        <td>
                          {timestamp(
                            p.status === "PAID"
                              ? p.paidAt || p.createdAt
                              : p.createdAt,
                          )}
                        </td>
                        <td>
                          <button
                            type="button"
                            className="btn small secondary"
                            onClick={() => setDetail(p)}
                            aria-label={`Xem đơn ${p.orderCode}`}
                          >
                            Xem
                          </button>
                        </td>
                      </tr>
                    ))}
                  </tbody>
                </table>
              </div>
            )}
            <div className="commerce-pagination">
              <span>
                {orders.total
                  ? `${(orders.page - 1) * 20 + 1}–${Math.min(orders.page * 20, orders.total)} / ${orders.total} đơn`
                  : "0 đơn"}
              </span>
              <div>
                <button
                  type="button"
                  className="btn secondary small"
                  disabled={filter.page <= 1}
                  onClick={() =>
                    setFilter({ ...filter, page: filter.page - 1 })
                  }
                >
                  Trang trước
                </button>
                <span>
                  Trang {orders.page} / {pages}
                </span>
                <button
                  type="button"
                  className="btn secondary small"
                  disabled={filter.page >= pages}
                  onClick={() =>
                    setFilter({ ...filter, page: filter.page + 1 })
                  }
                >
                  Trang sau
                </button>
              </div>
            </div>
          </>
        )}
      </div>
      {data && (
        <p className="commerce-updated">
          Cập nhật lúc {timestamp(data.generatedAt)} · Chọn “Làm mới doanh thu”
          để lấy số liệu mới nhất.
        </p>
      )}
      {detail && (
        <PurchaseDetail purchase={detail} onClose={() => setDetail(null)} />
      )}
    </section>
  );
}
