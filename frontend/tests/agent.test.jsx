import { beforeEach, expect, it, vi } from "vitest";
import {
  fireEvent,
  render,
  screen,
  waitFor,
  within,
} from "@testing-library/react";
import { AgentPage } from "../src/agent.jsx";
import { state } from "../src/ui.jsx";

let calls, turns, available, failSend, result;
beforeEach(() => {
  sessionStorage.clear();
  localStorage.clear();
  calls = [];
  turns = [];
  available = true;
  failSend = false;
  state.workspace = {
    profile: { id: "member" },
    tasks: [],
    availability: [],
    events: [],
    preference: {
      timeZoneId: "Asia/Ho_Chi_Minh",
      maximumDailyMinutes: 480,
      maximumSessionMinutes: 120,
    },
  };
  result = {
    id: "turn",
    conversationId: "conversation",
    requestId: "request",
    text: "Xếp báo cáo",
    status: "APPLIED",
    reply: "Đã lưu yêu cầu.",
    question: "",
    canUndo: true,
    changes: [
      {
        kind: "TASK",
        taskId: "task",
        name: "Báo cáo SWP",
        before: "",
        after: "120 phút",
      },
    ],
    warnings: [],
    plan: {
      sessions: [
        {
          id: "session",
          taskId: "task",
          start: "2026-10-15T07:00:00Z",
          end: "2026-10-15T09:00:00Z",
          preserved: false,
        },
      ],
    },
  };
  vi.stubGlobal(
    "fetch",
    vi.fn(async (url, options = {}) => {
      const body = options.body ? JSON.parse(options.body) : undefined;
      calls.push({ url, method: options.method, body });
      if (url === "/api/workspace")
        return {
          ok: true,
          json: async () => ({
            data: {
              ...state.workspace,
              events: state.workspace.events || [],
              sessions: state.workspace.sessions || [],
            },
          }),
        };
      if (url === "/api/agent/usage")
        return {
          ok: true,
          json: async () => ({
            data: {
              dailyLimit: 200,
              used: 0,
              active: 0,
              concurrentLimit: 2,
              resetsAt: "2026-10-16T00:00:00Z",
              inputTokens: null,
              outputTokens: null,
              unknownUsageCalls: 0,
            },
          }),
        };
      if (url.startsWith("/api/agent/conversations")) {
        const request = new URL(url, "https://localhost");
        const match = request.pathname.match(/conversations\/([^/]+)\/turns/);
        if (match)
          return {
            ok: true,
            json: async () => ({
              data: {
                items: turns.filter((turn) => turn.conversationId === match[1]),
                nextCursor: null,
              },
            }),
          };
        const groups = [
          ...new Set(turns.map((turn) => turn.conversationId)),
        ].map((id) => ({
          id,
          title: turns.find((turn) => turn.conversationId === id).text,
          turnCount: turns.filter((turn) => turn.conversationId === id).length,
          archived: false,
          version: "version",
        }));
        const search = (request.searchParams.get("search") || "").toLowerCase();
        return {
          ok: true,
          json: async () => ({
            data: {
              items: groups.filter((row) =>
                row.title.toLowerCase().includes(search),
              ),
              nextCursor: null,
            },
          }),
        };
      }
      if (url === "/api/agent/capabilities")
        return {
          ok: true,
          json: async () => ({
            data: {
              aiAvailable: available,
              features: [
                "conversation-lifecycle",
                "usage-ledger",
                "planning-policy",
              ],
            },
          }),
        };
      if (url === "/api/agent/turns" && options.method === "GET")
        return { ok: true, json: async () => ({ data: turns }) };
      if (url === "/api/agent/turns" || url === "/api/agent/turns/stream") {
        if (failSend)
          return {
            ok: false,
            status: 503,
            json: async () => ({
              error: { message: "AI tạm thời chưa khả dụng." },
            }),
          };
        result = {
          ...result,
          conversationId: body.conversationId,
          requestId: body.requestId,
          text: body.text,
        };
      }
      if (url.endsWith("/apply"))
        result = { ...result, status: "APPLIED", canUndo: true };
      if (url.endsWith("/undo"))
        result = {
          ...result,
          status: "UNDONE",
          canUndo: false,
          reply: "Đã hoàn tác yêu cầu.",
        };
      turns = [result];
      return { ok: true, json: async () => ({ data: result }) };
    }),
  );
});
async function ready() {
  await waitFor(() =>
    expect(screen.queryByText("Đang tải trợ lý…")).toBeNull(),
  );
  fireEvent.change(screen.getByLabelText("Yêu cầu của bạn"), {
    target: { value: "Xếp báo cáo" },
  });
}

it("renders detailed schedule advice as readable paragraphs and sections without apply actions", async () => {
  result.status = "ANSWERED"; result.canUndo = false; result.changes = []; result.plan = null;
  result.reply = "Lịch của bạn có nhiều khoảng nghỉ ngắn. Mình gợi ý giữ khoảng dài nhất cho báo cáo gần hạn.\n\n**Thời gian còn lại và điểm cần chú ý**\n- Còn 110 phút trống, gồm bốn khoảng ngắn và một khoảng dài.\n\n**Gợi ý có thể cân nhắc**\n- Báo cáo: 17:50–18:50, đủ thời lượng và đã kiểm tra ràng buộc.\n\nLịch hiện tại chưa thay đổi.";
  render(<AgentPage />);
  await ready();
  fireEvent.click(screen.getByRole("button", { name: "Gửi yêu cầu" }));
  await screen.findByText(/Lịch của bạn có nhiều khoảng nghỉ ngắn/);
  expect(screen.getByText("Thời gian còn lại và điểm cần chú ý").tagName).toBe("STRONG");
  expect(screen.getByText(/Báo cáo: 17:50–18:50/).closest("li")).toBeTruthy();
  expect(screen.queryByRole("button", { name: /Lưu thay đổi|Xác nhận dời lịch/ })).toBeNull();
  expect(calls.some((call) => call.url.endsWith("/apply"))).toBe(false);
});

