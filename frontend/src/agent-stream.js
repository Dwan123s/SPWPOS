import { api } from "./api.js";

export async function sendAgentStream(body, onEvent, signal, retry = true) {
  const response = await fetch("/api/agent/turns/stream", {
    method: "POST",
    credentials: "same-origin",
    signal,
    headers: {
      "Content-Type": "application/json",
      "X-SPWPOS": "1",
      Accept: "text/event-stream",
    },
    body: JSON.stringify(body),
  });
  if (response.status === 401 && retry) {
    // Reuse the account's existing cross-tab refresh lock before retrying a rejected request.
    await api("/api/browser-session/me");
    signal?.throwIfAborted();
    return sendAgentStream(body, onEvent, signal, false);
  }
  if (!response.ok) {
    let problem;
    try {
      problem = await response.json();
    } catch {
      /* Never display raw responses. */
    }
    throw Object.assign(
      new Error(
        problem?.error?.message || "Không thể kết nối với NORA. Hãy thử lại.",
      ),
      { code: problem?.error?.code, status: response.status },
    );
  }
  // Older adapters/test transports may return an authoritative JSON result.
  if (
    !response.body?.getReader ||
    !response.headers?.get("content-type")?.includes("text/event-stream")
  ) {
    const payload = await response.json();
    if (!payload.data?.id || !payload.data?.status)
      throw new Error("NORA chưa trả về kết quả hoàn chỉnh.");
    return payload.data;
  }
  const reader = response.body.getReader(),
    decoder = new TextDecoder();
  let buffer = "",
    result = null;
  function frame(value) {
    const data = value
      .split("\n")
      .filter((line) => line.startsWith("data:"))
      .map((line) => line.slice(5).trimStart())
      .join("\n");
    if (!data) return;
    let event;
    try {
      event = JSON.parse(data);
    } catch {
      throw new Error("Phản hồi streaming chưa đúng định dạng. Hãy thử lại.");
    }
    if (event.type === "error")
      throw Object.assign(
        new Error(event.message || "NORA chưa hoàn tất yêu cầu."),
        { code: event.code },
      );
    if (event.type === "result") {
      if (!event.turn?.id || !event.turn?.status)
        throw new Error("Kết quả NORA chưa hợp lệ.");
      result = event.turn;
    } else if (!result && ["phase", "tool", "delta"].includes(event.type))
      onEvent(event);
  }
  try {
    for (;;) {
      const { value, done } = await reader.read();
      buffer += decoder.decode(value, { stream: !done });
      buffer = buffer.replace(/\r\n/g, "\n");
      if (buffer.length > 1048576)
        throw new Error("Phản hồi NORA vượt giới hạn.");
      let end;
      while ((end = buffer.indexOf("\n\n")) !== -1) {
        frame(buffer.slice(0, end));
        buffer = buffer.slice(end + 2);
      }
      if (done) {
        if (buffer.trim()) frame(buffer);
        break;
      }
    }
    if (!result)
      throw new Error(
        "Kết nối bị ngắt trước khi NORA hoàn tất. Bạn có thể gửi lại cùng yêu cầu.",
      );
    return result;
  } finally {
    await reader.cancel().catch(() => {});
    reader.releaseLock();
  }
}
