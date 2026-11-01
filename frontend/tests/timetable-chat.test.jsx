import { afterEach, beforeEach, expect, it, vi } from "vitest";
import {
  createEvent,
  fireEvent,
  render,
  screen,
  waitFor,
} from "@testing-library/react";
import { AgentPage } from "../src/agent.jsx";
import { readTimetableImage } from "../src/timetable-image-api.js";
import { state } from "../src/ui.jsx";

let imageCalls, turns, sent, failImage, failPlan, imageSignal, pauseImage;
const extraction = {
  text: "Toán: ngày 02/11/2026, 07:00–09:00, chỉ một lần.",
  warnings: [],
  needsReview: false,
};
const response = (data, status = 200) =>
  new Response(
    JSON.stringify(status < 400 ? { data } : { error: { message: data } }),
    {
      status,
      headers: { "Content-Type": "application/json" },
    },
  );
const file = () =>
  new File([new Uint8Array([137, 80, 78, 71])], "thoikhoabieu.png", {
    type: "image/png",
  });
beforeEach(() => {
  sessionStorage.clear();
  localStorage.clear();
  imageCalls = [];
  turns = [];
  sent = [];
  failImage = false;
  failPlan = false;
  pauseImage = false;
  state.workspace = {
    profile: { id: "image-member" },
    tasks: [],
    events: [],
    sessions: [],
    availability: [],
    preference: { timeZoneId: "Asia/Ho_Chi_Minh" },
  };
  vi.stubGlobal(
    "fetch",
    vi.fn(async (url, options = {}) => {
      if (url === "/api/agent/capabilities")
        return response({
          aiAvailable: true,
          imageAvailable: true,
          provider: "Gemini",
        });
      if (url === "/api/agent/turns") return response(turns);
      if (url === "/api/workspace") return response(state.workspace);
      if (url === "/api/agent/images/timetable") {
        imageCalls.push(options);
        imageSignal = options.signal;
        if (pauseImage)
          return new Promise((_, reject) =>
            options.signal.addEventListener("abort", () =>
              reject(new DOMException("Aborted", "AbortError")),
            ),
          );
        return failImage
          ? response("Chưa đọc rõ ảnh", 422)
          : response(extraction);
      }
      if (url === "/api/agent/turns/stream") {
        const body = JSON.parse(options.body);
        if (body.image) {
          imageCalls.push({ ...options, body: body.image });
          imageSignal = options.signal;
          if (pauseImage)
            return new Promise((_, reject) =>
              options.signal.addEventListener("abort", () =>
                reject(new DOMException("Aborted", "AbortError")),
              ),
            );
          if (failImage) return response("Chưa đọc rõ ảnh", 422);
        }
        sent.push(body);
        if (failPlan) return response("AI tạm thời chưa khả dụng", 503);
        const turn = {
          id: "image-turn",
          conversationId: body.conversationId,
          requestId: body.requestId,
          text: body.text,
          status: body.autoApply ? "APPLIED" : "PREVIEW",
          reply: "Đã xếp thời khóa biểu.",
          changes: [],
          warnings: [],
          canUndo: body.autoApply,
          image: body.image
            ? {
                name: body.image.name,
                mimeType: body.image.mimeType,
                url: "/api/agent/turns/aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee/image",
              }
            : null,
        };
        turns = [turn];
        return response(turn);
      }
      return response({});
    }),
  );
});
afterEach(() => vi.unstubAllGlobals());

async function choose() {
  await waitFor(() =>
    expect(screen.queryByText("Đang tải trợ lý…")).toBeNull(),
  );
  fireEvent.change(screen.getByLabelText("Chọn ảnh thời khóa biểu"), {
    target: { files: [file()] },
  });
  await screen.findByAltText("Ảnh thời khóa biểu đã chọn");
}

async function pasteIntoComposer(clipboardData) {
  await waitFor(() =>
    expect(screen.queryByText("Đang tải trợ lý…")).toBeNull(),
  );
  const event = createEvent.paste(screen.getByLabelText("Yêu cầu của bạn"), {
    clipboardData,
  });
  fireEvent(screen.getByLabelText("Yêu cầu của bạn"), event);
  return event;
}