it("keeps an urgent displacement pending with automatic save enabled until the user confirms", async () => {
  result.status = "PREVIEW";
  result.canUndo = false;
  result.plan.hasUrgentReplan = true;
  result.changes.push({ kind: "SESSION", name: "Bài tập cũ", taskId: "old",
    before: "22/10/2026 19:00 → 22/10/2026 20:00",
    after: "23/10/2026 19:00 → 23/10/2026 20:00" });
  const changed = vi.fn();
  render(<AgentPage onWorkspaceChanged={changed} />);
  await ready();
  fireEvent.click(screen.getByRole("button", { name: "Gửi yêu cầu" }));
  const confirm = await screen.findByRole("button", { name: "Xác nhận dời lịch" });
  expect(calls.find((call) => call.url === "/api/agent/turns/stream").body.autoApply).toBe(true);
  expect(calls.some((call) => call.url.endsWith("/apply"))).toBe(false);
  expect(changed).not.toHaveBeenCalled();
  const card = screen.getByRole("region", { name: "Kết quả yêu cầu" });
  expect(within(card).getByText(/Giờ cũ:/).closest("details")).toBeNull();
  expect(within(card).getByText(/Giờ mới:/)).toBeTruthy();
  fireEvent.click(confirm);
  await screen.findAllByText("Đã lưu lịch");
  expect(calls.filter((call) => call.url.endsWith("/apply"))).toHaveLength(1);
  expect(changed).toHaveBeenCalledOnce();
});

it("shows deadline protection as a plan that needs confirmation even when automatic save is enabled", async () => {
  result.status = "PREVIEW";
  result.canUndo = false;
  result.plan.hasDeadlineProtection = true;
  const changed = vi.fn();
  render(<AgentPage onWorkspaceChanged={changed} />);
  await ready();
  fireEvent.click(screen.getByRole("button", { name: "Gửi yêu cầu" }));
  const confirm = await screen.findByRole("button", { name: "Xác nhận kế hoạch" });
  expect(calls.some((call) => call.url.endsWith("/apply"))).toBe(false);
  expect(changed).not.toHaveBeenCalled();
  fireEvent.click(confirm);
  await screen.findAllByText("Đã lưu lịch");
  expect(calls.filter((call) => call.url.endsWith("/apply"))).toHaveLength(1);
});

it("shows the old and proposed shopping hours beside an urgent meeting before confirming the move", async () => {
  result.status = "PREVIEW";
  result.canUndo = false;
  result.plan.hasUrgentReplan = true;
  result.plan.sessions = [];
  result.changes = [
    { kind: "EVENT", name: "Cuộc họp khẩn", before: "", after: "22/10/2026 15:00 → 22/10/2026 17:00" },
    { kind: "EVENT", name: "Đi mua sắm", before: "22/10/2026 15:00 → 22/10/2026 16:00", after: "22/10/2026 17:00 → 22/10/2026 18:00" },
  ];
  render(<AgentPage />);
  await ready();
  fireEvent.click(screen.getByRole("button", { name: "Gửi yêu cầu" }));
  const confirm = await screen.findByRole("button", { name: "Xác nhận dời lịch" });
  const shopping = screen.getByText("Đi mua sắm").closest("li");
  expect(within(shopping).getByText(/Giờ cũ:/).closest("details")).toBeNull();
  expect(within(shopping).getByText(/Giờ mới:/)).toBeTruthy();
  expect(shopping.textContent).toContain("15:00");
  expect(shopping.textContent).toContain("18:00");
  expect(calls.some((call) => call.url.endsWith("/apply"))).toBe(false);
  fireEvent.click(confirm);
  await screen.findAllByText("Đã lưu lịch");
  expect(calls.filter((call) => call.url.endsWith("/apply"))).toHaveLength(1);
});

it("names the proposed work shift move and explains that changing a shift needs permission", async () => {
  result.status = "PREVIEW";
  result.canUndo = false;
  result.reply = "Giữ Họp khẩn 16:00–18:00; đề xuất dời Đi làm để tránh trùng giờ.";
  result.plan.hasUrgentReplan = true;
  result.plan.sessions = [];
  result.changes = [
    { kind: "EVENT", name: "Họp khẩn", before: "", after: "24/10/2026 16:00 → 24/10/2026 18:00" },
    { kind: "EVENT", name: "Đi làm", before: "24/10/2026 15:00 → 24/10/2026 18:00", after: "24/10/2026 18:00 → 24/10/2026 21:00",
      reason: "Đây là ca làm. Chỉ xác nhận phương án nếu bạn được phép đổi ca." },
  ];
  render(<AgentPage />);
  await ready();
  fireEvent.click(screen.getByRole("button", { name: "Gửi yêu cầu" }));
  const confirm = await screen.findByRole("button", { name: "Xác nhận dời lịch" });
  const move = screen.getByText("Đi làm").closest("li");
  expect(within(move).getByText(/Giờ cũ:/).textContent).toContain("15:00");
  expect(within(move).getByText(/Giờ mới:/).textContent).toContain("21:00");
  expect(within(move).getByText(/Lý do và điều kiện:/).textContent).toContain("được phép đổi ca");
  expect(calls.some((call) => call.url.endsWith("/apply"))).toBe(false);
  fireEvent.click(confirm);
  await waitFor(() => expect(calls.filter((call) => call.url.endsWith("/apply"))).toHaveLength(1));
});

