import { afterEach, beforeEach, expect, it, vi } from "vitest";
import {
  act,
  fireEvent,
  render,
  renderHook,
  screen,
  waitFor,
} from "@testing-library/react";
import { useVoiceInput, transcribeVoice } from "../src/voice-input.js";
import { AgentPage } from "../src/agent.jsx";
import { state } from "../src/ui.jsx";

let recorder, track, stream, getUserMedia, mediaDescriptor;
class Recorder {
  static isTypeSupported(type) {
    return type.startsWith("audio/webm");
  }
  constructor(source, options) {
    recorder = this;
    this.state = "inactive";
    this.mimeType = options.mimeType;
  }
  start() {
    this.state = "recording";
    this.onstart?.();
  }
  stop() {
    this.state = "inactive";
    this.ondataavailable?.({
      data: new Blob([new Uint8Array([0x1a, 0x45, 0xdf, 0xa3, 1, 2])], {
        type: this.mimeType,
      }),
    });
    this.onstop?.();
  }
}
const transcriptResponse = (text = "Ngày mai họp từ 14 giờ đến 16 giờ.") =>
  new Response(JSON.stringify({ data: { text } }), {
    headers: { "content-type": "application/json" },
  });
beforeEach(() => {
  localStorage.clear();
  sessionStorage.clear();
  recorder = null;
  track = { stop: vi.fn(), onended: null };
  stream = { getTracks: () => [track], getAudioTracks: () => [track] };
  getUserMedia = vi.fn().mockResolvedValue(stream);
  mediaDescriptor = Object.getOwnPropertyDescriptor(navigator, "mediaDevices");
  Object.defineProperty(navigator, "mediaDevices", {
    configurable: true,
    value: { getUserMedia },
  });
  vi.stubGlobal("MediaRecorder", Recorder);
  vi.stubGlobal("isSecureContext", true);
  vi.stubGlobal("SpeechRecognition", undefined);
  vi.stubGlobal("webkitSpeechRecognition", undefined);
  vi.stubGlobal(
    "fetch",
    vi.fn().mockImplementation(async () => transcriptResponse()),
  );
});
afterEach(() => {
  if (mediaDescriptor)
    Object.defineProperty(navigator, "mediaDevices", mediaDescriptor);
  else delete navigator.mediaDevices;
  vi.useRealTimers();
  vi.unstubAllGlobals();
});
async function begin(result) {
  await act(async () => {
    await result.current.start();
  });
}