it("pastes a copied image into the composer, preserves typed text and schedules only after Send", async () => {
  render(<AgentPage />);
  await waitFor(() =>
    expect(screen.queryByText("Đang tải trợ lý…")).toBeNull(),
  );
  fireEvent.change(screen.getByLabelText("Yêu cầu của bạn"), {
    target: { value: "Áp dụng trong tuần tới" },
  });
  const image = file();
  const event = await pasteIntoComposer({
    items: [{ kind: "file", type: "image/png", getAsFile: () => image }],
  });
  expect(event.defaultPrevented).toBe(true);
  await screen.findByAltText("Ảnh thời khóa biểu đã chọn");
  expect(screen.getByLabelText("Yêu cầu của bạn").value).toBe(
    "Áp dụng trong tuần tới",
  );
  expect(imageCalls).toHaveLength(0);
  expect(sent).toHaveLength(0);
  fireEvent.click(screen.getByRole("button", { name: "Gửi yêu cầu" }));
  await waitFor(() => expect(sent).toHaveLength(1));
  expect(imageCalls[0].body.name).toBe(image.name);
  expect(imageCalls[0].body.data).toBeTruthy();
  expect(sent[0].text).toContain("Áp dụng trong tuần tới");
  expect(sent[0].text).not.toContain(extraction.text);
  expect(sent[0].image.data).toBeTruthy();
  expect(sent[0].autoApply).toBe(true);
});

it("accepts clipboard images from the files list when clipboard items are unavailable", async () => {
  render(<AgentPage />);
  const event = await pasteIntoComposer({ files: [file()] });
  expect(event.defaultPrevented).toBe(true);
  await screen.findByAltText("Ảnh thời khóa biểu đã chọn");
  expect(imageCalls).toHaveLength(0);
});

it("leaves text and HTML pastes unchanged and does not download copied image URLs", async () => {
  render(<AgentPage />);
  const event = await pasteIntoComposer({
    items: [
      { kind: "string", type: "text/plain" },
      { kind: "string", type: "text/html" },
    ],
    files: [],
    getData: (type) =>
      type === "text/html"
        ? '<img src="https://example.com/timetable.png">'
        : "Lịch học mới",
  });
  expect(event.defaultPrevented).toBe(false);
  expect(screen.queryByAltText("Ảnh thời khóa biểu đã chọn")).toBeNull();
  expect(imageCalls).toHaveLength(0);
  expect(sent).toHaveLength(0);
  expect(
    fetch.mock.calls.some(([url]) =>
      String(url).startsWith("https://example.com"),
    ),
  ).toBe(false);
});

it("rejects oversized pasted images with the same limit as selected files", async () => {
  render(<AgentPage />);
  const large = file();
  Object.defineProperty(large, "size", { value: 5 * 1024 * 1024 + 1 });
  await pasteIntoComposer({ files: [large] });
  expect((await screen.findByRole("alert")).textContent).toContain("5 MB");
  expect(screen.queryByAltText("Ảnh thời khóa biểu đã chọn")).toBeNull();
  expect(imageCalls).toHaveLength(0);
});

it("does not replace a submitted image if another paste arrives while AI is reading", async () => {
  pauseImage = true;
  render(<AgentPage />);
  await choose();
  fireEvent.click(screen.getByRole("button", { name: "Gửi yêu cầu" }));
  await waitFor(() => expect(imageCalls).toHaveLength(1));
  const event = await pasteIntoComposer({
    files: [new File(["different"], "different.png", { type: "image/png" })],
  });
  expect(event.defaultPrevented).toBe(false);
  expect(screen.getByText("thoikhoabieu.png")).toBeTruthy();
  expect(screen.queryByText("different.png")).toBeNull();
  fireEvent.click(screen.getByRole("button", { name: "Dừng" }));
  await waitFor(() => expect(imageSignal.aborted).toBe(true));
});