it("offers clickable time alternatives on an existing rejected turn and sends the chosen date in the same conversation", async () => {
  const original = "lên lịch mai tôi đi học vào lúc 18h đến 19h";
  turns = [
    {
      ...result,
      status: "FAILED",
      changes: [],
      plan: null,
      canUndo: false,
      text: original,
      errorCode: "BUSY_TIME_CONFLICT",
      reply:
        "Sự kiện nằm trong giờ đã đánh dấu bận. Chưa lưu thay đổi. Đề xuất giữ nguyên thời lượng: 22/10/2026 21:00–22:00; 23/10/2026 19:00–20:00. Bạn chọn một khung giờ để thử lại.",
    },
  ];
  render(<AgentPage />);
  await ready();
  const chip = await screen.findByRole("button", {
    name: "23/10/2026 · 19:00–20:00",
  });
  expect(calls.filter((call) => call.method === "POST")).toHaveLength(0);
  fireEvent.click(chip);
  await waitFor(() =>
    expect(calls.some((call) => call.url === "/api/agent/turns/stream")).toBe(
      true,
    ),
  );
  const request = calls.find((call) => call.url === "/api/agent/turns/stream");
  expect(request.body.conversationId).toBe("conversation");
  expect(request.body.text).toContain(
    "Tôi chọn từ 19:00 đến 20:00 ngày 23/10/2026.",
  );
  expect(request.body.text).toContain(original);
  expect(
    calls.some(
      (call) => call.url === "/api/time/events" && call.method === "POST",
    ),
  ).toBe(false);
});

it("submits a custom time prompt in the same conversation without directly saving an event", async () => {
  const original = "Ngày mai tôi đi học từ 20h đến 21h";
  turns = [{ ...result, status: "FAILED", changes: [], plan: null, canUndo: false,
    text: original, errorCode: "BUSY_TIME_CONFLICT",
    reply: "Sự kiện nằm trong giờ bận. Đề xuất giữ nguyên thời lượng: 26/10/2026 19:00–20:00. Bạn chọn một khung giờ để thử lại." }];
  render(<AgentPage />);
  await ready();
  const form = (await screen.findByRole("group", { name: "Hoặc nhập thời gian khác" })).closest("form");
  fireEvent.change(within(form).getByRole("textbox", { name: "Thời gian khác" }), { target: { value: "Mai 22h, giữ nguyên thời lượng giúp tôi" } });
  expect(calls.some((call) => call.url === "/api/agent/turns/stream")).toBe(false);
  fireEvent.submit(form);
  await waitFor(() => expect(calls.some((call) => call.url === "/api/agent/turns/stream")).toBe(true));
  const request = calls.find((call) => call.url === "/api/agent/turns/stream");
  expect(request.body.conversationId).toBe("conversation");
  expect(request.body.text).toContain("Mai 22h, giữ nguyên thời lượng giúp tôi");
  expect(request.body.text).toContain(original);
  expect(calls.some((call) => call.url === "/api/time/events" && call.method === "POST")).toBe(false);
});

it.each([
  ["hội thoại", "ArrowRight", "Thu gọn lịch sử", "Mở lịch sử", 260],
  ["lịch", "ArrowLeft", "Đóng bảng lịch", "Lịch của bạn", 320],
])(
  "resizes the %s panel, preserves its width after collapse and reload, without sizing menus",
  async (panel, key, close, reopen, initial) => {
    vi.spyOn(window, "matchMedia").mockImplementation((query) => ({
      matches: query === "(min-width: 1081px)",
      addEventListener() {},
      removeEventListener() {},
    }));
    vi.spyOn(window, "innerWidth", "get").mockReturnValue(1440);
    turns = [{ ...result }];
    let view = render(<AgentPage />);
    await ready();
    const separator = () =>
      screen.getByRole("separator", { name: `Đổi chiều rộng bảng ${panel}` });
    expect(separator().getAttribute("aria-valuenow")).toBe(String(initial));
    fireEvent.keyDown(separator(), { key });
    expect(separator().getAttribute("aria-valuenow")).toBe(
      String(initial + 20),
    );
    fireEvent.click(screen.getByRole("button", { name: close, exact: true }));
    expect(
      screen.queryByRole("separator", { name: `Đổi chiều rộng bảng ${panel}` }),
    ).toBeNull();
    fireEvent.click(screen.getByRole("button", { name: reopen, exact: true }));
    expect(separator().getAttribute("aria-valuenow")).toBe(
      String(initial + 20),
    );
    expect(screen.queryByLabelText(`Kích thước bảng ${panel}`)).toBeNull();
    view.unmount();
    view = render(<AgentPage />);
    await ready();
    expect(separator().getAttribute("aria-valuenow")).toBe(
      String(initial + 20),
    );
    fireEvent.keyDown(separator(), { key: "End" });
    const maximum = Number(separator().getAttribute("aria-valuemax"));
    expect(Number(separator().getAttribute("aria-valuenow"))).toBe(maximum);
    fireEvent.keyDown(separator(), { key });
    expect(Number(separator().getAttribute("aria-valuenow"))).toBe(maximum);
  },
);

it("keeps the calendar collapsed when a new saved response arrives", async () => {
  vi.spyOn(window, "matchMedia").mockImplementation((query) => ({
    matches: query === "(min-width: 1081px)",
    addEventListener() {},
    removeEventListener() {},
  }));
  render(<AgentPage />);
  await ready();
  fireEvent.click(screen.getByRole("button", { name: "Đóng bảng lịch" }));
  fireEvent.submit(screen.getByRole("form", { name: "Gửi yêu cầu cho NORA" }));
  await screen.findByRole("region", { name: "Kết quả yêu cầu" });
  expect(
    screen
      .getByRole("button", { name: "Lịch của bạn" })
      .getAttribute("aria-expanded"),
  ).toBe("false");
});

