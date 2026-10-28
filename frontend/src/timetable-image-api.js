import { api } from "./api.js";

export async function readTimetableImage(file, signal, retry = true) {
  const response = await fetch("/api/agent/images/timetable", {
    method: "POST",
    credentials: "same-origin",
    signal,
    headers: {
      "Content-Type": file.type,
      "X-SPWPOS": "1",
      "X-SPWPOS-Image-Consent": "1",
    },
    body: file,
  });
  if (response.status === 401 && retry) {
    await api("/api/browser-session/me");
    signal.throwIfAborted();
    return readTimetableImage(file, signal, false);
  }
  let payload;
  try {
    payload = await response.json();
  } catch {
    /* Never display raw provider output. */
  }
  if (!response.ok)
    throw Object.assign(
      new Error(
        payload?.error?.message ||
          "Chưa đọc được ảnh thời khóa biểu. Bạn thử lại nhé.",
      ),
      { code: payload?.error?.code, status: response.status },
    );
  const value = payload?.data;
  if (
    typeof value?.text !== "string" ||
    !value.text.trim() ||
    value.text.length > 1600 ||
    !Array.isArray(value.warnings) ||
    value.warnings.length > 8 ||
    value.warnings.some((x) => typeof x !== "string" || x.length > 200) ||
    typeof value.needsReview !== "boolean"
  )
    throw new Error(
      "Mình chưa đọc đầy đủ thời khóa biểu. Bạn thử ảnh rõ hơn nhé.",
    );
  return { ...value, text: value.text.trim() };
}
