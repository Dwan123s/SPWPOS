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
  fireEvent.click(screen.getByRole("button", { name: "Hoàn tác yêu cầu" }));
  await screen.findByText("Đã hoàn tác yêu cầu.");
  expect(changed).toHaveBeenCalledTimes(2);
});
it("preview mode leaves the workspace unchanged until explicit apply", async () => {
  result.status = "PREVIEW";
  result.canUndo = false;
  const changed = vi.fn(async () => {});
  render(<AgentPage onWorkspaceChanged={changed} />);
  await ready();
  fireEvent.click(screen.getByLabelText("Tự lưu khi lịch khả thi"));
  fireEvent.click(screen.getByRole("button", { name: "Gửi yêu cầu" }));
  await screen.findAllByText("Chờ áp dụng");
  expect(changed).not.toHaveBeenCalled();
  expect(calls.find((call) => call.method === "POST").body.autoApply).toBe(
    false,
  );
  fireEvent.click(screen.getByRole("button", { name: "Áp dụng yêu cầu này" }));
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
    name: /Kế hoạch của bạn.*phiên/,
  });
  fireEvent.click(cards[0]);
  const panel = screen.getByRole("complementary", { name: "Kế hoạch của bạn" });
  expect(panel.textContent).toContain("Kế hoạch trước");
  expect(panel.textContent).not.toContain("Kế hoạch sau");
  expect(calls.filter((call) => call.method === "POST")).toHaveLength(0);
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
  expect(delBtn).toBeTruthy();
  fireEvent.click(delBtn);
  expect(
    screen.getByText(/Bạn có chắc muốn xóa toàn bộ cuộc trò chuyện này\?/),
  ).toBeTruthy();
  fireEvent.click(screen.getByRole("button", { name: "Hủy" }));
  expect(
    screen.queryByText(/Bạn có chắc muốn xóa toàn bộ cuộc trò chuyện này\?/),
  ).toBeNull();
});