it("keeps useful explanation and expandable verified details after saving and reloading history", async () => {
  result.reply = "Mình ưu tiên hồ sơ trước để bạn còn thời gian kiểm tra.";
  result.outcome = "Đã lưu lịch: Xử lý hồ sơ [WORK], 20/10/2026 12:00–12:45.";
  const first = render(<AgentPage />);
  await ready();
  fireEvent.click(screen.getByRole("button", { name: "Gửi yêu cầu" }));
  await screen.findByText(result.reply);
  fireEvent.click(screen.getByText("Giải thích và chi tiết"));
  const verified = await screen.findByText(
    "Đã lưu lịch: Xử lý hồ sơ · Công việc, 20/10/2026 12:00–12:45.",
  );
  expect(verified.closest("details").open).toBe(true);
  expect(screen.getByText(result.reply)).toBeTruthy();
  first.unmount();
  render(<AgentPage />);
  await screen.findByText(result.reply);
  fireEvent.click(screen.getByText("Giải thích và chi tiết"));
  const reloaded = await screen.findByText(
    "Đã lưu lịch: Xử lý hồ sơ · Công việc, 20/10/2026 12:00–12:45.",
  );
  expect(reloaded.closest("details").open).toBe(true);
  expect(screen.getByText(result.reply)).toBeTruthy();
});

it("recovers a saved request from history after the stream connection fails", async () => {
  const fallback = globalThis.fetch;
  vi.stubGlobal(
    "fetch",
    vi.fn(async (url, options = {}) => {
      if (url === "/api/agent/turns/stream") {
        const body = JSON.parse(options.body);
        turns = [
          {
            ...result,
            requestId: body.requestId,
            conversationId: body.conversationId,
          },
        ];
        throw new TypeError("Failed to fetch");
      }
      return fallback(url, options);
    }),
  );
  render(<AgentPage />);
  await ready();
  fireEvent.click(screen.getByRole("button", { name: "Gửi yêu cầu" }));
  await screen.findByText(result.reply);
  expect(screen.queryByRole("alert")).toBeNull();
  expect(screen.queryByText("Kết nối bị gián đoạn")).toBeNull();
  expect(screen.getAllByText("Đã lưu lịch").length).toBeGreaterThan(0);
});

