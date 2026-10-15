export function ConnectionState({ onRetry, onLogin }) {
  const offline = navigator.onLine === false;
  return (
    <section
      className="planora-loading planora-loading--fullscreen"
      data-motion-state="paused"
      aria-labelledby="connection-title"
    >
      <div className="loading-content">
        <div className="loading-art" aria-hidden="true">
          <span className="loading-halo" />
          <div className="loading-mascot">
            <img
              src="/assets/start/planora-owl-v1.png"
              alt=""
              width="1254"
              height="1254"
            />
          </div>
        </div>
        <div className="loading-copy">
          <h1 id="connection-title">
            Chưa kết nối được
            <br />
            <span>không gian của bạn.</span>
          </h1>
          <p className="loading-label" role="alert">
            {offline
              ? "Thiết bị đang ngoại tuyến. Hãy kiểm tra mạng rồi thử lại."
              : "Kết nối chưa sẵn sàng. Bạn có thể thử lại mà không cần đăng nhập lại."}
          </p>
          <div className="loading-connection-actions">
            <button type="button" className="btn primary" onClick={onRetry}>
              Thử kết nối lại
            </button>
            <button type="button" className="btn secondary" onClick={onLogin}>
              Mở đăng nhập
            </button>
          </div>
        </div>
      </div>
    </section>
  );
}