it("reads and schedules an image-only submission with automatic save in one Send action", async () => {
  const changed = vi.fn();
  render(<AgentPage onWorkspaceChanged={changed} />);
  await choose();
  expect(imageCalls).toHaveLength(0);
  expect(
    screen.queryByRole("button", { name: /Đồng ý và đọc ảnh/ }),
  ).toBeNull();
  fireEvent.click(screen.getByRole("button", { name: "Gửi yêu cầu" }));
  await waitFor(() => expect(sent).toHaveLength(1));
  expect(imageCalls).toHaveLength(1);
  expect(imageCalls[0].body.mimeType).toBe("image/png");
  expect(sent[0].autoApply).toBe(true);
  expect(sent[0].text).toBe("Nhập thời khóa biểu trong ảnh, tự phân tích chi tiết lịch bận và các khoảng rảnh từng ngày.");
  expect(sent[0].image.data).toBeTruthy();
  expect(sent[0].text).not.toContain("Chỉ xem trước");
  await waitFor(() => expect(changed).toHaveBeenCalledTimes(1));
  expect(screen.queryByAltText("Ảnh thời khóa biểu đã chọn")).toBeNull();
  expect(
    screen.getByRole("button", { name: "Xem ảnh thời khóa biểu" }),
  ).toBeTruthy();
  fireEvent.click(
    screen.getByRole("button", { name: "Xem ảnh thời khóa biểu" }),
  );
  expect(
    screen.getByRole("dialog", { name: "Ảnh thời khóa biểu phóng to" }).open,
  ).toBe(true);
  fireEvent.click(screen.getByRole("button", { name: "Đóng ảnh" }));
});

it("respects an explicitly disabled automatic-save switch and includes the accompanying date instructions", async () => {
  render(<AgentPage />);
  await choose();
  fireEvent.click(screen.getByRole("switch", { name: "Tự động lưu lịch" }));
  fireEvent.change(screen.getByLabelText("Yêu cầu của bạn"), {
    target: { value: "Áp dụng từ 02/11/2026 trong 2 tuần" },
  });
  fireEvent.click(screen.getByRole("button", { name: "Gửi yêu cầu" }));
  await waitFor(() => expect(sent).toHaveLength(1));
  expect(sent[0].autoApply).toBe(false);
  expect(sent[0].text).toContain("Áp dụng từ 02/11/2026 trong 2 tuần");
});

it("retains the image after an unreadable response and never sends a planning command", async () => {
  failImage = true;
  render(<AgentPage />);
  await choose();
  fireEvent.click(screen.getByRole("button", { name: "Gửi yêu cầu" }));
  await waitFor(() =>
    expect(screen.getAllByText("Chưa đọc rõ ảnh").length).toBeGreaterThan(0),
  );
  expect(sent).toHaveLength(0);
  expect(screen.getByAltText("Ảnh thời khóa biểu đã chọn")).toBeTruthy();
  failImage = false;
  fireEvent.click(screen.getByRole("button", { name: "Gửi yêu cầu" }));
  await waitFor(() => expect(sent).toHaveLength(1));
  expect(imageCalls).toHaveLength(2);
});

it("retries direct visual planning with the same request id and image", async () => {
  failPlan = true;
  render(<AgentPage />);
  await choose();
  fireEvent.click(screen.getByRole("button", { name: "Gửi yêu cầu" }));
  await screen.findByText("AI tạm thời chưa khả dụng");
  await waitFor(() =>
    expect(screen.getByRole("button", { name: "Gửi yêu cầu" }).disabled).toBe(
      false,
    ),
  );
  failPlan = false;
  fireEvent.click(screen.getByRole("button", { name: "Gửi yêu cầu" }));
  await waitFor(() => expect(sent).toHaveLength(2));
  expect(imageCalls).toHaveLength(2);
  expect(sent[1].requestId).toBe(sent[0].requestId);
  expect(sent[1].image).toEqual(sent[0].image);
});

it("stops an in-progress image read without issuing a calendar mutation", async () => {
  pauseImage = true;
  render(<AgentPage />);
  await choose();
  fireEvent.click(screen.getByRole("button", { name: "Gửi yêu cầu" }));
  await waitFor(() => expect(imageCalls).toHaveLength(1));
  fireEvent.click(screen.getByRole("button", { name: "Dừng" }));
  await waitFor(() => expect(imageSignal.aborted).toBe(true));
  expect(sent).toHaveLength(0);
  expect(screen.getByAltText("Ảnh thời khóa biểu đã chọn")).toBeTruthy();
});

it("refreshes browser authentication once before retrying the same image upload", async () => {
  let uploaded = 0;
  vi.stubGlobal(
    "fetch",
    vi.fn(async (url) => {
      if (url === "/api/agent/images/timetable")
        return ++uploaded === 1
          ? response("expired", 401)
          : response(extraction);
      return response({});
    }),
  );
  const result = await readTimetableImage(file(), new AbortController().signal);
  expect(result.text).toBe(extraction.text);
  expect(uploaded).toBe(2);
  expect(
    fetch.mock.calls.some(([url]) => url === "/api/browser-session/me"),
  ).toBe(true);
});
