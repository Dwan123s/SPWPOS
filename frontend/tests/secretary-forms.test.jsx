import { beforeEach, expect, it, vi } from "vitest";
import { act, fireEvent, render, screen } from "@testing-library/react";
import { App } from "../src/App.jsx";
import { timePage } from "../src/page-time.jsx";
import { scenarioPage } from "../src/page-scenario.jsx";
import { NotesPage } from "../src/notes.jsx";
import { openTask, captureModal, openDetail } from "../src/tasks.jsx";
import { eventModal, exceptionModal, sessionDetail } from "../src/calendar.jsx";
import { takeAgentDraft } from "../src/agent-handoff.js";
import { state } from "../src/ui.jsx";
import { update } from "../src/store.js";
import { workspace, resetFixtures } from "./fixtures.js";

beforeEach(() => {
  resetFixtures();
  state.workspace = workspace();
  state.workspace.features = [
    ...(state.workspace.features || []),
    "secretary-v1",
  ];
  takeAgentDraft();
  update({
    screen: null,
    screenKey: "loading",
    modal: null,
    toasts: [],
    slots: {},
  });
  window.history.replaceState(null, "", "/");
});

it("keeps manual working hours available independently of NORA", () => {
  state.noraAvailable = false;
  render(timePage());
  expect(document.querySelector("form[data-form='availability']")).toBeTruthy();
  expect(screen.getByRole("button", { name: "Lưu lịch rảnh" })).toBeTruthy();
  expect(screen.queryByRole("button", { name: "Nhờ NORA" })).toBeNull();
});

it("opens manual task and event forms in secretary mode and offers NORA separately", () => {
  state.noraAvailable = true;
  render(<App initialize={false} />);
  act(() => openTask());
  expect(screen.getByRole("button", { name: "Lưu công việc" })).toBeTruthy();
  expect(document.querySelector("input[name='deadline']").value).toBe("");
  expect(screen.getByRole("button", { name: "Nhờ NORA" })).toBeTruthy();
  fireEvent.change(screen.getByLabelText("Tên công việc"), { target: { value: "Five-hour report" } });
  fireEvent.click(screen.getByRole("button", { name: "Lưu nháp" }));
  expect(screen.getByRole("textbox").value).toBe("Five-hour report");
  act(() => eventModal());
  expect(screen.getByRole("button", { name: "Lưu sự kiện" })).toBeTruthy();
  expect(location.hash).not.toBe("#agent");
});

it("lets users finish work without entering percentages or dependencies", () => {
  render(<App initialize={false} />);
  act(() => openDetail(state.workspace.tasks[0].id));
  expect(
    document.querySelector(
      "form[data-form='task-progress'], form[data-form='dependency']",
    ),
  ).toBeNull();
  expect(
    screen.getByRole("button", { name: "Đã hoàn thành công việc" }),
  ).toBeTruthy();
});

it("keeps the session result action available in secretary mode", () => {
  render(<App initialize={false} />);
  act(() => sessionDetail(state.workspace.sessions[0].id));
  expect(screen.getByRole("button", { name: "Ghi kết quả phiên" })).toBeTruthy();
});

it("replaces the scenario parameter form with an unsent conversation request", () => {
  render(scenarioPage());
  expect(document.querySelector("form, input, select, textarea")).toBeNull();
  fireEvent.click(screen.getByRole("button", { name: "Nhờ NORA đánh giá" }));
  expect(takeAgentDraft()).toContain("Chỉ tư vấn");
});

it("keeps the original Notes editor available in secretary mode", async () => {
  vi.stubGlobal(
    "fetch",
    vi.fn(
      async (url) =>
        new Response(
          JSON.stringify({
            data:
              url === "/api/notes"
                ? []
                : { aiAvailable: true, provider: "Gemini" },
          }),
          { status: 200 },
        ),
    ),
  );
  render(<NotesPage />);
  await screen.findByLabelText("Nội dung ghi chú");
  expect(screen.getByRole("button", { name: "Lưu bản nháp" })).toBeTruthy();
  expect(
    screen.getByRole("button", { name: "Tạo và lưu lịch ngay" }),
  ).toBeTruthy();
});