it.each([true, false])(
  "replaces an optimistic streamed draft after a server overlap rejection (history available: %s)",
  async (historyAvailable) => {
    const fallback = globalThis.fetch;
    let rejected = false;
    const conflict =
      "Cuộc họp trùng Đi làm 13:30–17:00. Chưa lưu thay đổi; hãy chọn giờ khác.";
    vi.stubGlobal(
      "fetch",
      vi.fn(async (url, options = {}) => {
        if (url === "/api/agent/turns/stream") {
          const body = JSON.parse(options.body);
          rejected = true;
          turns = [
            {
              ...result,
              conversationId: body.conversationId,
              requestId: body.requestId,
              text: body.text,
              status: "FAILED",
              reply: conflict,
              errorCode: "FIXED_EVENT_OVERLAP",
              canUndo: false,
              changes: [],
              plan: null,
            },
          ];
          const encoder = new TextEncoder();
          return new Response(
            new ReadableStream({
              start(controller) {
                controller.enqueue(
                  encoder.encode(
                    `data: ${JSON.stringify({ type: "delta", delta: "Lịch trình sẽ được cập nhật." })}\n\n`,
                  ),
                );
                controller.enqueue(
                  encoder.encode(
                    `data: ${JSON.stringify({ type: "error", code: "FIXED_EVENT_OVERLAP", message: conflict })}\n\n`,
                  ),
                );
                controller.close();
              },
            }),
            { headers: { "content-type": "text/event-stream" } },
          );
        }
        if (url === "/api/agent/turns" && rejected && !historyAvailable)
          throw new Error("history unavailable");
        return fallback(url, options);
      }),
    );
    render(<AgentPage />);
    await ready();
    fireEvent.click(screen.getByRole("button", { name: "Gửi yêu cầu" }));
    await screen.findByText(conflict);
    await waitFor(() =>
      expect(screen.queryByText("Lịch trình sẽ được cập nhật.")).toBeNull(),
    );
    expect(screen.queryByText("Kết nối bị gián đoạn")).toBeNull();
    expect(
      screen.getAllByText(/Chưa lưu|chưa được lưu/).length,
    ).toBeGreaterThan(0);
    expect(screen.queryByText("Đã lưu lịch")).toBeNull();
    await waitFor(() => expect(screen.getAllByText(conflict)).toHaveLength(1));
    if (historyAvailable) expect(screen.queryByRole("alert")).toBeNull();
    else expect(screen.getByRole("alert").textContent).toContain(conflict);
  },
);
it("sends a clear request, shows actual schedule in the account timezone and permits undo", async () => {
  const changed = vi.fn(async () => {});
  render(<AgentPage onWorkspaceChanged={changed} />);
  await ready();
  fireEvent.click(screen.getByRole("button", { name: "Gửi yêu cầu" }));
  await screen.findAllByText("Đã lưu lịch");
  const conversation = screen.getByLabelText("Lịch sử hội thoại");
  expect(within(conversation).getByText(/14:00.*16:00/)).toBeTruthy();
  expect(changed).toHaveBeenCalledOnce();
  expect(calls.find((call) => call.method === "POST").body.autoApply).toBe(
    true,
  );
  fireEvent.click(screen.getByRole("button", { name: "Hoàn tác" }));
  await screen.findByText("Đã hoàn tác yêu cầu.");
  expect(changed).toHaveBeenCalledTimes(2);
});
it("keeps save and remaining length in the box with help and keyboard hints below", async () => {
  render(<AgentPage />);
  await ready();
  const composer = screen.getByRole("form", { name: "Gửi yêu cầu cho NORA" });
  const save = screen.getByRole("switch", { name: "Tự động lưu lịch" });
  expect(composer.contains(save)).toBe(true);
  expect(screen.queryByLabelText("Tùy chọn gửi yêu cầu")).toBeNull();
  const count = document.getElementById("nora-request-count");
  expect(composer.contains(count)).toBe(true);
  const request = screen.getByLabelText("Yêu cầu của bạn");
  fireEvent.change(request, { target: { value: "abc" } });
  expect(count.textContent).toContain("1997");
  expect(count.hidden).toBe(true);
  fireEvent.change(request, { target: { value: "a".repeat(1500) } });
  expect(count.hidden).toBe(true);
  fireEvent.change(request, { target: { value: "a".repeat(1501) } });
  expect(count.hidden).toBe(false);
  expect(count.textContent).toContain("499");
  fireEvent.change(request, { target: { value: "" } });
  expect(count.hidden).toBe(true);
  const hint = screen.getByText("Enter gửi · Shift + Enter xuống dòng");
  expect(composer.contains(hint)).toBe(false);
  const help = screen.getByLabelText("Trợ giúp NORA");
  expect(composer.contains(help)).toBe(false);
  fireEvent.click(help);
  expect(screen.getByText(/NORA lưu khi lịch hợp lệ/)).toBeTruthy();
  fireEvent.click(screen.getByText(/NORA lưu khi lịch hợp lệ/));
  expect(help.closest("details").open).toBe(true);
  fireEvent.click(request);
  expect(help.closest("details").open).toBe(false);
  fireEvent.click(help);
  expect(
    screen.queryByText(
      "Dùng thời lượng cá nhân hóa và chừa thời gian dự phòng",
    ),
  ).toBeNull();
  fireEvent.keyDown(help, { key: "Escape" });
  expect(help.closest("details").hasAttribute("open")).toBe(false);
});
it("reserves the floating composer height as it grows without shortening the transcript viewport", async () => {
  let resized;
  let paint;
  vi.stubGlobal(
    "requestAnimationFrame",
    vi.fn((callback) => {
      paint = callback;
      return 1;
    }),
  );
  vi.stubGlobal("cancelAnimationFrame", vi.fn());
  const observe = vi.fn();
  vi.stubGlobal(
    "ResizeObserver",
    class {
      constructor(callback) {
        resized = callback;
      }
      observe = observe;
      disconnect() {}
    },
  );
  render(<AgentPage />);
  await ready();
  const composer = screen.getByRole("form", { name: "Gửi yêu cầu cho NORA" });
  const dock = composer.closest(".nora-composer-dock");
  expect(dock).not.toBeNull();
  expect(observe).toHaveBeenCalledWith(dock);
  vi.spyOn(dock, "getBoundingClientRect").mockReturnValue({ height: 200 });
  resized();
  const main = screen.getByLabelText("Trò chuyện với NORA");
  expect(main.style.getPropertyValue("--nora-composer-stack")).not.toBe(
    "200px",
  );
  paint();
  expect(main.style.getPropertyValue("--nora-composer-stack")).toBe("200px");
  vi.spyOn(dock, "getBoundingClientRect").mockReturnValue({ height: 260 });
  resized();
  paint();
  expect(main.style.getPropertyValue("--nora-composer-stack")).toBe("260px");
  vi.unstubAllGlobals();
});
it("preview mode leaves the workspace unchanged until explicit apply", async () => {
  result.status = "PREVIEW";
  result.canUndo = false;
  const changed = vi.fn(async () => {});
  render(<AgentPage onWorkspaceChanged={changed} />);
  await ready();
  fireEvent.click(screen.getByLabelText("Tự động lưu lịch"));
  fireEvent.click(screen.getByRole("button", { name: "Gửi yêu cầu" }));
  await screen.findAllByText("Chờ áp dụng");
  expect(changed).not.toHaveBeenCalled();
  expect(calls.find((call) => call.method === "POST").body.autoApply).toBe(
    false,
  );
  fireEvent.click(screen.getByRole("button", { name: "Lưu thay đổi" }));
  await screen.findAllByText("Đã lưu lịch");
  expect(changed).toHaveBeenCalledOnce();
});
it("retry preserves the text and idempotency key after a transport failure", async () => {
  failSend = true;
  render(<AgentPage />);
  await ready();
  fireEvent.click(screen.getByRole("button", { name: "Gửi yêu cầu" }));
  await screen.findByRole("alert");
  expect(screen.getByLabelText("Yêu cầu của bạn").value).toBe("Xếp báo cáo");
  const first = calls.find((call) => call.method === "POST").body;
  failSend = false;
  fireEvent.click(screen.getByRole("button", { name: "Gửi yêu cầu" }));
  await screen.findAllByText("Đã lưu lịch");
  expect(calls.filter((call) => call.method === "POST")[1].body.requestId).toBe(
    first.requestId,
  );
});
it("shows missing AI configuration and blocks sending without hiding history", async () => {
  available = false;
  turns = [
    {
      ...result,
      status: "ANSWERED",
      reply: "Lịch trước đây",
      plan: null,
      changes: [],
      canUndo: false,
    },
  ];
  render(<AgentPage />);
  await ready();
  expect(screen.getByText("Lịch trước đây")).toBeTruthy();
  expect(screen.getByText(/Trợ lý AI chưa được cấu hình/)).toBeTruthy();
  expect(screen.getByRole("button", { name: "Gửi yêu cầu" }).disabled).toBe(
    true,
  );
});
it("NORA prompt suggestions populate and focus the composer without sending or changing the calendar", async () => {
  render(<AgentPage />);
  await waitFor(() =>
    expect(screen.queryByText("Đang tải trợ lý…")).toBeNull(),
  );
  expect(
    screen.getByRole("img", { name: "NORA, linh vật cú xanh ôm cuốn lịch" }),
  ).toBeTruthy();
  fireEvent.click(
    screen.getByRole("button", { name: /Hoàn thành trước deadline/ }),
  );
  const field = screen.getByLabelText("Yêu cầu của bạn");
  expect(field.value).toContain("Báo cáo SWP");
  expect(document.activeElement).toBe(field);
  expect(calls.filter((call) => call.method === "POST")).toHaveLength(0);
});
it("Enter sends while Shift Enter and composing input do not submit", async () => {
  render(<AgentPage />);
  await ready();
  const field = screen.getByLabelText("Yêu cầu của bạn");
  fireEvent.keyDown(field, { key: "Enter", shiftKey: true });
  fireEvent.keyDown(field, { key: "Enter", isComposing: true });
  fireEvent.keyDown(field, { key: "Enter", keyCode: 229 });
  expect(calls.filter((call) => call.method === "POST")).toHaveLength(0);
  fireEvent.keyDown(field, { key: "Enter" });
  await screen.findAllByText("Đã lưu lịch");
  expect(calls.filter((call) => call.method === "POST")).toHaveLength(1);
});
it("NORA motion can be paused and the preference survives a new visit", async () => {
  const first = render(<AgentPage />);
  await ready();
  fireEvent.click(
    screen.getByRole("button", { name: "Tạm dừng chuyển động của NORA" }),
  );
  expect(
    first.container.querySelector(".nora-studio").dataset.motionState,
  ).toBe("paused");
  expect(localStorage.getItem("planora.nora.motion-paused")).toBe("true");
  first.unmount();
  const second = render(<AgentPage />);
  expect(
    second.container.querySelector(".nora-studio").dataset.motionState,
  ).toBe("paused");
  fireEvent.click(
    screen.getByRole("button", { name: "Bật chuyển động của NORA" }),
  );
  expect(
    second.container.querySelector(".nora-studio").dataset.motionState,
  ).toBe("running");
  await waitFor(() =>
    expect(screen.queryByText("Đang tải trợ lý…")).toBeNull(),
  );
});
it("shows a real partial reply and walking state, and Stop preserves the idempotency key for retry", async () => {
  const fallback = globalThis.fetch;
  let streamController, firstBody;
  let first = true;
  vi.stubGlobal(
    "fetch",
    vi.fn(async (url, options = {}) => {
      if (url === "/api/agent/turns/stream" && first) {
        first = false;
        firstBody = JSON.parse(options.body);
        calls.push({ url, method: options.method, body: firstBody });
        return new Response(
          new ReadableStream({
            start(controller) {
              streamController = controller;
              options.signal.addEventListener(
                "abort",
                () =>
                  controller.error(new DOMException("Stopped", "AbortError")),
                { once: true },
              );
            },
          }),
          { headers: { "content-type": "text/event-stream" } },
        );
      }
      return fallback(url, options);
    }),
  );
  const view = render(<AgentPage />);
  await ready();
  fireEvent.click(screen.getByRole("button", { name: "Gửi yêu cầu" }));
  await waitFor(() => expect(streamController).toBeTruthy());
  const encoder = new TextEncoder();
  streamController.enqueue(
    encoder.encode(
      `data: ${JSON.stringify({ type: "phase", stage: "UNDERSTANDING", message: "Đang hiểu yêu cầu" })}\n\ndata: ${JSON.stringify({ type: "delta", delta: "Mình đang tìm giờ rảnh." })}\n\n`,
    ),
  );
  await screen.findByText("Mình đang tìm giờ rảnh.");
  expect(
    view.container.querySelector(".nora-walk-track.is-active"),
  ).toBeTruthy();
  expect(screen.queryAllByText("Đã lưu lịch")).toHaveLength(0);
  fireEvent.click(screen.getByRole("button", { name: "Dừng", exact: true }));
  await screen.findByRole("alert");
  expect(view.container.querySelector(".nora-walk-track.is-active")).toBeNull();
  expect(screen.getByLabelText("Yêu cầu của bạn").value).toBe("Xếp báo cáo");
  fireEvent.click(screen.getByRole("button", { name: "Gửi yêu cầu" }));
  await screen.findAllByText("Đã lưu lịch");
  expect(calls.filter((call) => call.method === "POST")[1].body.requestId).toBe(
    firstBody.requestId,
  );
});
it("history search filters real conversations and selecting one preserves its existing messages", async () => {
  turns = [
    {
      ...result,
      id: "first",
      conversationId: "one",
      text: "Viết báo cáo",
      reply: "Lịch báo cáo",
      status: "ANSWERED",
      plan: null,
      changes: [],
    },
    {
      ...result,
      id: "second",
      conversationId: "two",
      text: "Ôn chương 4",
      reply: "Lịch ôn tập",
      status: "ANSWERED",
      plan: null,
      changes: [],
    },
  ];
  render(<AgentPage />);
  await ready();
  fireEvent.change(screen.getByLabelText("Tìm hội thoại"), {
    target: { value: "Viết" },
  });
  expect(
    screen.queryByRole("button", { name: "Ôn chương 4", exact: true }),
  ).toBeNull();
  fireEvent.click(
    await screen.findByRole("button", { name: "Viết báo cáo", exact: true }),
  );
  expect(await screen.findByText("Lịch báo cáo")).toBeTruthy();
  expect(calls.filter((call) => call.method === "POST")).toHaveLength(0);
});
it("opens the specific plan selected from an older reply instead of showing the latest plan", async () => {
  turns = [
    {
      ...result,
      id: "older",
      conversationId: "one",
      text: "Báo cáo trước",
      changes: [{ taskId: "older-task", name: "Kế hoạch trước" }],
      plan: {
        sessions: [
          {
            id: "old-session",
            taskId: "older-task",
            start: "2026-10-15T02:00:00Z",
            end: "2026-10-15T03:00:00Z",
          },
        ],
      },
    },
    {
      ...result,
      id: "newer",
      conversationId: "one",
      text: "Báo cáo sau",
      changes: [{ taskId: "newer-task", name: "Kế hoạch sau" }],
      plan: {
        sessions: [
          {
            id: "new-session",
            taskId: "newer-task",
            start: "2026-10-15T07:00:00Z",
            end: "2026-10-15T09:00:00Z",
          },
        ],
      },
    },
  ];
  render(<AgentPage />);
  await ready();
  const cards = screen.getAllByRole("button", {
    name: "Xem kết quả này",
  });
  expect(screen.queryByRole("dialog", { name: "Lịch của bạn" })).toBeNull();
  fireEvent.click(cards[0]);
  const panel = screen.getByRole("dialog", { name: "Lịch của bạn" });
  expect(panel.textContent).toContain("Kế hoạch trước");
  expect(panel.textContent).not.toContain("Kế hoạch sau");
  expect(calls.filter((call) => call.method === "POST")).toHaveLength(0);
});
it("retries an initial connection failure without leaving the Nora page", async () => {
  let offline = true;
  const fallback = globalThis.fetch;
  vi.stubGlobal(
    "fetch",
    vi.fn(async (url, options = {}) =>
      offline && url === "/api/agent/capabilities"
        ? {
            ok: false,
            status: 503,
            json: async () => ({
              error: { message: "Trợ lý tạm thời chưa khả dụng." },
            }),
          }
        : fallback(url, options),
    ),
  );
  render(<AgentPage />);
  await screen.findByText("Trợ lý tạm thời chưa khả dụng.");
  offline = false;
  fireEvent.click(screen.getByRole("button", { name: "Thử kết nối lại" }));
  await ready();
  expect(screen.getByRole("button", { name: "Gửi yêu cầu" }).disabled).toBe(
    false,
  );
  expect(screen.queryByRole("alert")).toBeNull();
});
it("copies the original prompt separately from the response and edits only the draft", async () => {
  const prompt = "Ngày mai học từ 7h đến 12h sáng.\nGiữ nguyên lịch khác.";
  turns = [
    {
      ...result,
      conversationId: "chat",
      text: prompt,
      createdAt: "2026-10-20T01:21:00Z",
    },
  ];
  const previous = Object.getOwnPropertyDescriptor(navigator, "clipboard");
  const writeText = vi.fn(async () => {});
  Object.defineProperty(navigator, "clipboard", {
    configurable: true,
    value: { writeText },
  });
  try {
    const view = render(<AgentPage />);
    await ready();
    fireEvent.click(screen.getByRole("button", { name: "Sao chép yêu cầu" }));
    await waitFor(() => expect(writeText).toHaveBeenLastCalledWith(prompt));
    fireEvent.click(screen.getByRole("button", { name: "Sao chép phản hồi" }));
    await waitFor(() =>
      expect(writeText).toHaveBeenLastCalledWith(result.reply),
    );
    fireEvent.click(
      screen.getByRole("button", { name: "Sửa và gửi lại yêu cầu" }),
    );
    expect(screen.getByLabelText("Yêu cầu của bạn").value).toBe(prompt);
    expect(
      calls.some(
        (call) => call.method === "POST" && call.url.includes("/turns"),
      ),
    ).toBe(false);
    expect(
      [...view.container.querySelectorAll(".agent-turn time")].map(
        (time) => time.dateTime,
      ),
    ).toEqual(["2026-10-20T01:21:00.000Z", "2026-10-20T01:21:00.000Z"]);
  } finally {
    if (previous) Object.defineProperty(navigator, "clipboard", previous);
    else delete navigator.clipboard;
  }
});

