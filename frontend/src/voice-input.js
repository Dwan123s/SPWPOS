import { useEffect, useRef, useState } from "react";
import { api } from "./api.js";

const MAX_BYTES = 2 * 1024 * 1024;
const MAX_SECONDS = 60;
const formats = [
  "audio/webm;codecs=opus",
  "audio/webm",
  "audio/ogg;codecs=opus",
  "audio/mp4",
];
function recordingFormat() {
  if (
    globalThis.isSecureContext === false ||
    !navigator.mediaDevices?.getUserMedia ||
    typeof MediaRecorder !== "function"
  )
    return null;
  return formats.find((type) => MediaRecorder.isTypeSupported?.(type)) || null;
}
export async function transcribeVoice(blob, signal, retry = true) {
  const response = await fetch("/api/agent/voice/transcribe", {
    method: "POST",
    credentials: "same-origin",
    signal,
    headers: { "X-SPWPOS": "1", "Content-Type": blob.type.split(";")[0] },
    body: blob,
  });
  if (response.status === 401 && retry) {
    await api("/api/browser-session/me");
    signal.throwIfAborted();
    return transcribeVoice(blob, signal, false);
  }
  let payload;
  try {
    payload = await response.json();
  } catch {
    /* Never show raw provider responses. */
  }
  if (!response.ok)
    throw new Error(
      payload?.error?.message ||
        "Chưa chuyển được giọng nói thành chữ. Hãy thử lại.",
    );
  if (
    typeof payload?.data?.text !== "string" ||
    !payload.data.text.trim() ||
    payload.data.text.length > 2000
  )
    throw new Error("Chưa nhận được bản chép lời hợp lệ. Hãy thử lại.");
  return payload.data.text.trim();
}

