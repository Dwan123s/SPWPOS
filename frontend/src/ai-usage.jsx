import { useEffect, useState } from "react";
import { api } from "./api.js";
export function AiUsageMeter({ refresh }) {
  const [data, setData] = useState(null);
  const [error, setError] = useState("");
  const [retry, setRetry] = useState(0);
  useEffect(() => {
    let active = true;
    api("/api/agent/usage")
      .then((value) => {
        if (!Number.isFinite(value?.dailyLimit))
          throw new Error("Chưa tải được hạn mức AI.");
        if (active) {
          setData(value);
          setError("");
        }
      })
      .catch((e) => {
        if (active) setError(e.message);
      });
    return () => {
      active = false;
    };
  }, [refresh, retry]);
  return (
    <details className="nora-usage">
      <summary>
        Hạn mức AI {data ? `· ${data.used}/${data.dailyLimit} lượt` : ""}
      </summary>
      {error && (
        <p>
          {error}{" "}
          <button type="button" onClick={() => setRetry((x) => x + 1)}>
            Tải lại hạn mức
          </button>
        </p>
      )}
      {data && (
        <>
          <p>
            {data.active}/{data.concurrentLimit} yêu cầu đang xử lý. Làm mới lúc{" "}
            {new Date(data.resetsAt).toLocaleString("vi-VN")}.
          </p>
          <p>
            Token đầu vào: {data.inputTokens ?? "Chưa có dữ liệu"} · đầu ra:{" "}
            {data.outputTokens ?? "Chưa có dữ liệu"}.
          </p>
          {data.unknownUsageCalls > 0 && (
            <p>
              {data.unknownUsageCalls} lượt chưa có đầy đủ số token từ nhà cung
              cấp.
            </p>
          )}
          <p>
            Chưa cấu hình bảng giá nên chưa quy đổi thành tiền. Đây là hạn mức
            của PLANORA; Google Cloud có quota riêng. Lượt thất bại có thể vẫn
            được tính vì nhà cung cấp có thể đã xử lý.
          </p>
        </>
      )}
    </details>
  );
}