it("records only after a click, then uploads audio and appends the transcript without sending a chat turn", async () => {
  const onTranscript = vi.fn();
  const { result } = renderHook(() =>
    useVoiceInput({ text: "NORA ơi,", onTranscript, enabled: true }),
  );
  expect(getUserMedia).not.toHaveBeenCalled();
  await begin(result);
  expect(result.current.phase).toBe("recording");
  expect(fetch).not.toHaveBeenCalled();
  act(() => result.current.stop());
  await waitFor(() => expect(result.current.phase).toBe("idle"));
  expect(onTranscript).toHaveBeenCalledWith(
    "NORA ơi, Ngày mai họp từ 14 giờ đến 16 giờ.",
  );
  expect(track.stop).toHaveBeenCalled();
  const [url, request] = fetch.mock.calls[0];
  expect(url).toBe("/api/agent/voice/transcribe");
  expect(request.body).toBeInstanceOf(Blob);
  expect(request.headers["Content-Type"]).toBe("audio/webm");
  expect(request.headers["X-SPWPOS"]).toBe("1");
});
it("cancel discards the clip, keeps the draft and releases the microphone without uploading", async () => {
  const onTranscript = vi.fn();
  const { result } = renderHook(() =>
    useVoiceInput({ text: "Bản nháp", onTranscript, enabled: true }),
  );
  await begin(result);
  act(() => result.current.cancel());
  expect(result.current.active).toBe(false);
  expect(track.stop).toHaveBeenCalled();
  expect(fetch).not.toHaveBeenCalled();
  expect(onTranscript).not.toHaveBeenCalled();
});
it("late microphone permission after unmount immediately releases the device", async () => {
  let grant;
  getUserMedia.mockImplementation(
    () =>
      new Promise((resolve) => {
        grant = resolve;
      }),
  );
  const { result, unmount } = renderHook(() =>
    useVoiceInput({ text: "", onTranscript: vi.fn(), enabled: true }),
  );
  let pending;
  act(() => {
    pending = result.current.start();
  });
  unmount();
  await act(async () => {
    grant(stream);
    await pending;
  });
  expect(track.stop).toHaveBeenCalled();
  expect(recorder).toBeNull();
  expect(fetch).not.toHaveBeenCalled();
});
it("permission denial is actionable and does not replace the typed draft", async () => {
  getUserMedia.mockRejectedValue(
    new DOMException("private-details", "NotAllowedError"),
  );
  const onTranscript = vi.fn();
  const { result } = renderHook(() =>
    useVoiceInput({ text: "Bản nháp", onTranscript, enabled: true }),
  );
  await begin(result);
  expect(result.current.error).toContain("Cho phép micro");
  expect(result.current.error).not.toContain("private-details");
  expect(onTranscript).not.toHaveBeenCalled();
  expect(fetch).not.toHaveBeenCalled();
});
it("stops at 60 seconds and rejects overlong combined text without silently truncating the draft", async () => {
  vi.useFakeTimers();
  const onTranscript = vi.fn();
  const { result } = renderHook(() =>
    useVoiceInput({ text: "a".repeat(1999), onTranscript, enabled: true }),
  );
  await begin(result);
  await act(async () => {
    await vi.advanceTimersByTimeAsync(60000);
  });
  expect(result.current.phase).toBe("idle");
  expect(result.current.error).toContain("vượt giới hạn");
  expect(onTranscript).not.toHaveBeenCalled();
  expect(track.stop).toHaveBeenCalled();
});
it("cancel during transcription aborts the upload and ignores a late response", async () => {
  let respond;
  fetch.mockImplementation(
    () =>
      new Promise((resolve) => {
        respond = resolve;
      }),
  );
  const onTranscript = vi.fn();
  const { result } = renderHook(() =>
    useVoiceInput({ text: "Bản nháp", onTranscript, enabled: true }),
  );
  await begin(result);
  act(() => result.current.stop());
  const signal = fetch.mock.calls[0][1].signal;
  act(() => result.current.cancel());
  expect(signal.aborted).toBe(true);
  await act(async () => {
    respond(transcriptResponse());
  });
  expect(onTranscript).not.toHaveBeenCalled();
});
it("unsupported environments never ask for microphone permission", async () => {
  vi.stubGlobal("MediaRecorder", undefined);
  const { result } = renderHook(() =>
    useVoiceInput({ text: "", onTranscript: vi.fn(), enabled: true }),
  );
  expect(result.current.supported).toBe(false);
  await begin(result);
  expect(getUserMedia).not.toHaveBeenCalled();
});
it("refreshes an expired session before retrying the same audio clip", async () => {
  const requests = [];
  fetch.mockImplementation(async (url, options) => {
    requests.push(url);
    return requests.length === 1
      ? new Response("{}", { status: 401 })
      : url === "/api/browser-session/me"
        ? new Response(JSON.stringify({ data: {} }))
        : transcriptResponse();
  });
  const blob = new Blob(["audio"], { type: "audio/webm" });
  expect(await transcribeVoice(blob, new AbortController().signal)).toContain(
    "14 giờ",
  );
  expect(requests).toEqual([
    "/api/agent/voice/transcribe",
    "/api/browser-session/me",
    "/api/agent/voice/transcribe",
  ]);
  expect(fetch.mock.calls[2][1].body).toBe(blob);
});
it("shows browser interim words immediately, revises them without duplication and restores the original draft on cancel", async () => {
  let recognition;
  class Recognition {
    constructor() {
      recognition = this;
      this.abort = vi.fn();
    }
    start() {}
  }
  vi.stubGlobal("SpeechRecognition", Recognition);
  const onPreview = vi.fn(),
    onTranscript = vi.fn();
  const { result } = renderHook(() =>
    useVoiceInput({ text: "NORA ơi,", onPreview, onTranscript, enabled: true }),
  );
  await begin(result);
  expect(recognition.lang).toBe("vi-VN");
  expect(recognition.interimResults).toBe(true);
  const words = (text) => [
    { 0: { transcript: text }, length: 1, isFinal: false },
  ];
  act(() => recognition.onresult({ results: words("ngày mai tôi họp") }));
  expect(onPreview).toHaveBeenLastCalledWith("NORA ơi, ngày mai tôi họp");
  act(() =>
    recognition.onresult({ results: words("ngày mai tôi họp lúc 14 giờ") }),
  );
  expect(onPreview).toHaveBeenLastCalledWith(
    "NORA ơi, ngày mai tôi họp lúc 14 giờ",
  );
  expect(result.current.previewing).toBe(true);
  expect(onTranscript).not.toHaveBeenCalled();
  expect(fetch).not.toHaveBeenCalled();
  const late = recognition.onresult;
  act(() => result.current.cancel());
  expect(recognition.abort).toHaveBeenCalled();
  expect(onPreview).toHaveBeenLastCalledWith("NORA ơi,");
  act(() => late({ results: words("late words") }));
  expect(onPreview).toHaveBeenLastCalledWith("NORA ơi,");
});
it("Cloud updates the cumulative transcript while still recording when browser speech is unavailable", async () => {
  vi.useFakeTimers();
  const onPreview = vi.fn(),
    onTranscript = vi.fn();
  const { result } = renderHook(() =>
    useVoiceInput({ text: "", onPreview, onTranscript, enabled: true }),
  );
  await begin(result);
  await act(async () => {
    await vi.advanceTimersByTimeAsync(4000);
    recorder.ondataavailable({
      data: new Blob(["first-audio"], { type: recorder.mimeType }),
    });
  });
  expect(onPreview).toHaveBeenCalledWith("Ngày mai họp từ 14 giờ đến 16 giờ.");
  expect(result.current.phase).toBe("recording");
  expect(onTranscript).not.toHaveBeenCalled();
  const firstBytes = fetch.mock.calls[0][1].body.size;
  await act(async () => {
    await vi.advanceTimersByTimeAsync(4000);
    recorder.ondataavailable({
      data: new Blob(["second-audio"], { type: recorder.mimeType }),
    });
  });
  expect(fetch.mock.calls[1][1].body.size).toBeGreaterThan(firstBytes);
  await act(async () => {
    result.current.stop();
  });
  expect(result.current.phase).toBe("idle");
  expect(onTranscript).toHaveBeenCalledTimes(1);
});
it("late Cloud preview cannot overwrite final transcription after stopping", async () => {
  vi.useFakeTimers();
  let finishPreview;
  fetch
    .mockImplementationOnce(
      () =>
        new Promise((resolve) => {
          finishPreview = resolve;
        }),
    )
    .mockImplementation(async () =>
      transcriptResponse("Bản chép lời cuối cùng."),
    );
  const onPreview = vi.fn(),
    onTranscript = vi.fn();
  const { result } = renderHook(() =>
    useVoiceInput({ text: "", onPreview, onTranscript, enabled: true }),
  );
  await begin(result);
  await act(async () => {
    await vi.advanceTimersByTimeAsync(4000);
    recorder.ondataavailable({
      data: new Blob(["audio"], { type: recorder.mimeType }),
    });
  });
  const signal = fetch.mock.calls[0][1].signal;
  await act(async () => {
    result.current.stop();
  });
  expect(signal.aborted).toBe(true);
  await act(async () => finishPreview(transcriptResponse("Kết quả cũ.")));
  expect(onTranscript).toHaveBeenLastCalledWith("Bản chép lời cuối cùng.");
  expect(onPreview).not.toHaveBeenCalled();
});
it("the chat composer blocks Enter during recording and enables review before Enter sends", async () => {
  state.workspace = {
    profile: { id: "voice-member" },
    tasks: [],
    preference: { timeZoneId: "Asia/Ho_Chi_Minh" },
  };
  fetch.mockImplementation(async (url) => {
    if (url === "/api/agent/voice/transcribe") return transcriptResponse();
    if (url.startsWith("/api/agent/conversations"))
      return {
        ok: true,
        json: async () => ({ data: { items: [], nextCursor: null } }),
      };
    return {
      ok: true,
      json: async () => ({
        data:
          url === "/api/agent/capabilities"
            ? { aiAvailable: true, voiceAvailable: true }
            : [],
      }),
    };
  });
  const { unmount } = render(<AgentPage />);
  const mic = await screen.findByRole("button", {
    name: "Nhập bằng giọng nói",
  });
  await waitFor(() => expect(mic.disabled).toBe(false));
  const input = screen.getByLabelText("Yêu cầu của bạn");
  fireEvent.change(input, { target: { value: "NORA ơi," } });
  fireEvent.click(mic);
  await screen.findByRole("button", { name: "Dừng ghi và chuyển thành chữ" });
  expect(input.disabled).toBe(true);
  fireEvent.keyDown(input, { key: "Enter" });
  expect(fetch.mock.calls.some(([url]) => url.endsWith("turns/stream"))).toBe(
    false,
  );
  fireEvent.click(
    screen.getByRole("button", { name: "Dừng ghi và chuyển thành chữ" }),
  );
  await waitFor(() =>
    expect(input.value).toBe("NORA ơi, Ngày mai họp từ 14 giờ đến 16 giờ."),
  );
  expect(input.disabled).toBe(false);
  expect(screen.getByRole("button", { name: "Gửi yêu cầu" }).disabled).toBe(
    false,
  );
  expect(fetch.mock.calls.some(([url]) => url.endsWith("turns/stream"))).toBe(
    false,
  );
  unmount();
});