export function useVoiceInput({ text, onTranscript, onPreview, enabled }) {
  const [phase, setPhase] = useState("idle");
  const [seconds, setSeconds] = useState(0);
  const [error, setError] = useState("");
  const [completed, setCompleted] = useState(0);
  const [previewing, setPreviewing] = useState(false);
  const session = useRef(null);
  const callbacks = useRef({ text, onTranscript, onPreview, enabled });
  callbacks.current = { text, onTranscript, onPreview, enabled };
  const mimeType = recordingFormat();
  function combined(ctx, transcript) {
    return (
      ctx.base +
      (ctx.base && !/\s$/.test(ctx.base) ? " " : "") +
      transcript.trim()
    );
  }
  function preview(ctx, transcript) {
    if (session.current !== ctx || ctx.finish || !transcript.trim()) return;
    const next = combined(ctx, transcript);
    if (next.length > 2000) return;
    ctx.preview = next;
    setPreviewing(true);
    callbacks.current.onPreview?.(next);
  }
  function stopLive(ctx) {
    ctx.previewAbort?.abort();
    clearTimeout(ctx.previewLimit);
    if (ctx.recognition) {
      ctx.recognition.onresult =
        ctx.recognition.onerror =
        ctx.recognition.onend =
          null;
      try {
        ctx.recognition.abort();
      } catch {
        /* Already disconnected. */
      }
    }
  }
  function startLive(ctx) {
    const Recognition =
      globalThis.SpeechRecognition || globalThis.webkitSpeechRecognition;
    ctx.cloudLive = true;
    if (typeof Recognition !== "function") return;
    try {
      const recognition = new Recognition();
      ctx.recognition = recognition;
      recognition.lang = "vi-VN";
      recognition.continuous = true;
      recognition.interimResults = true;
      recognition.maxAlternatives = 1;
      ctx.cloudLive = false;
      recognition.onresult = (event) => {
        if (session.current !== ctx || ctx.finish) return;
        const words = Array.from(event.results)
          .map((result) => result[0]?.transcript || "")
          .join(" ");
        if (words.trim()) {
          ctx.browserResultAt = Date.now();
          preview(ctx, words);
        }
      };
      // If the browser's speech service stops or fails, the recorded audio still powers Cloud previews.
      recognition.onerror = recognition.onend = () => {
        if (session.current === ctx && !ctx.finish) ctx.cloudLive = true;
      };
      recognition.start();
    } catch {
      ctx.cloudLive = true;
    }
  }
  async function cloudPreview(ctx) {
    if (
      !callbacks.current.onPreview ||
      session.current !== ctx ||
      ctx.finish ||
      ctx.previewPending ||
      !ctx.chunks.length
    )
      return;
    const now = Date.now();
    if (now - (ctx.lastPreviewAt || ctx.started) < 4000) return;
    if (!ctx.cloudLive && now - (ctx.browserResultAt || ctx.started) < 6000)
      return;
    ctx.lastPreviewAt = now;
    ctx.previewPending = true;
    const abort = new AbortController();
    ctx.previewAbort = abort;
    ctx.previewLimit = setTimeout(() => abort.abort(), 12000);
    try {
      // Include the header; later recognition replaces the cumulative preview without duplicating words.
      const blob = new Blob(ctx.chunks, {
        type: ctx.recorder.mimeType || mimeType,
      });
      const words = await transcribeVoice(blob, abort.signal);
      if (ctx.cloudLive || !ctx.browserResultAt || ctx.browserResultAt <= now)
        preview(ctx, words);
    } catch {
      /* Silence or incomplete containers do not interrupt recording; the final check stays authoritative. */
    } finally {
      clearTimeout(ctx.previewLimit);
      ctx.previewPending = false;
    }
  }
  function release(ctx) {
    clearTimeout(ctx.limit);
    clearTimeout(ctx.uploadLimit);
    clearInterval(ctx.clock);
    ctx.stream?.getTracks().forEach((track) => {
      track.onended = null;
      track.stop();
    });
  }
  function dispose(ctx) {
    stopLive(ctx);
    release(ctx);
    ctx.abort?.abort();
    if (ctx.recorder) {
      ctx.recorder.onstart =
        ctx.recorder.onstop =
        ctx.recorder.ondataavailable =
        ctx.recorder.onerror =
          null;
      if (ctx.recorder.state !== "inactive")
        try {
          ctx.recorder.stop();
        } catch {
          /* Already stopped. */
        }
    }
    ctx.chunks = [];
  }
  function cancel() {
    const ctx = session.current;
    session.current = null;
    if (ctx) {
      dispose(ctx);
      callbacks.current.onPreview?.(ctx.base);
    }
    setPreviewing(false);
    setPhase("idle");
  }
  function fail(ctx, message) {
    if (session.current !== ctx) return;
    session.current = null;
    dispose(ctx);
    callbacks.current.onPreview?.(ctx.base);
    setPreviewing(false);
    setPhase("idle");
    setError(message);
  }
  function stop() {
    const ctx = session.current;
    if (!ctx || ctx.recorder?.state !== "recording") return;
    ctx.finish = true;
    stopLive(ctx);
    clearTimeout(ctx.limit);
    clearInterval(ctx.clock);
    setPhase("transcribing");
    try {
      ctx.recorder.stop();
      release(ctx);
    } catch {
      fail(ctx, "Chưa hoàn tất bản ghi. Hãy thử lại.");
    }
  }
  async function start() {
    if (session.current || !mimeType || !callbacks.current.enabled) return;
    if (callbacks.current.text.length >= 2000) {
      setError("Ô chat đã đầy. Hãy rút ngắn nội dung trước khi ghi thêm.");
      return;
    }
    const ctx = {
      base: callbacks.current.text,
      chunks: [],
      size: 0,
      abort: new AbortController(),
    };
    session.current = ctx;
    setError("");
    setSeconds(0);
    setPreviewing(false);
    setPhase("requesting");
    try {
      const stream = await navigator.mediaDevices.getUserMedia({ audio: true });
      if (session.current !== ctx) {
        stream.getTracks().forEach((track) => track.stop());
        return;
      }
      ctx.stream = stream;
      const recorder = new MediaRecorder(stream, {
        mimeType,
        audioBitsPerSecond: 64000,
      });
      ctx.recorder = recorder;
      stream.getAudioTracks().forEach((track) => {
        track.onended = () =>
          fail(
            ctx,
            "Micro đã ngắt kết nối. Hãy kiểm tra thiết bị rồi thử lại.",
          );
      });
      recorder.onstart = () => {
        if (session.current !== ctx) return;
        ctx.started = Date.now();
        setPhase("recording");
        startLive(ctx);
        ctx.clock = setInterval(
          () =>
            setSeconds(
              Math.min(
                MAX_SECONDS,
                Math.floor((Date.now() - ctx.started) / 1000),
              ),
            ),
          1000,
        );
        ctx.limit = setTimeout(stop, MAX_SECONDS * 1000);
      };
      recorder.ondataavailable = (event) => {
        if (session.current !== ctx || !event.data.size) return;
        ctx.size += event.data.size;
        if (ctx.size > MAX_BYTES) {
          fail(ctx, "Bản ghi quá lớn. Hãy ghi một đoạn ngắn hơn.");
          return;
        }
        ctx.chunks.push(event.data);
        if (!ctx.finish) void cloudPreview(ctx);
      };
      recorder.onerror = () =>
        fail(ctx, "Không ghi được âm thanh. Hãy kiểm tra micro rồi thử lại.");
      recorder.onstop = async () => {
        release(ctx);
        if (session.current !== ctx) return;
        if (!ctx.finish) {
          fail(ctx, "Bản ghi bị gián đoạn. Hãy thử lại.");
          return;
        }
        const blob = new Blob(ctx.chunks, {
          type: recorder.mimeType || mimeType,
        });
        ctx.chunks = [];
        if (!blob.size) {
          fail(ctx, "Bản ghi chưa có âm thanh. Hãy nói rồi bấm dừng.");
          return;
        }
        ctx.uploadLimit = setTimeout(() => ctx.abort.abort(), 30000);
        try {
          const transcript = await transcribeVoice(blob, ctx.abort.signal);
          if (session.current !== ctx) return;
          const next = combined(ctx, transcript);
          if (next.length > 2000) {
            fail(
              ctx,
              "Nội dung vượt giới hạn ô chat. Hãy rút ngắn bản nháp và ghi đoạn ngắn hơn.",
            );
            return;
          }
          callbacks.current.onTranscript(next);
          setCompleted((value) => value + 1);
          session.current = null;
          dispose(ctx);
          setPreviewing(false);
          setPhase("idle");
        } catch (failure) {
          fail(
            ctx,
            ctx.abort.signal.aborted
              ? "Chuyển giọng nói quá lâu. Hãy thử lại hoặc nhập bằng bàn phím."
              : failure.message,
          );
        }
      };
      recorder.start(1000);
    } catch (failure) {
      const message =
        failure.name === "NotAllowedError" || failure.name === "SecurityError"
          ? "Chưa được phép dùng micro. Cho phép micro trong cài đặt của trang rồi thử lại."
          : failure.name === "NotFoundError"
            ? "Không tìm thấy micro. Hãy kết nối micro rồi thử lại."
            : "Không mở được micro. Kiểm tra thiết bị hoặc ứng dụng đang dùng micro rồi thử lại.";
      fail(ctx, message);
    }
  }
  useEffect(() => {
    const abandon = () => {
      if (!document.hidden) return;
      const ctx = session.current;
      session.current = null;
      if (ctx) {
        dispose(ctx);
        callbacks.current.onPreview?.(ctx.base);
        setPreviewing(false);
        setPhase("idle");
        setError("Đã hủy ghi âm khi rời trang. Bản nháp vẫn được giữ.");
      }
    };
    document.addEventListener("visibilitychange", abandon);
    return () => {
      document.removeEventListener("visibilitychange", abandon);
      const ctx = session.current;
      session.current = null;
      if (ctx) dispose(ctx);
    };
  }, []);
  return {
    phase,
    seconds,
    error,
    completed,
    previewing,
    supported: Boolean(mimeType),
    active: phase !== "idle",
    start,
    stop,
    cancel,
    isActive: () => session.current !== null,
    clearError: () => setError(""),
  };
}