it.each(["yêu cầu", "phản hồi"])(
  "explains a clipboard failure for the %s instead of leaving an unresponsive copy control",
  async (kind) => {
    turns = [{ ...result, conversationId: "chat" }];
    const previous = Object.getOwnPropertyDescriptor(navigator, "clipboard");
    Object.defineProperty(navigator, "clipboard", {
      configurable: true,
      value: {
        writeText: async () => {
          throw new Error("denied");
        },
      },
    });
    try {
      render(<AgentPage />);
      await ready();
      fireEvent.click(screen.getByRole("button", { name: `Sao chép ${kind}` }));
      expect(
        await screen.findByText(/Không thể sao chép tự động/),
      ).toBeTruthy();
      expect(screen.queryByText("Đã sao chép")).toBeNull();
    } finally {
      if (previous) Object.defineProperty(navigator, "clipboard", previous);
      else delete navigator.clipboard;
    }
  },
);
it("updates the active title after rename and enables an archived conversation only after restore", async () => {
  turns = [{ ...result, conversationId: "chat", text: "Yêu cầu gốc" }];
  let row = {
    id: "chat",
    title: "Kế hoạch tuần",
    archived: false,
    version: "v1",
    turnCount: 1,
  };
  const fallback = globalThis.fetch;
  vi.stubGlobal(
    "fetch",
    vi.fn(async (raw, options = {}) => {
      const url = new URL(raw, "https://localhost");
      if (
        url.pathname === "/api/agent/conversations/chat" &&
        options.method === "PUT"
      ) {
        row = { ...row, ...JSON.parse(options.body), version: "v2" };
        return { ok: true, json: async () => ({ data: row }) };
      }
      if (url.pathname === "/api/agent/conversations")
        return {
          ok: true,
          json: async () => ({
            data: {
              items:
                row.archived === (url.searchParams.get("archived") === "true")
                  ? [row]
                  : [],
              nextCursor: null,
            },
          }),
        };
      return fallback(raw, options);
    }),
  );
  const view = render(<AgentPage />);
  await ready();
  fireEvent.click(
    await screen.findByRole("button", { name: "Tùy chọn Kế hoạch tuần" }),
  );
  fireEvent.click(screen.getByRole("menuitem", { name: "Đổi tên" }));
  fireEvent.change(screen.getByRole("textbox", { name: "Tên hội thoại" }), {
    target: { value: "Kế hoạch mới" },
  });
  fireEvent.click(screen.getByRole("button", { name: "Lưu tên" }));
  await waitFor(() =>
    expect(
      view.container.querySelector(".nora-chat-title").textContent,
    ).toContain("Kế hoạch mới"),
  );
  const options = await screen.findByRole("button", {
    name: "Tùy chọn Kế hoạch mới",
  });
  await waitFor(() => expect(options.disabled).toBe(false));
  fireEvent.click(options);
  fireEvent.click(
    screen.getByRole("menuitem", { name: "Lưu trữ", exact: true }),
  );
  await waitFor(() => expect(row.archived).toBe(true));
  fireEvent.click(screen.getByRole("tab", { name: "Đã lưu trữ" }));
  const archived = await screen.findByRole("button", {
    name: "Kế hoạch mới",
    exact: true,
  });
  await waitFor(() => expect(archived.disabled).toBe(false));
  fireEvent.click(archived);
  await screen.findByRole("button", { name: "Khôi phục hội thoại" });
  expect(screen.getByLabelText("Yêu cầu của bạn").disabled).toBe(true);
  fireEvent.click(screen.getByRole("button", { name: "Khôi phục hội thoại" }));
  await waitFor(() =>
    expect(screen.getByLabelText("Yêu cầu của bạn").disabled).toBe(false),
  );
  expect(row.archived).toBe(false);
  expect(
    view.container.querySelector(".nora-chat-title").textContent,
  ).toContain("Kế hoạch mới");
});
it("keeps the current conversation and deletion confirmation when the server rejects deletion", async () => {
  turns = [
    {
      ...result,
      id: "turn-1",
      conversationId: "test-conv",
      text: "Kế hoạch tuần",
      reply: "Nội dung cần giữ",
    },
  ];
  const fallback = globalThis.fetch;
  vi.stubGlobal(
    "fetch",
    vi.fn(async (url, options = {}) =>
      options.method === "DELETE"
        ? {
            ok: false,
            status: 503,
            json: async () => ({
              error: { message: "Chưa xóa được hội thoại. Hãy thử lại." },
            }),
          }
        : fallback(url, options),
    ),
  );
  render(<AgentPage />);
  await ready();
  await screen.findByText("Nội dung cần giữ");
  fireEvent.click(screen.getByRole("button", { name: "Xóa cuộc trò chuyện" }));
  fireEvent.click(screen.getByRole("button", { name: "Xóa vĩnh viễn" }));
  await screen.findByText("Chưa xóa được hội thoại. Hãy thử lại.");
  expect(screen.getByText("Nội dung cần giữ")).toBeTruthy();
  expect(screen.getByRole("button", { name: "Xóa vĩnh viễn" })).toBeTruthy();
});
it("dismisses a deletion confirmation when starting a different conversation", async () => {
  turns = [{ ...result, conversationId: "test-conv", text: "Kế hoạch tuần" }];
  render(<AgentPage />);
  await ready();
  fireEvent.click(screen.getByRole("button", { name: "Xóa cuộc trò chuyện" }));
  fireEvent.click(
    screen.getByRole("button", { name: "Hội thoại mới", exact: true }),
  );
  expect(screen.queryByRole("button", { name: "Xóa vĩnh viễn" })).toBeNull();
});
it("renders circular send button with nora-send-button class and supports deleting the current conversation", async () => {
  turns = [
    {
      ...result,
      id: "turn-1",
      conversationId: "test-conv",
      text: "Xếp lịch kiểm tra",
      reply: "Đã xếp",
      status: "APPLIED",
    },
  ];
  render(<AgentPage />);
  await waitFor(() =>
    expect(screen.queryByText("Đang tải trợ lý…")).toBeNull(),
  );
  const sendBtn = screen.getByRole("button", { name: "Gửi yêu cầu" });
  expect(sendBtn.className).toContain("nora-send-button");
  const delBtn = screen.getByRole("button", { name: "Xóa cuộc trò chuyện" });
  expect(delBtn.textContent).toBe("");
  fireEvent.click(delBtn);
  const dialog = screen.getByRole("alertdialog", {
    name: "Xóa cuộc trò chuyện?",
  });
  expect(dialog.textContent).toContain("Xếp lịch kiểm tra");
  fireEvent.click(screen.getByRole("button", { name: "Hủy" }));
  expect(screen.queryByRole("alertdialog")).toBeNull();
});
