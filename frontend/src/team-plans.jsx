import { useEffect, useState } from "react";
import { api } from "./api.js";
import { safePayOsCheckout } from "./payos-checkout.js";
import { icon } from "./ui.jsx";
import "./team-plans.css";

const planNames = {
  free: "Free",
  premium20: "Premium 20",
  premiumUnlimited: "Premium không giới hạn",
};
const money = new Intl.NumberFormat("vi-VN", {
  style: "currency",
  currency: "VND",
});

export function TeamPlans() {
  const [billing, setBilling] = useState(null);
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState("");
  const current = billing?.plan ?? {
    code: "free",
    memberLimit: 5,
    amountVnd: 0,
  };

  useEffect(() => {
    const request = new AbortController();
    api("/api/commerce/premium", "GET", undefined, true, request.signal)
      .then((result) => {
        if (request.signal.aborted) return;
        setBilling(result);
      })
      .catch((failure) => {
        if (!request.signal.aborted) setError(failure.message);
      });
    return () => request.abort();
  }, []);

  async function perform(action) {
    if (busy) return;
    setBusy(true);
    setError("");
    try {
      await action();
    } catch (failure) {
      setError(failure.message);
    } finally {
      setBusy(false);
    }
  }

  const checkout = safePayOsCheckout(billing?.order?.checkoutUrl);
  const pending = billing?.order?.status === "PENDING";
  return (
    <section className="team-panel team-plans" aria-label="Gói của tài khoản">
      <div className="team-panel-heading">
        <div>
          <h3>Chọn gói phù hợp với bạn</h3>
          <p>Áp dụng cho tất cả nhóm bạn tạo.</p>
        </div>
        <span className="premium-current">
          {icon("shield")} {planNames[current.code]}
        </span>
      </div>
      <p className="premium-account-status">
        {current.memberLimit == null
          ? "Không giới hạn thành viên trong mỗi nhóm bạn tạo"
          : `Tối đa ${current.memberLimit} thành viên trong mỗi nhóm bạn tạo`}
        {" · Tính cả trưởng nhóm"}
        {billing?.validUntil && (
          <>
            {" "}
            · Có hiệu lực đến{" "}
            {new Intl.DateTimeFormat("vi-VN", { dateStyle: "long" }).format(
              new Date(billing.validUntil),
            )}
          </>
        )}
      </p>
      {
        <>
          <div className="team-plan-options">
            {(billing?.plans ?? [current]).map((plan) => (
              <div
                className="team-plan-option"
                key={plan.code}
                data-current={plan.code === current.code}
                data-plan={plan.code}
              >
                <span className="premium-plan-icon" aria-hidden="true">
                  {icon(
                    plan.amountVnd === 0
                      ? "users"
                      : plan.memberLimit == null
                        ? "spark"
                        : "shield",
                  )}
                </span>
                <h4>{planNames[plan.code]}</h4>
                <p>
                  {plan.memberLimit == null
                    ? "Không giới hạn thành viên"
                    : `Tối đa ${plan.memberLimit} thành viên`}
                </p>
                <div className="premium-price">
                  <strong>
                    {plan.amountVnd === 0
                      ? "Miễn phí"
                      : money.format(plan.amountVnd)}
                  </strong>
                  {plan.amountVnd > 0 && <span>/ tháng</span>}
                </div>
                {plan.amountVnd > 0 && (
                  <small>Hiệu lực 1 tháng · Gia hạn khi cần</small>
                )}
                <ul className="premium-benefits">
                  <li>
                    {icon("check")}{" "}
                    {plan.memberLimit == null
                      ? "Không giới hạn thành viên mỗi nhóm"
                      : `${plan.memberLimit} thành viên mỗi nhóm`}
                  </li>
                  <li>{icon("check")} Áp dụng cho mọi nhóm bạn tạo</li>
                </ul>
                {plan.amountVnd === 0 ? (
                  <span className="premium-free-status">
                    {current.code === plan.code
                      ? "Gói hiện tại"
                      : "Gói miễn phí"}
                  </span>
                ) : (
                  plan.amountVnd >= current.amountVnd &&
                  (pending &&
                  checkout &&
                  billing.order.planCode === plan.code ? (
                    <a
                      className="btn primary"
                      href={checkout}
                      target="_blank"
                      rel="noopener noreferrer"
                    >
                      Tiếp tục thanh toán {icon("arrow")}
                    </a>
                  ) : (
                    <button
                      type="button"
                      className="btn primary"
                      disabled={
                        busy ||
                        !billing?.available ||
                        (pending &&
                          (!!checkout || billing.order.planCode !== plan.code))
                      }
                      onClick={() =>
                        perform(async () => {
                          const order = await api(
                            "/api/commerce/premium",
                            "POST",
                            { code: plan.code },
                          );
                          setBilling((value) => ({ ...value, order }));
                        })
                      }
                    >
                      {plan.code === current.code
                        ? "Gia hạn 1 tháng"
                        : pending && billing.order.planCode !== plan.code
                          ? "Đang có đơn chờ thanh toán"
                          : `Chọn ${planNames[plan.code]}`}
                      {pending && billing.order.planCode !== plan.code && (
                        <span className="premium-pending-hint">
                          Hoàn tất đơn hiện tại để chọn gói khác
                        </span>
                      )}
                    </button>
                  ))
                )}
              </div>
            ))}
          </div>
          {billing && !billing.available && (
            <p role="status">Thanh toán payOS chưa sẵn sàng.</p>
          )}
          {pending && (
            <div className="premium-payment-panel">
              {
                <p role="status">
                  Đơn {planNames[billing.order.planCode]} đang chờ thanh toán.
                </p>
              }
              <div className="team-actions">
                {checkout && (
                  <a
                    className="btn primary"
                    href={checkout}
                    target="_blank"
                    rel="noopener noreferrer"
                  >
                    Thanh toán qua payOS {icon("arrow")}
                  </a>
                )}
                <button
                  type="button"
                  className="btn secondary"
                  disabled={busy}
                  onClick={() =>
                    perform(async () =>
                      setBilling(await api("/api/commerce/premium")),
                    )
                  }
                >
                  Kiểm tra thanh toán
                </button>
              </div>
            </div>
          )}
          {billing?.order?.status === "PAID" && current.code !== "free" && (
            <p role="status">
              Đã xác nhận thanh toán và kích hoạt Premium cho tài khoản.
            </p>
          )}
          {billing?.order?.status === "PAID" && current.code === "free" && (
            <p role="status">
              Gói Premium đã hết hạn. Chọn gói để sử dụng thêm 1 tháng.
            </p>
          )}
          {["EXPIRED", "CANCELLED"].includes(billing?.order?.status) && (
            <p role="status">
              Đơn thanh toán đã hết hạn hoặc bị hủy. Bạn có thể chọn gói để tạo
              đơn mới.
            </p>
          )}
          {error && (
            <p className="team-error" role="alert">
              {error}
            </p>
          )}
          {!billing && error && (
            <button
              type="button"
              className="btn secondary"
              disabled={busy}
              onClick={() =>
                perform(async () => {
                  setBilling(await api("/api/commerce/premium"));
                })
              }
            >
              Thử lại
            </button>
          )}
        </>
      }
    </section>
  );
}
